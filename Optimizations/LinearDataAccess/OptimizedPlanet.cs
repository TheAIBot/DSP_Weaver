using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedEntities = [];

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates;
    public OptimizedAssembler[] _optimizedAssemblers;
    public bool[] _assemblerReplicatings;
    public int[] _assemblerExtraPowerRatios;
    public AssemblerRecipe[] _assemblerRecipes;
    public Dictionary<int, int> _assemblerIdToOptimizedIndex;
    public AssemblerExecutor _assemblerExecutor;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    //private NetworkIdAndState<LabState>[] _labProduceNetworkIdAndStates;
    private ProducingLabExecutor _producingLabExecutor;
    private NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    public OptimizedProducingLab[] _optimizedProducingLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public Dictionary<int, int> _producingLabIdToOptimizedIndex;

    private ResearchingLabExecutor _researchingLabExecutor;
    private NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    private OptimizedResearchingLab[] _optimizedResearchingLabs;
    public Dictionary<int, int> _researchingLabIdToOptimizedIndex;


    private OptimizedPowerSystem _optimizedPowerSystem;

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

            var optimizedInserters = new OptimizedPlanet();
            optimizedInserters.InitializeData(planet);
            _planetToOptimizedEntities.Add(planet, optimizedInserters);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogMessage($"Saving {nameof(OptimizedPlanet)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet == GameMain.localPlanet.factory)
            {
                continue;
            }

            if (_planetToOptimizedEntities.TryGetValue(planet, out OptimizedPlanet optimizedInserters))
            {
                optimizedInserters.Save(planet);
            }
        }
    }

    public void InitializeData(PlanetFactory planet)
    {
        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(planet.powerSystem);

        InitializeAssemblers(planet, optimizedPowerSystemBuilder);
        InitializeMiners(planet);
        InitializeEjectors(planet);
        InitializeLabAssemblers(planet);
        InitializeResearchingLabs(planet);
        InitializeInserters(planet, optimizedPowerSystemBuilder);

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();
    }

    private void InitializeInserters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>();
        _optimizedBiInserterExecutor.Initialize(planet, this, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder());

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>();
        _optimizedInserterExecutor.Initialize(planet, this, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder());
    }

    private void InitializeAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();

        List<NetworkIdAndState<AssemblerState>> assemblerNetworkIdAndStates = [];
        List<OptimizedAssembler> optimizedAssemblers = [];
        List<bool> assemblerReplicatings = [];
        List<int> assemblerExtraPowerRatios = [];
        Dictionary<AssemblerRecipe, int> assemblerRecipeToIndex = [];
        List<AssemblerRecipe> assemblerRecipes = [];
        Dictionary<int, int> assemblerIdToOptimizedIndex = [];

        for (int i = 0; i < planet.factorySystem.assemblerCursor; i++)
        {
            ref AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            if (assembler.recipeId == 0)
            {
                continue;
            }

            AssemblerRecipe assemblerRecipe = new AssemblerRecipe(assembler.recipeId,
                                                                  assembler.recipeType,
                                                                  assembler.timeSpend,
                                                                  assembler.extraTimeSpend,
                                                                  assembler.productive,
                                                                  assembler.requires,
                                                                  assembler.requireCounts,
                                                                  assembler.products,
                                                                  assembler.productCounts);
            if (!assemblerRecipeToIndex.TryGetValue(assemblerRecipe, out int assemblerRecipeIndex))
            {
                assemblerRecipeIndex = assemblerRecipeToIndex.Count;
                assemblerRecipeToIndex.Add(assemblerRecipe, assemblerRecipeIndex);
                assemblerRecipes.Add(assemblerRecipe);
            }

            assemblerIdToOptimizedIndex.Add(assembler.id, optimizedAssemblers.Count);
            int networkIndex = planet.powerSystem.consumerPool[assembler.pcId].networkId;
            assemblerNetworkIdAndStates.Add(new NetworkIdAndState<AssemblerState>((int)(assembler.recipeId == 0 ? AssemblerState.InactiveNoRecipeSet : AssemblerState.Active),
                                                                                  networkIndex));
            optimizedAssemblers.Add(new OptimizedAssembler(assemblerRecipeIndex, ref assembler));
            assemblerReplicatings.Add(assembler.replicating);
            assemblerExtraPowerRatios.Add(assembler.extraPowerRatio);
            optimizedPowerSystemBuilder.AddAssembler(ref assembler, networkIndex);


            // set it here so we don't have to set it in the update loop.
            // Need to remember to update it when the assemblers recipe is changed.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates.ToArray();
        _assemblerRecipes = assemblerRecipes.ToArray();
        _optimizedAssemblers = optimizedAssemblers.ToArray();
        _assemblerReplicatings = assemblerReplicatings.ToArray();
        _assemblerExtraPowerRatios = assemblerExtraPowerRatios.ToArray();
        _assemblerIdToOptimizedIndex = assemblerIdToOptimizedIndex;
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

    private void InitializeLabAssemblers(PlanetFactory planet)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(planet);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(PlanetFactory planet)
    {
        _researchingLabExecutor = new ResearchingLabExecutor();
        _researchingLabExecutor.Initialize(planet);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    public void Save(PlanetFactory planet)
    {
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.InserterPartExecute))]
    public static bool InserterPartExecute(WorkerThreadExecutor __instance)
    {
        InserterPartExecute(__instance, x => x._optimizedBiInserterExecutor);
        InserterPartExecute(__instance, x => x._optimizedInserterExecutor);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void InserterPartExecute<T>(WorkerThreadExecutor __instance, Func<OptimizedPlanet, T> inserterExecutorSelector)
        where T : IInserterExecutor
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
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);
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
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

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
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

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

    public static int PickFrom(PlanetFactory planet,
                               OptimizedPlanet optimizedPlanet,
                               ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                               ref readonly InserterConnections inserterConnections,
                               int inserterIndex,
                               PickFromProducingPlant[] pickFromProducingPlants,
                               int offset,
                               int filter,
                               int[] needs,
                               out byte stack,
                               out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = inserterConnections.PickFrom;
        int objectIndex = typedObjectIndex.Index;
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            if (needs == null)
            {
                return planet.cargoTraffic.TryPickItem(objectIndex, offset, filter, out stack, out inc);
            }
            return planet.cargoTraffic.TryPickItem(objectIndex, offset, filter, needs, out stack, out inc);
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = (AssemblerState)optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State;
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveOutputFull)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactivePickFrom;
                return 0;
            }

            PickFromProducingPlant producingPlant = pickFromProducingPlants[inserterIndex];
            int[] products = producingPlant.Products;
            int[] produced = producingPlant.Produced;

            int num = products.Length;
            switch (num)
            {
                case 1:
                    if (produced[0] > 0 && products[0] > 0 && (filter == 0 || filter == products[0]) && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        int value = Interlocked.Decrement(ref produced[0]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                            return products[0];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[0]);
                        }
                    }
                    break;
                case 2:
                    if ((filter == products[0] || filter == 0) && produced[0] > 0 && products[0] > 0 && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        int value = Interlocked.Decrement(ref produced[0]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                            return products[0];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[0]);
                        }
                    }
                    if ((filter == products[1] || filter == 0) && produced[1] > 0 && products[1] > 0 && (needs == null || needs[0] == products[1] || needs[1] == products[1] || needs[2] == products[1] || needs[3] == products[1] || needs[4] == products[1] || needs[5] == products[1]))
                    {
                        int value = Interlocked.Decrement(ref produced[1]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                            return products[1];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[1]);
                        }
                    }
                    break;
                default:
                    {
                        for (int i = 0; i < num; i++)
                        {
                            if ((filter == products[i] || filter == 0) && produced[i] > 0 && products[i] > 0 && (needs == null || needs[0] == products[i] || needs[1] == products[i] || needs[2] == products[i] || needs[3] == products[i] || needs[4] == products[i] || needs[5] == products[i]))
                            {
                                int value = Interlocked.Decrement(ref produced[i]);
                                if (value >= 0)
                                {
                                    optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                                    return products[i];
                                }
                                else
                                {
                                    Interlocked.Increment(ref produced[i]);
                                }
                            }
                        }
                        break;
                    }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[objectIndex];
            lock (planet.entityMutexs[ejector.entityId])
            {
                int bulletId = ejector.bulletId;
                int bulletCount = ejector.bulletCount;
                if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && (needs == null || needs[0] == bulletId || needs[1] == bulletId || needs[2] == bulletId || needs[3] == bulletId || needs[4] == bulletId || needs[5] == bulletId))
                {
                    ejector.TakeOneBulletUnsafe(out inc);
                    return bulletId;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            ref SiloComponent silo = ref planet.factorySystem.siloPool[objectIndex];
            lock (planet.entityMutexs[silo.entityId])
            {
                int bulletId2 = silo.bulletId;
                int bulletCount2 = silo.bulletCount;
                if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && (needs == null || needs[0] == bulletId2 || needs[1] == bulletId2 || needs[2] == bulletId2 || needs[3] == bulletId2 || needs[4] == bulletId2 || needs[5] == bulletId2))
                {
                    silo.TakeOneBulletUnsafe(out inc);
                    return bulletId2;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            int inc2;
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            StorageComponent storageComponent2 = storageComponent;
            if (storageComponent != null)
            {
                storageComponent = storageComponent.topStorage;
                while (storageComponent != null)
                {
                    lock (planet.entityMutexs[storageComponent.entityId])
                    {
                        if (storageComponent.lastEmptyItem != 0 && storageComponent.lastEmptyItem != filter)
                        {
                            int itemId = filter;
                            int count = 1;
                            bool flag = false;
                            if (needs == null)
                            {
                                storageComponent.TakeTailItems(ref itemId, ref count, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                                inc = (byte)inc2;
                                flag = count == 1;
                            }
                            else
                            {
                                bool flag2 = storageComponent.TakeTailItems(ref itemId, ref count, needs, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                                inc = (byte)inc2;
                                flag = count == 1 || flag2;
                            }
                            if (count == 1)
                            {
                                storageComponent.lastEmptyItem = -1;
                                return itemId;
                            }
                            if (!flag)
                            {
                                storageComponent.lastEmptyItem = filter;
                            }
                        }
                        if (storageComponent == storageComponent2)
                        {
                            break;
                        }
                        storageComponent = storageComponent.previousStorage;
                        continue;
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            int inc2;
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (stationComponent != null)
            {
                lock (planet.entityMutexs[stationComponent.entityId])
                {
                    int _itemId = filter;
                    int _count = 1;
                    if (needs == null)
                    {
                        stationComponent.TakeItem(ref _itemId, ref _count, out inc2);
                        inc = (byte)inc2;
                    }
                    else
                    {
                        stationComponent.TakeItem(ref _itemId, ref _count, needs, out inc2);
                        inc = (byte)inc2;
                    }
                    if (_count == 1)
                    {
                        return _itemId;
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ProducingLab)
        {
            PickFromProducingPlant producingPlant = pickFromProducingPlants[inserterIndex];
            int[] products2 = producingPlant.Products;
            int[] produced2 = producingPlant.Produced;
            if (products2 == null || produced2 == null)
            {
                return 0;
            }
            for (int j = 0; j < products2.Length; j++)
            {
                if (produced2[j] > 0 && products2[j] > 0 && (filter == 0 || filter == products2[j]) && (needs == null || needs[0] == products2[j] || needs[1] == products2[j] || needs[2] == products2[j] || needs[3] == products2[j] || needs[4] == products2[j] || needs[5] == products2[j]))
                {
                    int value = Interlocked.Decrement(ref produced2[j]);
                    if (value >= 0)
                    {
                        return products2[j];
                    }
                    else
                    {
                        Interlocked.Increment(ref produced2[j]);
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            ref PowerGeneratorComponent powerGenerator = ref planet.powerSystem.genPool[offset];
            int inc2;
            if (offset > 0 && planet.powerSystem.genPool[offset].id == offset)
            {
                lock (planet.entityMutexs[powerGenerator.entityId])
                {
                    if (planet.powerSystem.genPool[offset].fuelCount <= 8)
                    {
                        int result = planet.powerSystem.genPool[objectIndex].PickFuelFrom(filter, out inc2);
                        inc = (byte)inc2;
                        return result;
                    }
                }
            }
            return 0;
        }

        return 0;
    }

    public static int InsertInto(PlanetFactory planet,
                                 OptimizedPlanet optimizedPlanet,
                                 ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                                 ref readonly InserterConnections inserterConnections,
                                 int[]? entityNeeds,
                                 int offset,
                                 int itemId,
                                 byte itemCount,
                                 byte itemInc,
                                 out byte remainInc)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = inserterConnections.InsertInto;
        int objectIndex = typedObjectIndex.Index;
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            if (planet.cargoTraffic.TryInsertItem(objectIndex, offset, itemId, itemCount, itemInc))
            {
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = (AssemblerState)optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State;
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveInputMissing)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactiveInsertInto;
                return 0;
            }

            if (entityNeeds == null)
            {
                throw new InvalidOperationException($"Array from {nameof(entityNeeds)} should only be null if assembler is inactive which the above if statement should have caught.");
            }
            OptimizedAssembler reference = optimizedPlanet._optimizedAssemblers[objectIndex];
            AssemblerRecipe assemblerRecipe = optimizedPlanet._assemblerRecipes[reference.assemblerRecipeIndex];
            int[] requires = assemblerRecipe.Requires;
            int num = requires.Length;
            if (0 < num && requires[0] == itemId)
            {
                Interlocked.Add(ref reference.served[0], itemCount);
                Interlocked.Add(ref reference.incServed[0], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1] == itemId)
            {
                Interlocked.Add(ref reference.served[1], itemCount);
                Interlocked.Add(ref reference.incServed[1], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2] == itemId)
            {
                Interlocked.Add(ref reference.served[2], itemCount);
                Interlocked.Add(ref reference.incServed[2], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3] == itemId)
            {
                Interlocked.Add(ref reference.served[3], itemCount);
                Interlocked.Add(ref reference.incServed[3], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4] == itemId)
            {
                Interlocked.Add(ref reference.served[4], itemCount);
                Interlocked.Add(ref reference.incServed[4], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5] == itemId)
            {
                Interlocked.Add(ref reference.served[5], itemCount);
                Interlocked.Add(ref reference.incServed[5], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.ejectorPool[objectIndex].bulletId == itemId)
            {
                Interlocked.Add(ref planet.factorySystem.ejectorPool[objectIndex].bulletCount, itemCount);
                Interlocked.Add(ref planet.factorySystem.ejectorPool[objectIndex].bulletInc, itemInc);
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.siloPool[objectIndex].bulletId == itemId)
            {
                Interlocked.Add(ref planet.factorySystem.siloPool[objectIndex].bulletCount, itemCount);
                Interlocked.Add(ref planet.factorySystem.siloPool[objectIndex].bulletInc, itemInc);
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ProducingLab)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            ref readonly OptimizedProducingLab reference2 = ref optimizedPlanet._optimizedProducingLabs[objectIndex];
            ProducingLabRecipe producingLabRecipe = optimizedPlanet._producingLabRecipes[reference2.producingLabRecipeIndex];
            int[] requires2 = producingLabRecipe.Requires;
            int[] served = reference2.served;
            int[] incServed = reference2.incServed;
            if (requires2 == null)
            {
                return 0;
            }
            int num3 = requires2.Length;
            for (int i = 0; i < num3; i++)
            {
                if (requires2[i] == itemId)
                {
                    Interlocked.Add(ref served[i], itemCount);
                    Interlocked.Add(ref incServed[i], itemInc);
                    remainInc = 0;
                    return itemCount;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ResearchingLab)
        {
            if (entityNeeds == null)
            {
                return 0;
            }

            ref readonly OptimizedResearchingLab lab = ref optimizedPlanet._optimizedResearchingLabs[objectIndex];
            int[] matrixServed = lab.matrixServed;
            int[] matrixIncServed = lab.matrixIncServed;
            if (matrixServed == null)
            {
                return 0;
            }
            int num2 = itemId - 6001;
            if (num2 >= 0 && num2 < 6)
            {
                Interlocked.Add(ref matrixServed[num2], 3600 * itemCount);
                Interlocked.Add(ref matrixIncServed[num2], 3600 * itemInc);
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            while (storageComponent != null)
            {
                lock (planet.entityMutexs[storageComponent.entityId])
                {
                    if (storageComponent.lastFullItem != itemId)
                    {
                        int num4 = 0;
                        num4 = ((planet.entityPool[storageComponent.entityId].battleBaseId != 0) ? storageComponent.AddItemFilteredBanOnly(itemId, itemCount, itemInc, out var remainInc2) : storageComponent.AddItem(itemId, itemCount, itemInc, out remainInc2, useBan: true));
                        remainInc = (byte)remainInc2;
                        if (num4 == itemCount)
                        {
                            storageComponent.lastFullItem = -1;
                        }
                        else
                        {
                            storageComponent.lastFullItem = itemId;
                        }
                        if (num4 != 0 || storageComponent.nextStorage == null)
                        {
                            return num4;
                        }
                    }
                    storageComponent = storageComponent.nextStorage;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
            {
                lock (planet.entityMutexs[stationComponent.entityId])
                {
                    if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
                    {
                        stationComponent.warperCount += itemCount;
                        remainInc = 0;
                        return itemCount;
                    }
                }
            }
            StationStore[] storage = stationComponent.storage;
            for (int j = 0; j < entityNeeds.Length && j < storage.Length; j++)
            {
                if (entityNeeds[j] == itemId && storage[j].itemId == itemId)
                {
                    Interlocked.Add(ref storage[j].count, itemCount);
                    Interlocked.Add(ref storage[j].inc, itemInc);
                    remainInc = 0;
                    return itemCount;
                }
            }

            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            PowerGeneratorComponent[] genPool = planet.powerSystem.genPool;
            ref PowerGeneratorComponent powerGenerator = ref genPool[objectIndex];
            lock (planet.entityMutexs[powerGenerator.entityId])
            {
                if (itemId == powerGenerator.fuelId)
                {
                    if (powerGenerator.fuelCount < 10)
                    {
                        ref short fuelCount = ref powerGenerator.fuelCount;
                        fuelCount += itemCount;
                        ref short fuelInc = ref powerGenerator.fuelInc;
                        fuelInc += itemInc;
                        remainInc = 0;
                        return itemCount;
                    }
                    return 0;
                }
                if (powerGenerator.fuelId == 0)
                {
                    int[] array = ItemProto.fuelNeeds[powerGenerator.fuelMask];
                    if (array == null || array.Length == 0)
                    {
                        return 0;
                    }
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (array[k] == itemId)
                        {
                            powerGenerator.SetNewFuel(itemId, itemCount, itemInc);
                            remainInc = 0;
                            return itemCount;
                        }
                    }
                    return 0;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Splitter)
        {
            switch (offset)
            {
                case 0:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltA, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 1:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltB, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 2:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltC, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 3:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltD, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
            }
            return 0;
        }

        return 0;
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
            for (int i = _start; i < _end; i++)
            {
                if (factorySystem.minerPool[i].id != i)
                {
                    continue;
                }
                int entityId = factorySystem.minerPool[i].entityId;
                float num6 = networkServes[_minerNetworkIds[i]];
                uint num7 = factorySystem.minerPool[i].InternalUpdate(planet, veinPool, num6, (factorySystem.minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
                if (isActive)
                {
                    int stationId = entityPool[entityId].stationId;
                    int num8 = (int)Mathf.Floor(entityAnimPool[entityId].time / 10f);
                    entityAnimPool[entityId].time = entityAnimPool[entityId].time % 10f;
                    entityAnimPool[entityId].Step(num7, num * num6);
                    entityAnimPool[entityId].power = num6;
                    if (stationId > 0)
                    {
                        if (factorySystem.minerPool[i].veinCount > 0)
                        {
                            EVeinType veinTypeByItemId = LDB.veins.GetVeinTypeByItemId(veinPool[factorySystem.minerPool[i].veins[0]].productId);
                            entityAnimPool[entityId].state += (uint)((int)veinTypeByItemId * 100);
                        }
                        entityAnimPool[entityId].power += 10f;
                        entityAnimPool[entityId].power += factorySystem.minerPool[i].speed / 10 * 10;
                        if (num7 == 1)
                        {
                            num8 = 3000;
                        }
                        else
                        {
                            num8 -= (int)(num * 1000f);
                            if (num8 < 0)
                            {
                                num8 = 0;
                            }
                        }
                        entityAnimPool[entityId].time += num8 * 10;
                    }
                    if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                    {
                        entitySignPool[entityId].signType = ((factorySystem.minerPool[i].minimumVeinAmount < 1000) ? 7u : 0u);
                    }
                }
                if (flag2 && factorySystem.minerPool[i].type == EMinerType.Vein)
                {
                    if ((long)i % 30L == time % 30)
                    {
                        factorySystem.minerPool[i].GetTotalVeinAmount(veinPool);
                    }
                    if (isActive)
                    {
                        entitySignPool[entityId].count0 = factorySystem.minerPool[i].totalVeinAmount;
                    }
                }
                else
                {
                    if (isActive)
                    {
                        entitySignPool[entityId].count0 = 0f;
                    }
                }
            }
        }

        _assemblerExecutor.GameTick(planet, this, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

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
    public static bool ParallelGameTickBeforePower(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (isActive)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[__instance.factory];
        optimizedPlanet._optimizedPowerSystem.ParallelGameTickBeforePower(__instance.factory, optimizedPlanet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

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

    public TypedObjectIndex GetAsGranularTypedObjectIndex(int index, PlanetFactory planet)
    {
        ref readonly EntityData entity = ref planet.entityPool[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            if (!_assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
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

[StructLayout(LayoutKind.Auto)]
internal struct ProducingLabRecipe : IEqualityComparer<ProducingLabRecipe>
{
    public readonly int RecipeId;
    public readonly int TimeSpend;
    public readonly int ExtraTimeSpend;
    public readonly int Speed;
    public readonly bool Productive;
    public readonly int[] Requires;
    public readonly int[] RequireCounts;
    public readonly int[] Products;
    public readonly int[] ProductCounts;

    public ProducingLabRecipe(ref readonly LabComponent lab)
    {
        RecipeId = lab.recipeId;
        TimeSpend = lab.timeSpend;
        ExtraTimeSpend = lab.extraTimeSpend;
        Speed = lab.speed;
        Productive = lab.productive;
        Requires = lab.requires;
        RequireCounts = lab.requireCounts;
        Products = lab.products;
        ProductCounts = lab.productCounts;
    }

    public bool Equals(ProducingLabRecipe x, ProducingLabRecipe y)
    {
        return x.RecipeId == y.RecipeId &&
               x.TimeSpend == y.TimeSpend &&
               x.ExtraTimeSpend == y.ExtraTimeSpend &&
               x.Speed == y.Speed &&
               x.Productive == y.Productive &&
               x.Requires.SequenceEqual(y.Requires) &&
               x.RequireCounts.SequenceEqual(y.RequireCounts) &&
               x.Products.SequenceEqual(y.Products) &&
               x.ProductCounts.SequenceEqual(y.ProductCounts);
    }

    public int GetHashCode(ProducingLabRecipe obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(RecipeId);
        hashCode.Add(TimeSpend);
        hashCode.Add(ExtraTimeSpend);
        hashCode.Add(Speed);
        hashCode.Add(Productive);
        for (int i = 0; i < Requires.Length; i++)
        {
            hashCode.Add(Requires[i]);
        }
        for (int i = 0; i < RequireCounts.Length; i++)
        {
            hashCode.Add(RequireCounts[i]);
        }
        for (int i = 0; i < Products.Length; i++)
        {
            hashCode.Add(Products[i]);
        }
        for (int i = 0; i < ProductCounts.Length; i++)
        {
            hashCode.Add(ProductCounts[i]);
        }

        return hashCode.ToHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is not ProducingLabRecipe other)
        {
            return false;
        }

        return Equals(this, other);
    }

    public override int GetHashCode()
    {
        return GetHashCode(this);
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedProducingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly int producingLabRecipeIndex;
    public readonly bool forceAccMode;
    public readonly int[] served;
    public readonly int[] incServed;
    public readonly int[] needs;
    public readonly int[] produced;
    public readonly int nextLabIndex;
    public bool replicating;
    public bool incUsed;
    public int time;
    public int extraTime;
    public int extraSpeed;
    public int extraPowerRatio;
    public int speedOverride;

    public OptimizedProducingLab(int producingLabRecipeIndex,
                                 int? nextLabIndex,
                                 ref readonly LabComponent lab)
    {
        this.producingLabRecipeIndex = producingLabRecipeIndex;
        forceAccMode = lab.forceAccMode;
        served = lab.served;
        incServed = lab.incServed;
        needs = lab.needs;
        produced = lab.produced;
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        replicating = lab.replicating;
        incUsed = lab.incUsed;
        time = lab.time;
        extraTime = lab.extraTime;
        extraSpeed = lab.extraSpeed;
        extraPowerRatio = lab.extraPowerRatio;
        speedOverride = lab.speedOverride;
    }

    public OptimizedProducingLab(int nextLabIndex,
                                 ref readonly OptimizedProducingLab lab)
    {
        producingLabRecipeIndex = lab.producingLabRecipeIndex;
        forceAccMode = lab.forceAccMode;
        served = lab.served;
        incServed = lab.incServed;
        needs = lab.needs;
        produced = lab.produced;
        this.nextLabIndex = nextLabIndex;
        replicating = lab.replicating;
        incUsed = lab.incUsed;
        time = lab.time;
        extraTime = lab.extraTime;
        extraSpeed = lab.extraSpeed;
        extraPowerRatio = lab.extraPowerRatio;
        speedOverride = lab.speedOverride;
    }

    public void UpdateNeedsAssemble(ref readonly ProducingLabRecipe producingLabRecipe)
    {
        int num = served.Length;
        int num2 = ((producingLabRecipe.TimeSpend > 5400000) ? 6 : (3 * ((speedOverride + 5001) / 10000) + 3));
        needs[0] = ((0 < num && served[0] < num2) ? producingLabRecipe.Requires[0] : 0);
        needs[1] = ((1 < num && served[1] < num2) ? producingLabRecipe.Requires[1] : 0);
        needs[2] = ((2 < num && served[2] < num2) ? producingLabRecipe.Requires[2] : 0);
        needs[3] = ((3 < num && served[3] < num2) ? producingLabRecipe.Requires[3] : 0);
        needs[4] = ((4 < num && served[4] < num2) ? producingLabRecipe.Requires[4] : 0);
        needs[5] = ((5 < num && served[5] < num2) ? producingLabRecipe.Requires[5] : 0);
    }

    public uint InternalUpdateAssemble(float power,
                                       int[] productRegister,
                                       int[] consumeRegister,
                                       ref readonly ProducingLabRecipe producingLabRecipe)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        if (extraTime >= producingLabRecipe.ExtraTimeSpend)
        {
            int num = producingLabRecipe.Products.Length;
            if (num == 1)
            {
                produced[0] += producingLabRecipe.ProductCounts[0];
                lock (productRegister)
                {
                    productRegister[producingLabRecipe.Products[0]] += producingLabRecipe.ProductCounts[0];
                }
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    produced[i] += producingLabRecipe.ProductCounts[i];
                    lock (productRegister)
                    {
                        productRegister[producingLabRecipe.Products[i]] += producingLabRecipe.ProductCounts[i];
                    }
                }
            }
            extraTime -= producingLabRecipe.ExtraTimeSpend;
        }
        if (time >= producingLabRecipe.TimeSpend)
        {
            replicating = false;
            int num2 = producingLabRecipe.Products.Length;
            if (num2 == 1)
            {
                if (produced[0] + producingLabRecipe.ProductCounts[0] > 10 * ((speedOverride + 9999) / 10000))
                {
                    return 0u;
                }
                produced[0] += producingLabRecipe.ProductCounts[0];
                lock (productRegister)
                {
                    productRegister[producingLabRecipe.Products[0]] += producingLabRecipe.ProductCounts[0];
                }
            }
            else
            {
                for (int j = 0; j < num2; j++)
                {
                    if (produced[j] + producingLabRecipe.ProductCounts[j] > 10 * ((speedOverride + 9999) / 10000))
                    {
                        return 0u;
                    }
                }
                for (int k = 0; k < num2; k++)
                {
                    produced[k] += producingLabRecipe.ProductCounts[k];
                    lock (productRegister)
                    {
                        productRegister[producingLabRecipe.Products[k]] += producingLabRecipe.ProductCounts[k];
                    }
                }
            }
            extraSpeed = 0;
            speedOverride = producingLabRecipe.Speed;
            extraPowerRatio = 0;
            time -= producingLabRecipe.TimeSpend;
        }
        if (!replicating)
        {
            int num3 = producingLabRecipe.RequireCounts.Length;
            for (int l = 0; l < num3; l++)
            {
                if (incServed[l] <= 0)
                {
                    incServed[l] = 0;
                }
                if (served[l] < producingLabRecipe.RequireCounts[l] || served[l] == 0)
                {
                    time = 0;
                    return 0u;
                }
            }
            int num4 = ((num3 > 0) ? 10 : 0);
            for (int m = 0; m < num3; m++)
            {
                int num5 = split_inc_level(ref served[m], ref incServed[m], producingLabRecipe.RequireCounts[m]);
                num4 = ((num4 < num5) ? num4 : num5);
                if (!incUsed)
                {
                    incUsed = num5 > 0;
                }
                if (served[m] == 0)
                {
                    incServed[m] = 0;
                }
                lock (consumeRegister)
                {
                    consumeRegister[producingLabRecipe.Requires[m]] += producingLabRecipe.RequireCounts[m];
                }
            }
            if (num4 < 0)
            {
                num4 = 0;
            }
            if (producingLabRecipe.Productive && !forceAccMode)
            {
                extraSpeed = (int)((double)producingLabRecipe.Speed * Cargo.incTableMilli[num4] * 10.0 + 0.1);
                speedOverride = producingLabRecipe.Speed;
                extraPowerRatio = Cargo.powerTable[num4];
            }
            else
            {
                extraSpeed = 0;
                speedOverride = (int)((double)producingLabRecipe.Speed * (1.0 + Cargo.accTableMilli[num4]) + 0.1);
                extraPowerRatio = Cargo.powerTable[num4];
            }
            replicating = true;
        }
        if (replicating && time < producingLabRecipe.TimeSpend && extraTime < producingLabRecipe.ExtraTimeSpend)
        {
            time += (int)(power * (float)speedOverride);
            extraTime += (int)(power * (float)extraSpeed);
        }
        if (!replicating)
        {
            return 0u;
        }
        return (uint)(producingLabRecipe.Products[0] - LabComponent.matrixIds[0] + 1);
    }

    public void UpdateOutputToNext(OptimizedProducingLab[] labPool,
                                   ref readonly ProducingLabRecipe producingLabRecipe)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        // This should never be possible. All labs in a column always share their settings
        //if (labPool[nextLabIndex].needs == null || recipeId != labPool[nextLabIndex].recipeId || techId != labPool[nextLabIndex].techId)
        //{
        //    return;
        //}
        if (served != null && labPool[nextLabIndex].served != null)
        {
            int[] obj2 = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? served : labPool[nextLabIndex].served);
            int[] array2 = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? labPool[nextLabIndex].served : served);
            lock (obj2)
            {
                lock (array2)
                {
                    int num13 = served.Length;
                    int num14 = ((producingLabRecipe.TimeSpend > 5400000) ? 1 : (1 + speedOverride / 20000));
                    for (int i = 0; i < num13; i++)
                    {
                        if (labPool[nextLabIndex].needs[i] == producingLabRecipe.Requires[i] && served[i] >= producingLabRecipe.RequireCounts[i] + num14)
                        {
                            int num15 = served[i] - producingLabRecipe.RequireCounts[i] - num14;
                            if (num15 > 5)
                            {
                                num15 = 5;
                            }
                            int num16 = num15 * incServed[i] / served[i];
                            served[i] -= num15;
                            incServed[i] -= num16;
                            labPool[nextLabIndex].served[i] += num15;
                            labPool[nextLabIndex].incServed[i] += num16;
                        }
                    }
                }
            }
        }

        int[] obj3 = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? produced : labPool[nextLabIndex].produced);
        int[] array3 = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? labPool[nextLabIndex].produced : produced);
        lock (obj3)
        {
            lock (array3)
            {
                int num17 = 10 * ((speedOverride + 9999) / 10000) - 2;
                if (produced[0] < num17 && labPool[nextLabIndex].produced[0] > 0)
                {
                    int num18 = ((num17 - produced[0] < labPool[nextLabIndex].produced[0]) ? (num17 - produced[0]) : labPool[nextLabIndex].produced[0]);
                    produced[0] += num18;
                    labPool[nextLabIndex].produced[0] -= num18;
                }
            }
        }
    }

    private int split_inc_level(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        m -= ((num2 > 0) ? (num * p + num2) : (num * p));
        return num;
    }
}

internal sealed class ProducingLabExecutor
{
    public NetworkIdAndState<LabState>[] _networkIdAndStates;
    public OptimizedProducingLab[] _optimizedLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public int[] _entityIds;
    public Dictionary<int, int> _labIdToOptimizedLabIndex;

    public void GameTickLabProduceMode(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, _optimizedLabs.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }

        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        for (int i = _start; i < _end; i++)
        {
            NetworkIdAndState<LabState> networkIdAndState = _networkIdAndStates[i];

            ref OptimizedProducingLab lab = ref optimizedLabs[i];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
            lab.UpdateNeedsAssemble(in producingLabRecipe);

            float power = networkServes[networkIdAndState.Index];
            lab.InternalUpdateAssemble(power,
                                       productRegister,
                                       consumeRegister,
                                       in producingLabRecipe);
        }
    }

    public void GameTickLabOutputToNext(long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        int num = 0;
        int num2 = 0;
        for (int i = (int)(GameMain.gameTick % 5); i < _optimizedLabs.Length; i += 5)
        {
            if (num == _curThreadIdx)
            {
                ref OptimizedProducingLab lab = ref optimizedLabs[i];
                ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
                lab.UpdateOutputToNext(optimizedLabs, in producingLabRecipe);
            }
            num2++;
            if (num2 >= _minimumMissionCnt)
            {
                num2 = 0;
                num++;
                num %= _usedThreadCnt;
            }
        }
    }

    public void Initialize(PlanetFactory planet)
    {
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedProducingLab> optimizedLabs = [];
        Dictionary<ProducingLabRecipe, int> producingLabRecipeToRecipeIndex = [];
        List<ProducingLabRecipe> producingLabRecipes = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];

        for (int i = 0; i < planet.factorySystem.labCursor; i++)
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            if (lab.id != i)
            {
                continue;
            }

            if (lab.researchMode)
            {
                continue;
            }

            if (lab.recipeId == 0)
            {
                continue;
            }

            int? nextLabIndex = null;
            if (planet.factorySystem.labPool[lab.nextLabId].id != 0 &&
                planet.factorySystem.labPool[lab.nextLabId].id == lab.nextLabId)
            {
                nextLabIndex = lab.nextLabId;
            }

            var producingLabRecipe = new ProducingLabRecipe(in lab);
            if (!producingLabRecipeToRecipeIndex.TryGetValue(producingLabRecipe, out int producingLabRecipeIndex))
            {
                producingLabRecipeToRecipeIndex.Add(producingLabRecipe, producingLabRecipes.Count);
                producingLabRecipeIndex = producingLabRecipes.Count;
                producingLabRecipes.Add(producingLabRecipe);
            }

            labIdToOptimizedLabIndex.Add(i, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedProducingLab(producingLabRecipeIndex, nextLabIndex, ref lab));
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, planet.powerSystem.consumerPool[lab.pcId].networkId));
            entityIds.Add(lab.entityId);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            planet.entityNeeds[lab.entityId] = lab.needs;
        }

        for (int i = 0; i < optimizedLabs.Count; i++)
        {
            OptimizedProducingLab lab = optimizedLabs[i];
            if (lab.nextLabIndex == OptimizedProducingLab.NO_NEXT_LAB)
            {
                continue;
            }

            if (!labIdToOptimizedLabIndex.TryGetValue(lab.nextLabIndex, out int nextOptimizedLabIndex))
            {
                throw new InvalidOperationException("Next lab index was not part of the converted research labs.");
            }

            optimizedLabs[i] = new OptimizedProducingLab(nextOptimizedLabIndex, ref lab);
        }

        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _producingLabRecipes = producingLabRecipes.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedResearchingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly int[] needs;
    public readonly int[] matrixServed;
    public readonly int[] matrixIncServed;
    public readonly int nextLabIndex;
    public bool incUsed;
    public int hashBytes;
    public int extraHashBytes;
    public int extraPowerRatio;

    public OptimizedResearchingLab(int? nextLabIndex,
                                   ref readonly LabComponent lab)
    {
        needs = lab.needs;
        matrixServed = lab.matrixServed;
        matrixIncServed = lab.matrixIncServed;
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
        extraPowerRatio = lab.extraPowerRatio;
    }

    public OptimizedResearchingLab(int nextLabIndex,
                                   ref readonly OptimizedResearchingLab lab)
    {
        needs = lab.needs;
        matrixServed = lab.matrixServed;
        matrixIncServed = lab.matrixIncServed;
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
        extraPowerRatio = lab.extraPowerRatio;
    }

    public void SetFunction(int entityId, int techId, int[] matrixPoints, SignData[] signPool)
    {
        hashBytes = 0;
        extraHashBytes = 0;
        extraPowerRatio = 0;
        incUsed = false;
        Array.Copy(LabComponent.matrixIds, needs, LabComponent.matrixIds.Length);
        signPool[entityId].iconId0 = (uint)techId;
        signPool[entityId].iconType = ((techId != 0) ? 3u : 0u);
    }

    public void UpdateNeedsResearch()
    {
        needs[0] = ((matrixServed[0] < 36000) ? 6001 : 0);
        needs[1] = ((matrixServed[1] < 36000) ? 6002 : 0);
        needs[2] = ((matrixServed[2] < 36000) ? 6003 : 0);
        needs[3] = ((matrixServed[3] < 36000) ? 6004 : 0);
        needs[4] = ((matrixServed[4] < 36000) ? 6005 : 0);
        needs[5] = ((matrixServed[5] < 36000) ? 6006 : 0);
    }

    public uint InternalUpdateResearch(float power,
                                       float research_speed,
                                       int techId,
                                       int[] matrixPoints,
                                       int[] consumeRegister,
                                       ref TechState ts,
                                       ref int techHashedThisFrame,
                                       ref long uMatrixPoint,
                                       ref long hashRegister)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        int num = (int)(research_speed + 2f);
        int num2 = 0;
        if (matrixPoints[0] > 0)
        {
            num2 = matrixServed[0] / matrixPoints[0];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        if (matrixPoints[1] > 0)
        {
            num2 = matrixServed[1] / matrixPoints[1];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        if (matrixPoints[2] > 0)
        {
            num2 = matrixServed[2] / matrixPoints[2];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        if (matrixPoints[3] > 0)
        {
            num2 = matrixServed[3] / matrixPoints[3];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        if (matrixPoints[4] > 0)
        {
            num2 = matrixServed[4] / matrixPoints[4];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        if (matrixPoints[5] > 0)
        {
            num2 = matrixServed[5] / matrixPoints[5];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    return 0u;
                }
            }
        }
        research_speed = ((research_speed < (float)num) ? research_speed : ((float)num));
        int num3 = (int)(power * 10000f * research_speed + 0.5f);
        hashBytes += num3;
        long num4 = hashBytes / 10000;
        hashBytes -= (int)num4 * 10000;
        long num5 = ts.hashNeeded - ts.hashUploaded;
        num4 = ((num4 < num5) ? num4 : num5);
        num4 = ((num4 < num) ? num4 : num);
        int num6 = (int)num4;
        if (num6 > 0)
        {
            int num7 = matrixServed.Length;
            int num8 = ((num7 != 0) ? 10 : 0);
            for (int i = 0; i < num7; i++)
            {
                if (matrixPoints[i] > 0)
                {
                    int num9 = matrixServed[i] / 3600;
                    int num10 = split_inc_level(ref matrixServed[i], ref matrixIncServed[i], matrixPoints[i] * num6);
                    num8 = ((num8 < num10) ? num8 : num10);
                    int num11 = matrixServed[i] / 3600;
                    if (matrixServed[i] <= 0 || matrixIncServed[i] < 0)
                    {
                        matrixIncServed[i] = 0;
                    }
                    int num12 = num9 - num11;
                    if (num12 > 0 && !incUsed)
                    {
                        incUsed = num8 > 0;
                    }
                    consumeRegister[LabComponent.matrixIds[i]] += num12;
                }
            }
            if (num8 < 0)
            {
                num8 = 0;
            }
            long num13 = 0L;
            int num14 = 0;
            int extraSpeed = (int)(10000.0 * Cargo.incTableMilli[num8] * 10.0 + 0.1);
            extraPowerRatio = Cargo.powerTable[num8];
            extraHashBytes += (int)(power * (float)extraSpeed * research_speed + 0.5f);
            num13 = extraHashBytes / 100000;
            extraHashBytes -= (int)num13 * 100000;
            num13 = ((num13 < 0) ? 0 : num13);
            num14 = (int)num13;
            ts.hashUploaded += num4 + num13;
            hashRegister += num4 + num13;
            uMatrixPoint += ts.uPointPerHash * num4;
            techHashedThisFrame += num6 + num14;
            if (ts.hashUploaded >= ts.hashNeeded)
            {
                TechProto techProto = LDB.techs.Select(techId);
                if (ts.curLevel >= ts.maxLevel)
                {
                    ts.curLevel = ts.maxLevel;
                    ts.hashUploaded = ts.hashNeeded;
                    ts.unlocked = true;
                    ts.unlockTick = GameMain.gameTick;
                }
                else
                {
                    ts.curLevel++;
                    ts.hashUploaded = 0L;
                    ts.hashNeeded = techProto.GetHashNeeded(ts.curLevel);
                }
            }
        }
        else
        {
            extraPowerRatio = 0;
        }
        return 1u;
    }

    public void UpdateOutputToNext(OptimizedResearchingLab[] labPool)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        // This should never be possible. All labs in a column always share their settings
        //if (labPool[nextLabId].needs == null || recipeId != labPool[nextLabId].recipeId || techId != labPool[nextLabId].techId)
        //{
        //    return;
        //}

        if (matrixServed != null && labPool[nextLabIndex].matrixServed != null)
        {
            int[] obj = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? matrixServed : labPool[nextLabIndex].matrixServed);
            int[] array = ((nextLabIndex > labPool[nextLabIndex].nextLabIndex) ? labPool[nextLabIndex].matrixServed : matrixServed);
            lock (obj)
            {
                lock (array)
                {
                    if (labPool[nextLabIndex].needs[0] == 6001 && matrixServed[0] >= 7200)
                    {
                        int num = (matrixServed[0] - 7200) / 3600 * 3600;
                        if (num > 36000)
                        {
                            num = 36000;
                        }
                        int num2 = split_inc(ref matrixServed[0], ref matrixIncServed[0], num);
                        labPool[nextLabIndex].matrixIncServed[0] += num2;
                        labPool[nextLabIndex].matrixServed[0] += num;
                    }
                    if (labPool[nextLabIndex].needs[1] == 6002 && matrixServed[1] >= 7200)
                    {
                        int num3 = (matrixServed[1] - 7200) / 3600 * 3600;
                        if (num3 > 36000)
                        {
                            num3 = 36000;
                        }
                        int num4 = split_inc(ref matrixServed[1], ref matrixIncServed[1], num3);
                        labPool[nextLabIndex].matrixIncServed[1] += num4;
                        labPool[nextLabIndex].matrixServed[1] += num3;
                    }
                    if (labPool[nextLabIndex].needs[2] == 6003 && matrixServed[2] >= 7200)
                    {
                        int num5 = (matrixServed[2] - 7200) / 3600 * 3600;
                        if (num5 > 36000)
                        {
                            num5 = 36000;
                        }
                        int num6 = split_inc(ref matrixServed[2], ref matrixIncServed[2], num5);
                        labPool[nextLabIndex].matrixIncServed[2] += num6;
                        labPool[nextLabIndex].matrixServed[2] += num5;
                    }
                    if (labPool[nextLabIndex].needs[3] == 6004 && matrixServed[3] >= 7200)
                    {
                        int num7 = (matrixServed[3] - 7200) / 3600 * 3600;
                        if (num7 > 36000)
                        {
                            num7 = 36000;
                        }
                        int num8 = split_inc(ref matrixServed[3], ref matrixIncServed[3], num7);
                        labPool[nextLabIndex].matrixIncServed[3] += num8;
                        labPool[nextLabIndex].matrixServed[3] += num7;
                    }
                    if (labPool[nextLabIndex].needs[4] == 6005 && matrixServed[4] >= 7200)
                    {
                        int num9 = (matrixServed[4] - 7200) / 3600 * 3600;
                        if (num9 > 36000)
                        {
                            num9 = 36000;
                        }
                        int num10 = split_inc(ref matrixServed[4], ref matrixIncServed[4], num9);
                        labPool[nextLabIndex].matrixIncServed[4] += num10;
                        labPool[nextLabIndex].matrixServed[4] += num9;
                    }
                    if (labPool[nextLabIndex].needs[5] == 6006 && matrixServed[5] >= 7200)
                    {
                        int num11 = (matrixServed[5] - 7200) / 3600 * 3600;
                        if (num11 > 36000)
                        {
                            num11 = 36000;
                        }
                        int num12 = split_inc(ref matrixServed[5], ref matrixIncServed[5], num11);
                        labPool[nextLabIndex].matrixIncServed[5] += num12;
                        labPool[nextLabIndex].matrixServed[5] += num11;
                    }
                }
            }
        }
    }

    private int split_inc(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        num = ((num2 > 0) ? (num * p + num2) : (num * p));
        m -= num;
        return num;
    }

    private int split_inc_level(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        m -= ((num2 > 0) ? (num * p + num2) : (num * p));
        return num;
    }
}

internal sealed class ResearchingLabExecutor
{
    private int[] _matrixPoints;
    public NetworkIdAndState<LabState>[] _networkIdAndStates;
    public OptimizedResearchingLab[] _optimizedLabs;
    public int[] _entityIds;
    public Dictionary<int, int> _labIdToOptimizedLabIndex;

    public void GameTickLabResearchMode(PlanetFactory planet, long time)
    {
        FactorySystem factorySystem = planet.factorySystem;
        GameHistoryData history = GameMain.history;
        GameStatData statistics = GameMain.statistics;
        FactoryProductionStat factoryProductionStat = statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = factoryProductionStat.consumeRegister;
        SignData[] entitySignPool = planet.entitySignPool;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int num = history.currentTech;
        TechProto techProto = LDB.techs.Select(num);
        TechState ts = default(TechState);
        bool flag2 = false;
        float research_speed = history.techSpeed;
        int techHashedThisFrame = statistics.techHashedThisFrame;
        long uMatrixPoint = history.universeMatrixPointUploaded;
        long hashRegister = factoryProductionStat.hashRegister;
        if (num > 0 && techProto != null && techProto.IsLabTech && GameMain.history.techStates.ContainsKey(num))
        {
            ts = history.techStates[num];
            flag2 = true;
        }
        if (!flag2)
        {
            num = 0;
        }
        int num2 = 0;
        if (flag2)
        {
            for (int i = 0; i < techProto.Items.Length; i++)
            {
                int num3 = techProto.Items[i] - LabComponent.matrixIds[0];
                if (num3 >= 0 && num3 < 5)
                {
                    num2 |= 1 << num3;
                }
                else if (num3 == 5)
                {
                    num2 = 32;
                    break;
                }
            }
        }
        if (num2 > 32)
        {
            num2 = 32;
        }
        if (num2 < 0)
        {
            num2 = 0;
        }
        float num4 = (float)LabComponent.techShaderStates[num2] + 0.2f;
        if (factorySystem.researchTechId != num)
        {
            factorySystem.researchTechId = num;

            Array.Clear(_matrixPoints, 0, _matrixPoints.Length);
            if (techProto != null && techProto.IsLabTech)
            {
                for (int i = 0; i < techProto.Items.Length; i++)
                {
                    int num46779 = techProto.Items[i] - LabComponent.matrixIds[0];
                    if (num46779 >= 0 && num46779 < _matrixPoints.Length)
                    {
                        _matrixPoints[num46779] = techProto.ItemPoints[i];
                    }
                }
            }

            int[] entityIds = _entityIds;
            for (int i = 0; i < _optimizedLabs.Length; i++)
            {
                _optimizedLabs[i].SetFunction(entityIds[i], factorySystem.researchTechId, _matrixPoints, entitySignPool);
            }
        }

        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        for (int k = 0; k < optimizedLabs.Length; k++)
        {
            ref NetworkIdAndState<LabState> networkIdAndState = ref networkIdAndStates[k];
            ref OptimizedResearchingLab reference = ref optimizedLabs[k];

            reference.UpdateNeedsResearch();
            if (flag2)
            {
                int curLevel = ts.curLevel;
                float power = networkServes[networkIdAndState.Index];
                reference.InternalUpdateResearch(power,
                                                 research_speed,
                                                 factorySystem.researchTechId,
                                                 _matrixPoints,
                                                 consumeRegister,
                                                 ref ts,
                                                 ref techHashedThisFrame,
                                                 ref uMatrixPoint,
                                                 ref hashRegister);
                if (ts.unlocked)
                {
                    history.techStates[factorySystem.researchTechId] = ts;
                    for (int l = 0; l < techProto.UnlockRecipes.Length; l++)
                    {
                        history.UnlockRecipe(techProto.UnlockRecipes[l]);
                    }
                    for (int m = 0; m < techProto.UnlockFunctions.Length; m++)
                    {
                        history.UnlockTechFunction(techProto.UnlockFunctions[m], techProto.UnlockValues[m], curLevel);
                    }
                    for (int n = 0; n < techProto.AddItems.Length; n++)
                    {
                        history.GainTechAwards(techProto.AddItems[n], techProto.AddItemCounts[n]);
                    }
                    history.NotifyTechUnlock(factorySystem.researchTechId, curLevel);
                    history.DequeueTech();
                    flag2 = false;
                }
                if (ts.curLevel > curLevel)
                {
                    history.techStates[factorySystem.researchTechId] = ts;
                    for (int num6 = 0; num6 < techProto.UnlockFunctions.Length; num6++)
                    {
                        history.UnlockTechFunction(techProto.UnlockFunctions[num6], techProto.UnlockValues[num6], curLevel);
                    }
                    for (int num7 = 0; num7 < techProto.AddItems.Length; num7++)
                    {
                        history.GainTechAwards(techProto.AddItems[num7], techProto.AddItemCounts[num7]);
                    }
                    history.NotifyTechUnlock(factorySystem.researchTechId, curLevel);
                    history.DequeueTech();
                    flag2 = false;
                }
            }
        }

        history.techStates[factorySystem.researchTechId] = ts;
        statistics.techHashedThisFrame = techHashedThisFrame;
        history.universeMatrixPointUploaded = uMatrixPoint;
        factoryProductionStat.hashRegister = hashRegister;
    }

    public void GameTickLabOutputToNext(long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        int num = 0;
        int num2 = 0;
        for (int i = (int)(GameMain.gameTick % 5); i < _optimizedLabs.Length; i += 5)
        {
            if (num == _curThreadIdx)
            {
                optimizedLabs[i].UpdateOutputToNext(optimizedLabs);
            }
            num2++;
            if (num2 >= _minimumMissionCnt)
            {
                num2 = 0;
                num++;
                num %= _usedThreadCnt;
            }
        }
    }

    public void Initialize(PlanetFactory planet)
    {
        int[] matrixPoints = new int[LabComponent.matrixIds.Length];
        bool copiedMatrixPoints = false;
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedResearchingLab> optimizedLabs = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];

        for (int i = 0; i < planet.factorySystem.labCursor; i++)
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            if (lab.id != i)
            {
                continue;
            }

            if (!lab.researchMode)
            {
                continue;
            }

            if (!copiedMatrixPoints && lab.matrixPoints != null)
            {
                Array.Copy(lab.matrixPoints, matrixPoints, matrixPoints.Length);
                copiedMatrixPoints = true;
            }

            int? nextLabIndex = null;
            if (planet.factorySystem.labPool[lab.nextLabId].id != 0 &&
                planet.factorySystem.labPool[lab.nextLabId].id == lab.nextLabId)
            {
                nextLabIndex = lab.nextLabId;
            }

            labIdToOptimizedLabIndex.Add(i, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedResearchingLab(nextLabIndex, ref lab));
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, planet.powerSystem.consumerPool[lab.pcId].networkId));
            entityIds.Add(lab.entityId);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            planet.entityNeeds[lab.entityId] = lab.needs;
        }

        for (int i = 0; i < optimizedLabs.Count; i++)
        {
            OptimizedResearchingLab lab = optimizedLabs[i];
            if (lab.nextLabIndex == OptimizedResearchingLab.NO_NEXT_LAB)
            {
                continue;
            }

            if (!labIdToOptimizedLabIndex.TryGetValue(lab.nextLabIndex, out int nextOptimizedLabIndex))
            {
                throw new InvalidOperationException("Next lab index was not part of the converted research labs.");
            }

            optimizedLabs[i] = new OptimizedResearchingLab(nextOptimizedLabIndex, ref lab);
        }

        _matrixPoints = matrixPoints.ToArray();
        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
    }
}