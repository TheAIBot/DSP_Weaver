using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedEntities = [];

    InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    private int[] _assemblerNetworkIds;
    public AssemblerState[] _assemblerStates;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    private NetworkIdAndState<LabState>[] _labNetworkIdAndStates;

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(OptimizedPlanet));
    }

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
        InitializeInserters(planet);
        InitializeAssemblers(planet);
        InitializeMiners(planet);
        InitializeEjectors(planet);
        InitializeLabAssemblers(planet);
    }

    private void InitializeInserters(PlanetFactory planet)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>();
        _optimizedBiInserterExecutor.Initialize(planet, x => x.bidirectional);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>();
        _optimizedInserterExecutor.Initialize(planet, x => !x.bidirectional);
    }

    private void InitializeAssemblers(PlanetFactory planet)
    {
        int[] assemblerNetworkIds = new int[planet.factorySystem.assemblerCursor];
        AssemblerState[] assemblerStates = new AssemblerState[planet.factorySystem.assemblerCursor];

        for (int i = 0; i < planet.factorySystem.assemblerCursor; i++)
        {
            ref readonly AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[i];
            if (assembler.id != i)
            {
                assemblerStates[i] = AssemblerState.InactiveNoAssembler;
                continue;
            }

            assemblerNetworkIds[i] = planet.powerSystem.consumerPool[assembler.pcId].networkId;

            if (assembler.recipeId == 0)
            {
                assemblerStates[i] = AssemblerState.InactiveNoRecipeSet;
            }
            else
            {
                assemblerStates[i] = AssemblerState.Active;
            }

            // set it here so we don't have to set it in the update loop.
            // Need to remember to update it when the assemblers recipe is changed.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIds = assemblerNetworkIds;
        _assemblerStates = assemblerStates;
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
        NetworkIdAndState<LabState>[] labNetworkIdAndStates = new NetworkIdAndState<LabState>[planet.factorySystem.labCursor];

        for (int i = 0; i < planet.factorySystem.labCursor; i++)
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            if (lab.id != i)
            {
                labNetworkIdAndStates[i] = new NetworkIdAndState<LabState>((int)LabState.InactiveNoAssembler, 0);
                continue;
            }

            LabState state = LabState.Active;
            if (lab.recipeId == 0)
            {
                state = LabState.InactiveNoRecipeSet;
            }
            if (lab.researchMode)
            {
                state = state | LabState.ResearchMode;
            }

            labNetworkIdAndStates[i] = new NetworkIdAndState<LabState>((int)state, planet.powerSystem.consumerPool[lab.pcId].networkId);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            planet.entityNeeds[lab.entityId] = lab.needs;
        }

        _labNetworkIdAndStates = labNetworkIdAndStates;
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
            AssemblerState assemblerState = optimizedPlanet._assemblerStates[objectIndex];
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
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
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
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
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
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
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
                                    optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
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
        else if (typedObjectIndex.EntityType == EntityType.Lab)
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
            AssemblerState assemblerState = optimizedPlanet._assemblerStates[objectIndex];
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
            ref AssemblerComponent reference = ref planet.factorySystem.assemblerPool[objectIndex];
            int[] requires = reference.requires;
            int num = requires.Length;
            if (0 < num && requires[0] == itemId)
            {
                Interlocked.Add(ref reference.served[0], itemCount);
                Interlocked.Add(ref reference.incServed[0], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1] == itemId)
            {
                Interlocked.Add(ref reference.served[1], itemCount);
                Interlocked.Add(ref reference.incServed[1], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2] == itemId)
            {
                Interlocked.Add(ref reference.served[2], itemCount);
                Interlocked.Add(ref reference.incServed[2], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3] == itemId)
            {
                Interlocked.Add(ref reference.served[3], itemCount);
                Interlocked.Add(ref reference.incServed[3], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4] == itemId)
            {
                Interlocked.Add(ref reference.served[4], itemCount);
                Interlocked.Add(ref reference.incServed[4], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5] == itemId)
            {
                Interlocked.Add(ref reference.served[5], itemCount);
                Interlocked.Add(ref reference.incServed[5], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
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
        else if (typedObjectIndex.EntityType == EntityType.Lab)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            ref LabComponent reference2 = ref planet.factorySystem.labPool[objectIndex];
            if (reference2.researchMode)
            {
                int[] matrixServed = reference2.matrixServed;
                int[] matrixIncServed = reference2.matrixIncServed;
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
            }
            else
            {
                int[] requires2 = reference2.requires;
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
                        OptimizedPlanet optimizedInserters = _planetToOptimizedEntities[planet];
                        optimizedInserters.GameTick(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
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
                        OptimizedPlanet optimizedInserters = _planetToOptimizedEntities[planet];
                        optimizedInserters.GameTickLabProduceMode(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
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
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.assemblerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int k = _start; k < _end; k++)
            {
                if (_assemblerStates[k] != AssemblerState.Active)
                {
                    continue;
                }

                float power = networkServes[_assemblerNetworkIds[k]];
                factorySystem.assemblerPool[k].UpdateNeeds();
                _assemblerStates[k] = AssemblerInternalUpdate(ref factorySystem.assemblerPool[k], power, productRegister, consumeRegister);
            }
        }
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

    private AssemblerState AssemblerInternalUpdate(ref AssemblerComponent assembler, float power, int[] productRegister, int[] consumeRegister)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return AssemblerState.Active;
        }

        if (assembler.extraTime >= assembler.extraTimeSpend)
        {
            int num = assembler.products.Length;
            if (num == 1)
            {
                assembler.produced[0] += assembler.productCounts[0];
                lock (productRegister)
                {
                    productRegister[assembler.products[0]] += assembler.productCounts[0];
                }
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    assembler.produced[i] += assembler.productCounts[i];
                    lock (productRegister)
                    {
                        productRegister[assembler.products[i]] += assembler.productCounts[i];
                    }
                }
            }
            assembler.extraCycleCount++;
            assembler.extraTime -= assembler.extraTimeSpend;
        }
        if (assembler.time >= assembler.timeSpend)
        {
            assembler.replicating = false;
            if (assembler.products.Length == 1)
            {
                switch (assembler.recipeType)
                {
                    case ERecipeType.Smelt:
                        if (assembler.produced[0] + assembler.productCounts[0] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    case ERecipeType.Assemble:
                        if (assembler.produced[0] > assembler.productCounts[0] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    default:
                        if (assembler.produced[0] > assembler.productCounts[0] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                }
                assembler.produced[0] += assembler.productCounts[0];
                lock (productRegister)
                {
                    productRegister[assembler.products[0]] += assembler.productCounts[0];
                }
            }
            else
            {
                int num2 = assembler.products.Length;
                if (assembler.recipeType == ERecipeType.Refine)
                {
                    for (int j = 0; j < num2; j++)
                    {
                        if (assembler.produced[j] > assembler.productCounts[j] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Particle)
                {
                    for (int k = 0; k < num2; k++)
                    {
                        if (assembler.produced[k] > assembler.productCounts[k] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Chemical)
                {
                    for (int l = 0; l < num2; l++)
                    {
                        if (assembler.produced[l] > assembler.productCounts[l] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Smelt)
                {
                    for (int m = 0; m < num2; m++)
                    {
                        if (assembler.produced[m] + assembler.productCounts[m] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Assemble)
                {
                    for (int n = 0; n < num2; n++)
                    {
                        if (assembler.produced[n] > assembler.productCounts[n] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else
                {
                    for (int num3 = 0; num3 < num2; num3++)
                    {
                        if (assembler.produced[num3] > assembler.productCounts[num3] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                for (int num4 = 0; num4 < num2; num4++)
                {
                    assembler.produced[num4] += assembler.productCounts[num4];
                    lock (productRegister)
                    {
                        productRegister[assembler.products[num4]] += assembler.productCounts[num4];
                    }
                }
            }
            assembler.extraSpeed = 0;
            assembler.speedOverride = assembler.speed;
            assembler.extraPowerRatio = 0;
            assembler.cycleCount++;
            assembler.time -= assembler.timeSpend;
        }
        if (!assembler.replicating)
        {
            int num5 = assembler.requireCounts.Length;
            for (int num6 = 0; num6 < num5; num6++)
            {
                if (assembler.incServed[num6] <= 0)
                {
                    assembler.incServed[num6] = 0;
                }
                if (assembler.served[num6] < assembler.requireCounts[num6] || assembler.served[num6] == 0)
                {
                    assembler.time = 0;
                    return AssemblerState.InactiveInputMissing;
                }
            }
            int num7 = ((num5 > 0) ? 10 : 0);
            for (int num8 = 0; num8 < num5; num8++)
            {
                int num9 = assembler.split_inc_level(ref assembler.served[num8], ref assembler.incServed[num8], assembler.requireCounts[num8]);
                num7 = ((num7 < num9) ? num7 : num9);
                if (!assembler.incUsed)
                {
                    assembler.incUsed = num9 > 0;
                }
                if (assembler.served[num8] == 0)
                {
                    assembler.incServed[num8] = 0;
                }
                lock (consumeRegister)
                {
                    consumeRegister[assembler.requires[num8]] += assembler.requireCounts[num8];
                }
            }
            if (num7 < 0)
            {
                num7 = 0;
            }
            if (assembler.productive && !assembler.forceAccMode)
            {
                assembler.extraSpeed = (int)((double)assembler.speed * Cargo.incTableMilli[num7] * 10.0 + 0.1);
                assembler.speedOverride = assembler.speed;
                assembler.extraPowerRatio = Cargo.powerTable[num7];
            }
            else
            {
                assembler.extraSpeed = 0;
                assembler.speedOverride = (int)((double)assembler.speed * (1.0 + Cargo.accTableMilli[num7]) + 0.1);
                assembler.extraPowerRatio = Cargo.powerTable[num7];
            }
            assembler.replicating = true;
        }
        if (assembler.replicating && assembler.time < assembler.timeSpend && assembler.extraTime < assembler.extraTimeSpend)
        {
            assembler.time += (int)(power * (float)assembler.speedOverride);
            assembler.extraTime += (int)(power * (float)assembler.extraSpeed);
        }
        if (!assembler.replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
            //return 0u;
        }
        return AssemblerState.Active;
    }

    private void GameTickLabProduceMode(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, planet.factorySystem.labCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }

        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        for (int i = _start; i < _end; i++)
        {
            NetworkIdAndState<LabState> networkIdAndState = _labNetworkIdAndStates[i];
            if (((LabState)networkIdAndState.State & LabState.Inactive) == LabState.Inactive)
            {
                continue;
            }
            if (((LabState)networkIdAndState.State & LabState.ResearchMode) == LabState.ResearchMode)
            {
                continue;
            }

            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            lab.UpdateNeedsAssemble();

            float power = networkServes[networkIdAndState.Index];
            lab.InternalUpdateAssemble(power, productRegister, consumeRegister);
        }
    }
    public static TypedObjectIndex GetAsTypedObjectIndex(int index, EntityData[] entities)
    {
        ref readonly EntityData entity = ref entities[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            return new TypedObjectIndex(EntityType.Assembler, entity.assemblerId);
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
            return new TypedObjectIndex(EntityType.Lab, entity.labId);
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
