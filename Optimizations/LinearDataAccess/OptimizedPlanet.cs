using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Spraycoaters;

namespace Weaver.Optimizations.LinearDataAccess;

internal enum OptimizedPlanetStatus
{
    Running,
    Stopped
}

internal sealed class OptimizedPlanet
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedEntities = [];

    private readonly PlanetFactory _planet;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public AssemblerExecutor _assemblerExecutor;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    //private NetworkIdAndState<LabState>[] _labProduceNetworkIdAndStates;
    public ProducingLabExecutor _producingLabExecutor;
    public NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    public OptimizedProducingLab[] _optimizedProducingLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public Dictionary<int, int> _producingLabIdToOptimizedIndex;

    public ResearchingLabExecutor _researchingLabExecutor;
    public NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    public OptimizedResearchingLab[] _optimizedResearchingLabs;
    public Dictionary<int, int> _researchingLabIdToOptimizedIndex;

    public SpraycoaterExecutor _spraycoaterExecutor;

    private OptimizedPowerSystem _optimizedPowerSystem;

    public OptimizedPlanet(PlanetFactory planet)
    {
        _planet = planet;
    }

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(OptimizedPlanet));
    }

    public static OptimizedPlanet GetOptimizedPlanet(PlanetFactory planet) => _planetToOptimizedEntities[planet];

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(OptimizedPlanet)}");

        _planetToOptimizedEntities.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            _planetToOptimizedEntities.Add(planet, new OptimizedPlanet(planet));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogMessage($"Saving {nameof(OptimizedPlanet)}");

        foreach (OptimizedPlanet optimizedPlanet in _planetToOptimizedEntities.Values)
        {
            optimizedPlanet.Save();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    public static void GameData_GameTick(long time)
    {
        foreach (KeyValuePair<PlanetFactory, OptimizedPlanet> planetToOptimizedPlanet in _planetToOptimizedEntities)
        {
            if (GameMain.localPlanet?.factory != planetToOptimizedPlanet.Key)
            {
                if (planetToOptimizedPlanet.Value.Status == OptimizedPlanetStatus.Stopped)
                {
                    WeaverFixes.Logger.LogMessage($"Optimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
                    planetToOptimizedPlanet.Value.Initialize();
                }

                continue;
            }

            if (planetToOptimizedPlanet.Value.Status == OptimizedPlanetStatus.Stopped)
            {
                continue;
            }

            WeaverFixes.Logger.LogMessage($"Deoptimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
            planetToOptimizedPlanet.Value.Save();
        }
    }

    public void Save()
    {
        _optimizedBiInserterExecutor.Save(_planet);
        _optimizedInserterExecutor.Save(_planet);
        _assemblerExecutor.Save(_planet);
        _producingLabExecutor.Save(_planet);
        _researchingLabExecutor.Save(_planet);
        _spraycoaterExecutor.Save(_planet);

        Status = OptimizedPlanetStatus.Stopped;
    }

    public void Initialize()
    {
        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(_planet.powerSystem);

        InitializeAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeMiners(_planet);
        InitializeEjectors(_planet);
        InitializeLabAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeResearchingLabs(_planet, optimizedPowerSystemBuilder);
        InitializeInserters(_planet, optimizedPowerSystemBuilder);
        InitializeSpraycoaters(_planet, optimizedPowerSystemBuilder);

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();

        Status = OptimizedPlanetStatus.Running;
    }

    private void InitializeInserters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedBiInserterExecutor.Initialize(planet, this, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder());

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedInserterExecutor.Initialize(planet, this, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder());
    }

    private void InitializeAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(planet, optimizedPowerSystemBuilder);
    }

    private void InitializeMiners(PlanetFactory planet)
    {
        int[] minerNetworkIds = new int[planet.factorySystem.minerCursor];

        for (int i = 0; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[i];
            if (miner.id != i)
            {
                continue;
            }

            minerNetworkIds[i] = planet.powerSystem.consumerPool[miner.pcId].networkId;

        }

        _minerNetworkIds = minerNetworkIds;
    }

    private void InitializeEjectors(PlanetFactory planet)
    {
        int[] ejectorNetworkIds = new int[planet.factorySystem.ejectorCursor];

        for (int i = 0; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[i];
            if (ejector.id != i)
            {
                continue;
            }

            ejectorNetworkIds[i] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }

    private void InitializeLabAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor();
        _researchingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeSpraycoaters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(planet, optimizedPowerSystemBuilder);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.InserterPartExecute))]
    public static bool InserterPartExecute(WorkerThreadExecutor __instance)
    {
        InserterPartExecute(__instance, x => x._optimizedBiInserterExecutor);
        InserterPartExecute(__instance, x => x._optimizedInserterExecutor);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void InserterPartExecute<T>(WorkerThreadExecutor __instance, Func<OptimizedPlanet, InserterExecutor<T>> inserterExecutorSelector)
        where T : struct, IInserter<T>
    {
        if (__instance.inserterFactories == null)
        {
            return;
        }
        int totalGalaxyInserterCount = 0;
        for (int planetIndex = 0; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            InserterExecutor<T> optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);
            totalGalaxyInserterCount += optimizedInserterExecutor.inserterCount;
        }
        int minimumMissionCnt = 64;
        if (!WorkerThreadExecutor.CalculateMissionIndex(totalGalaxyInserterCount, __instance.usedThreadCnt, __instance.curThreadIdx, minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }
        int threadStartingPlanetIndex = 0;
        int totalInsertersSeenOnPreviousPlanets = 0;
        for (int planetIndex = 0; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            InserterExecutor<T> optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            int totalInsertersIncludingOnThisPlanets = totalInsertersSeenOnPreviousPlanets + optimizedInserterExecutor.inserterCount;
            if (totalInsertersIncludingOnThisPlanets <= _start)
            {
                totalInsertersSeenOnPreviousPlanets = totalInsertersIncludingOnThisPlanets;
                continue;
            }
            threadStartingPlanetIndex = planetIndex;
            break;
        }
        for (int planetIndex = threadStartingPlanetIndex; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            InserterExecutor<T> optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            bool isActive = __instance.inserterLocalPlanet == __instance.inserterFactories[planetIndex].planet;
            int num5 = _start - totalInsertersSeenOnPreviousPlanets;
            int num6 = _end - totalInsertersSeenOnPreviousPlanets;
            if (_end - _start > optimizedInserterExecutor.inserterCount - num5)
            {
                try
                {
                    if (!isActive)
                    {
                        optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, optimizedInserterExecutor.inserterCount);
                    }
                    else
                    {
                        int planetInserterStartIndex = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num5);
                        int planetInserterEnd = optimizedInserterExecutor.GetUnoptimizedInserterIndex(optimizedInserterExecutor.inserterCount - 1) + 1;
                        __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, planetInserterStartIndex, planetInserterEnd);
                    }
                    totalInsertersSeenOnPreviousPlanets += optimizedInserterExecutor.inserterCount;
                    _start = totalInsertersSeenOnPreviousPlanets;
                }
                catch (Exception ex)
                {
                    __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter first gametick total cursor: " + optimizedInserterExecutor.inserterCount + "  Start & End: " + num5 + "/" + optimizedInserterExecutor.inserterCount + "  " + ex;
                    __instance.hasErrorMessage = true;
                }
                continue;
            }
            try
            {
                if (!isActive)
                {
                    optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, num6);
                }
                else
                {
                    int planetInserterStartIndex = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num5);
                    int planetInserterEnd = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num6 - 1) + 1;
                    __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, planetInserterStartIndex, planetInserterEnd);
                }
                break;
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter second gametick total cursor: " + optimizedInserterExecutor.inserterCount + "  Start & End: " + num5 + "/" + num6 + "  " + ex2;
                __instance.hasErrorMessage = true;
                break;
            }
        }
    }



    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.AssemblerPartExecute))]
    public static bool AssemblerPartExecute(WorkerThreadExecutor __instance)
    {
        if (__instance.assemblerFactories == null)
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }
        for (int i = 0; i < __instance.assemblerFactoryCnt; i++)
        {
            bool isActive = __instance.assemblerLocalPlanet == __instance.assemblerFactories[i].planet;
            try
            {
                if (__instance.assemblerFactories[i].factorySystem != null)
                {
                    if (!isActive)
                    {
                        PlanetFactory planet = __instance.assemblerFactories[i];
                        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
                        optimizedPlanet.GameTick(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
                    else
                    {
                        __instance.assemblerFactories[i].factorySystem.GameTick(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }

                }
            }
            catch (Exception ex)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Assembler Factory idx:" + i.ToString() + " Assembler gametick " + ex;
                __instance.hasErrorMessage = true;
            }
            try
            {
                if (__instance.assemblerFactories[i].factorySystem != null)
                {
                    if (!isActive)
                    {
                        PlanetFactory planet = __instance.assemblerFactories[i];
                        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
                        optimizedPlanet._producingLabExecutor.GameTickLabProduceMode(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
                    else
                    {
                        __instance.assemblerFactories[i].factorySystem.GameTickLabProduceMode(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
                }
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Lab Produce Factory idx:" + i.ToString() + " lab produce gametick " + ex2;
                __instance.hasErrorMessage = true;
            }
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    private void GameTick(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        EntityData[] entityPool = planet.entityPool;
        VeinData[] veinPool = planet.veinPool;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SignData[] entitySignPool = planet.entitySignPool;
        int[][] entityNeeds = planet.entityNeeds;
        FactorySystem factorySystem = planet.factorySystem;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        float num = 1f / 60f;
        AstroData[] astroPoses = null;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            float num2;
            float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
            if (num2 < 5f / 12f)
            {
                num2 = 5f / 12f;
            }
            float num4 = history.miningCostRate;
            float miningSpeedScale = history.miningSpeedScale;
            float num5 = history.miningCostRate * 0.40111667f / num2;
            if (num3 > 99.5f)
            {
                num4 = 0f;
                num5 = 0f;
            }
            bool flag2 = isActive && num4 > 0f;

            // Change back when dsp package as updated
            //int num6 = MinerComponent.InsufficientWarningThresAmount(num3, num4);
            int num6 = 4;

            for (int i = _start; i < _end; i++)
            {
                if (factorySystem.minerPool[i].id != i)
                {
                    continue;
                }
                int entityId = factorySystem.minerPool[i].entityId;
                int stationId = entityPool[entityId].stationId;
                float num7 = networkServes[consumerPool[factorySystem.minerPool[i].pcId].networkId];
                uint num8 = factorySystem.minerPool[i].InternalUpdate(planet, veinPool, num7, (factorySystem.minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
                int num9 = (int)Mathf.Floor(entityAnimPool[entityId].time / 10f);
                entityAnimPool[entityId].time = entityAnimPool[entityId].time % 10f;
                entityAnimPool[entityId].Step(num8, num * num7);
                entityAnimPool[entityId].power = num7;
                if (stationId > 0)
                {
                    if (factorySystem.minerPool[i].veinCount > 0)
                    {
                        EVeinType veinTypeByItemId = LDB.veins.GetVeinTypeByItemId(veinPool[factorySystem.minerPool[i].veins[0]].productId);
                        entityAnimPool[entityId].state += (uint)((int)veinTypeByItemId * 100);
                    }
                    entityAnimPool[entityId].power += 10f;
                    entityAnimPool[entityId].power += factorySystem.minerPool[i].speed / 10 * 10;
                    if (num8 == 1)
                    {
                        num9 = 3000;
                    }
                    else
                    {
                        num9 -= (int)(num * 1000f);
                        if (num9 < 0)
                        {
                            num9 = 0;
                        }
                    }
                    entityAnimPool[entityId].time += num9 * 10;
                }
                if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                {
                    entitySignPool[entityId].signType = ((factorySystem.minerPool[i].minimumVeinAmount < num6) ? 7u : 0u);
                }
                if (flag2 && factorySystem.minerPool[i].type == EMinerType.Vein)
                {
                    if ((long)i % 30L == time % 30)
                    {
                        factorySystem.minerPool[i].GetTotalVeinAmount(veinPool);
                    }
                    entitySignPool[entityId].count0 = factorySystem.minerPool[i].totalVeinAmount;
                }
                else
                {
                    entitySignPool[entityId].count0 = 0f;
                }
            }
        }

        _assemblerExecutor.GameTick(planet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.fractionatorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int l = _start; l < _end; l++)
            {
                if (factorySystem.fractionatorPool[l].id == l)
                {
                    int entityId4 = factorySystem.fractionatorPool[l].entityId;
                    float power2 = networkServes[consumerPool[factorySystem.fractionatorPool[l].pcId].networkId];
                    uint state = factorySystem.fractionatorPool[l].InternalUpdate(factorySystem.factory, power2, entitySignPool, productRegister, consumeRegister);
                    entityAnimPool[entityId4].time = Mathf.Sqrt((float)factorySystem.fractionatorPool[l].fluidInputCount * 0.025f);
                    entityAnimPool[entityId4].state = state;
                    entityAnimPool[entityId4].power = power2;
                }
            }
        }
        lock (factorySystem.ejectorPool)
        {
            if (factorySystem.ejectorCursor - factorySystem.ejectorRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSwarm swarm = null;
        if (factorySystem.factory.dysonSphere != null)
        {
            swarm = factorySystem.factory.dysonSphere.swarm;
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.ejectorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            int[] ejectorNetworkIds = _ejectorNetworkIds;
            for (int m = _start; m < _end; m++)
            {
                if (factorySystem.ejectorPool[m].id == m)
                {
                    float power3 = networkServes[ejectorNetworkIds[m]];
                    uint num11 = factorySystem.ejectorPool[m].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);

                    if (isActive)
                    {
                        int entityId5 = factorySystem.ejectorPool[m].entityId;
                        entityAnimPool[entityId5].state = num11;

                        if (entitySignPool[entityId5].signType == 0 || entitySignPool[entityId5].signType > 3)
                        {
                            entitySignPool[entityId5].signType = ((factorySystem.ejectorPool[m].orbitId <= 0 && !factorySystem.ejectorPool[m].autoOrbit) ? 5u : 0u);
                        }
                    }
                }
            }
        }
        lock (factorySystem.siloPool)
        {
            if (factorySystem.siloCursor - factorySystem.siloRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        bool flag3 = dysonSphere != null && dysonSphere.autoNodeCount > 0;
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.siloCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            return;
        }
        for (int n = _start; n < _end; n++)
        {
            if (factorySystem.siloPool[n].id == n)
            {
                int entityId6 = factorySystem.siloPool[n].entityId;
                uint num12 = 0u;
                float power4 = networkServes[consumerPool[factorySystem.siloPool[n].pcId].networkId];
                num12 = factorySystem.siloPool[n].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
                entityAnimPool[entityId6].state = num12;
                entityNeeds[entityId6] = factorySystem.siloPool[n].needs;
                if (entitySignPool[entityId6].signType == 0 || entitySignPool[entityId6].signType > 3)
                {
                    entitySignPool[entityId6].signType = ((!flag3) ? 9u : 0u);
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.ParallelGameTickBeforePower))]
    public static bool FactorySystem_ParallelGameTickBeforePower(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._optimizedPowerSystem.FactorySystem_ParallelGameTickBeforePower(__instance.factory, optimizedPlanet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CargoTraffic), nameof(CargoTraffic.ParallelGameTickBeforePower))]
    public static bool CargoTraffic_ParallelGameTickBeforePower(CargoTraffic __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._optimizedPowerSystem.CargoTraffic_ParallelGameTickBeforePower(__instance.factory, optimizedPlanet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    public static bool GameTick(PowerSystem __instance, long time, bool isActive, bool isMultithreadMode = false)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPowerSystem optimizedPowerSystem = _planetToOptimizedEntities[__instance.factory]._optimizedPowerSystem;
        optimizedPowerSystem.GameTick(__instance.factory, time, isActive, isMultithreadMode);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabResearchMode))]
    public static bool GameTickLabResearchMode(FactorySystem __instance, long time, bool isActive)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._researchingLabExecutor.GameTickLabResearchMode(__instance.factory, time);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem),
                  nameof(FactorySystem.GameTickLabOutputToNext),
                  [typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int)])]
    public static bool GameTickLabOutputToNext(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._producingLabExecutor.GameTickLabOutputToNext(time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);
        optimizedPlanet._researchingLabExecutor.GameTickLabOutputToNext(time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    //[HarmonyPrefix]
    //[HarmonyPatch(typeof(ModelProtoSet), nameof(ModelProtoSet.Select))]
    //public static ModelProto Select(ModelProtoSet __instance, int id)
    //{
    //    if (__instance.dataIndices.TryGetValue(id, out int index))
    //    {
    //        return __instance.dataArray[index];
    //    }
    //    return null;
    //}

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CargoTraffic), nameof(CargoTraffic.SpraycoaterGameTick))]
    public static bool SpraycoaterGameTick(CargoTraffic __instance)
    {
        bool isActive = GameMain.localPlanet != null && GameMain.localPlanet.factory == __instance.factory;
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(__instance.factory);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void ParallelSpraycoaterLogic()
    {
        PerformanceMonitor.BeginSample(ECpuWorkEntry.Belt);

        PlanetFactory? localFactory = GameMain.localPlanet?.factory;
        Parallel.For(0, GameMain.data.factoryCount, i =>
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet.cargoTraffic == null)
            {
                return;
            }

            bool isActive = localFactory == planet;
            if (isActive)
            {
                planet.cargoTraffic.SpraycoaterGameTick();
                return;
            }

            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            optimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(planet);
        });

        PerformanceMonitor.EndSample(ECpuWorkEntry.Belt);
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    static IEnumerable<CodeInstruction> ReplaceSingleThreadedSpraycoaterLogicWithParallelOptimizedLogic(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        CodeMatch[] sprayCoaterGameTickCall = [
            // factories[num3].cargoTraffic.SpraycoaterGameTick();
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameData), nameof(GameData.factories))),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Ldelem_Ref),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetFactory), nameof(PlanetFactory.cargoTraffic))),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(CargoTraffic), nameof(CargoTraffic.SpraycoaterGameTick))),
        ];

        CodeMatch[] afterSpraycoaterLoop = [
            new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Storage),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.BeginSample)))
        ];

        codeMatcher.MatchForward(false, sprayCoaterGameTickCall)
            .ThrowIfNotMatch($"Failed to find {nameof(sprayCoaterGameTickCall)}")
            .RemoveInstructions(sprayCoaterGameTickCall.Length)
            .MatchForward(false, afterSpraycoaterLoop)
            .ThrowIfNotMatch($"Failed to find {nameof(afterSpraycoaterLoop)}")
            .Insert([
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OptimizedPlanet), nameof(ParallelSpraycoaterLogic)))
            ]);

        return codeMatcher.InstructionEnumeration();
    }

    public TypedObjectIndex GetAsGranularTypedObjectIndex(int index, PlanetFactory planet)
    {
        ref readonly EntityData entity = ref planet.entityPool[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            if (!_assemblerExecutor._assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
            {
                throw new InvalidOperationException("Failed to convert assembler id into optimized assembler id.");
            }

            return new TypedObjectIndex(EntityType.Assembler, optimizedAssemblerIndex);
        }
        else if (entity.ejectorId != 0)
        {
            return new TypedObjectIndex(EntityType.Ejector, entity.ejectorId);
        }
        else if (entity.siloId != 0)
        {
            return new TypedObjectIndex(EntityType.Silo, entity.siloId);
        }
        else if (entity.labId != 0)
        {
            if (planet.factorySystem.labPool[entity.labId].researchMode)
            {
                if (!_researchingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    throw new InvalidOperationException("Failed to convert researching lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ResearchingLab, optimizedLabIndex);
            }
            else
            {
                if (!_producingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    throw new InvalidOperationException("Failed to convert producing lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ProducingLab, optimizedLabIndex);
            }
        }
        else if (entity.storageId != 0)
        {
            return new TypedObjectIndex(EntityType.Storage, entity.storageId);
        }
        else if (entity.stationId != 0)
        {
            return new TypedObjectIndex(EntityType.Station, entity.stationId);
        }
        else if (entity.powerGenId != 0)
        {
            return new TypedObjectIndex(EntityType.PowerGenerator, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new TypedObjectIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new TypedObjectIndex(EntityType.Inserter, entity.inserterId);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }

    public static int[]? GetEntityNeeds(PlanetFactory planet, int entityIndex)
    {
        ref readonly EntityData entity = ref planet.entityPool[entityIndex];
        if (entity.beltId != 0)
        {
            return null;
        }
        else if (entity.assemblerId != 0)
        {
            return planet.factorySystem.assemblerPool[entity.assemblerId].needs;
        }
        else if (entity.ejectorId != 0)
        {
            return planet.factorySystem.ejectorPool[entity.ejectorId].needs;
        }
        else if (entity.siloId != 0)
        {
            return planet.factorySystem.siloPool[entity.siloId].needs;
        }
        else if (entity.labId != 0)
        {
            return planet.factorySystem.labPool[entity.labId].needs;
        }
        else if (entity.storageId != 0)
        {
            return null;
        }
        else if (entity.stationId != 0)
        {
            return planet.transport.stationPool[entity.stationId].needs;
        }
        else if (entity.powerGenId != 0)
        {
            return null;
        }
        else if (entity.splitterId != 0)
        {
            return null;
        }
        else if (entity.inserterId != 0)
        {
            return null;
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}