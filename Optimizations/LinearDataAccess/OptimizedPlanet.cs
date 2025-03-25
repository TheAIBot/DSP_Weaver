﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedEntities = [];

    InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public int[] _assemblerNetworkIds;
    public AssemblerState[] _assemblerStates;
    public OptimizedAssembler[] _optimizedAssemblers;
    public AssemblerRecipe[] _assemblerRecipes;
    public Dictionary<int, int> _assemblerIdToOptimizedIndex;
    public AssemblerExecutor _assemblerExecutor;

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
        InitializeAssemblers(planet);
        InitializeInserters(planet);
        InitializeMiners(planet);
        InitializeEjectors(planet);
        InitializeLabAssemblers(planet);
    }

    private void InitializeInserters(PlanetFactory planet)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>();
        _optimizedBiInserterExecutor.Initialize(planet, this, x => x.bidirectional);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>();
        _optimizedInserterExecutor.Initialize(planet, this, x => !x.bidirectional);
    }

    private void InitializeAssemblers(PlanetFactory planet)
    {
        _assemblerExecutor = new AssemblerExecutor();

        List<int> assemblerNetworkIds = [];
        List<AssemblerState> assemblerStates = [];
        List<OptimizedAssembler> optimizedAssemblers = [];
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
                //WeaverFixes.Logger.LogMessage("");
                //assemblerRecipe.Print();
            }

            assemblerIdToOptimizedIndex.Add(assembler.id, optimizedAssemblers.Count);
            assemblerNetworkIds.Add(planet.powerSystem.consumerPool[assembler.pcId].networkId);
            assemblerStates.Add(assembler.recipeId == 0 ? AssemblerState.InactiveNoRecipeSet : AssemblerState.Active);
            optimizedAssemblers.Add(new OptimizedAssembler(assemblerRecipeIndex,
                                                           assembler.pcId,
                                                           assembler.forceAccMode,
                                                           assembler.speed,
                                                           assembler.served,
                                                           assembler.incServed,
                                                           assembler.needs,
                                                           assembler.produced,
                                                           assembler.replicating,
                                                           assembler.incUsed,
                                                           assembler.speedOverride,
                                                           assembler.time,
                                                           assembler.extraTime,
                                                           assembler.cycleCount,
                                                           assembler.extraCycleCount,
                                                           assembler.extraSpeed,
                                                           assembler.extraPowerRatio));


            // set it here so we don't have to set it in the update loop.
            // Need to remember to update it when the assemblers recipe is changed.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIds = assemblerNetworkIds.ToArray();
        _assemblerStates = assemblerStates.ToArray();
        _assemblerRecipes = assemblerRecipes.ToArray();
        _optimizedAssemblers = optimizedAssemblers.ToArray();
        _assemblerIdToOptimizedIndex = assemblerIdToOptimizedIndex;

        WeaverFixes.Logger.LogMessage($"Assembler Recipes: {_assemblerRecipes.Length}");
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
            OptimizedAssembler reference = optimizedPlanet._optimizedAssemblers[objectIndex];
            AssemblerRecipe assemblerRecipe = optimizedPlanet._assemblerRecipes[reference.assemblerRecipeIndex];
            int[] requires = assemblerRecipe.Requires;
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.ParallelGameTickBeforePower))]
    public static bool ParallelGameTickBeforePower(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        PlanetFactory planet = __instance.planet.factory;
        OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
        EntityData[] entityPool = planet.entityPool;
        StationComponent[] stationPool = planet.transport.stationPool;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            for (int i = _start; i < _end; i++)
            {
                if (__instance.minerPool[i].id == i)
                {
                    int stationId = entityPool[__instance.minerPool[i].entityId].stationId;
                    if (stationId > 0)
                    {
                        StationStore[] array = stationPool[stationId].storage;
                        int count = array[0].count;
                        int max = array[0].max;
                        max = ((max < 2000) ? 2000 : max);
                        float num = (float)count / (float)max;
                        num = ((num > 1f) ? 1f : num);
                        float num2 = -2.45f * num + 2.47f;
                        num2 = ((num2 > 1f) ? 1f : num2);
                        __instance.minerPool[i].speedDamper = num2;
                    }
                    else
                    {
                        float num3 = (float)__instance.minerPool[i].productCount / 50f;
                        num3 = ((num3 > 1f) ? 1f : num3);
                        float num4 = -2.45f * num3 + 2.47f;
                        num4 = ((num4 > 1f) ? 1f : num4);
                        __instance.minerPool[i].speedDamper = num4;
                    }
                    __instance.minerPool[i].SetPCState(consumerPool);
                }
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, optimizedPlanet._optimizedAssemblers.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            OptimizedAssembler[] optimizedAssemblers = optimizedPlanet._optimizedAssemblers;
            for (int j = _start; j < _end; j++)
            {
                optimizedAssemblers[j].SetPCState(consumerPool);
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.fractionatorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int k = _start; k < _end; k++)
            {
                if (__instance.fractionatorPool[k].id == k)
                {
                    __instance.fractionatorPool[k].SetPCState(consumerPool);
                }
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.ejectorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int l = _start; l < _end; l++)
            {
                if (__instance.ejectorPool[l].id == l)
                {
                    __instance.ejectorPool[l].SetPCState(consumerPool);
                }
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.siloCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int m = _start; m < _end; m++)
            {
                if (__instance.siloPool[m].id == m)
                {
                    __instance.siloPool[m].SetPCState(consumerPool);
                }
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.labCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int n = _start; n < _end; n++)
            {
                if (__instance.labPool[n].id == n)
                {
                    __instance.labPool[n].SetPCState(consumerPool);
                }
            }
        }

        optimizedPlanet._optimizedBiInserterExecutor.UpdatePower(planet, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);
        optimizedPlanet._optimizedInserterExecutor.UpdatePower(planet, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    public static bool GameTick(PowerSystem __instance, long time, bool isActive, bool isMultithreadMode = false)
    {
        FactoryProductionStat factoryProductionStat = GameMain.statistics.production.factoryStatPool[__instance.factory.index];
        int[] productRegister = factoryProductionStat.productRegister;
        int[] consumeRegister = factoryProductionStat.consumeRegister;
        long num = 0L;
        long num2 = 0L;
        long num3 = 0L;
        long num4 = 0L;
        long num5 = 0L;
        float num6 = 1f / 60f;
        PlanetData planetData = __instance.factory.planet;
        float windStrength = planetData.windStrength;
        float luminosity = planetData.luminosity;
        Vector3 normalized = planetData.runtimeLocalSunDirection.normalized;
        AnimData[] entityAnimPool = __instance.factory.entityAnimPool;
        SignData[] entitySignPool = __instance.factory.entitySignPool;
        if (__instance.networkServes == null || __instance.networkServes.Length != __instance.netPool.Length)
        {
            __instance.networkServes = new float[__instance.netPool.Length];
        }
        if (__instance.networkGenerates == null || __instance.networkGenerates.Length != __instance.netPool.Length)
        {
            __instance.networkGenerates = new float[__instance.netPool.Length];
        }
        bool useIonLayer = GameMain.history.useIonLayer;
        bool useCata = time % 10 == 0;
        Array.Clear(__instance.currentGeneratorCapacities, 0, __instance.currentGeneratorCapacities.Length);
        Player mainPlayer = GameMain.mainPlayer;
        Vector3 zero = Vector3.zero;
        Vector3 vector = Vector3.zero;
        float num7 = 0f;
        bool flag;
        if (mainPlayer.mecha.coreEnergyCap - mainPlayer.mecha.coreEnergy > 0.0 && mainPlayer.isAlive && mainPlayer.planetId == planetData.id)
        {
            float num8 = __instance.factory.planet.realRadius + 0.2f;
            zero = (isMultithreadMode ? __instance.multithreadPlayerPos : mainPlayer.position);
            float magnitude = zero.magnitude;
            if (magnitude > 0f)
            {
                vector = zero * (num8 / magnitude);
            }
            flag = magnitude > num8 - 30f && magnitude < num8 + 50f;
        }
        else
        {
            flag = false;
        }
        lock (mainPlayer.mecha)
        {
            num7 = Mathf.Pow(Mathf.Clamp01((float)(1.0 - mainPlayer.mecha.coreEnergy / mainPlayer.mecha.coreEnergyCap) * 10f), 0.75f);
        }
        float response = ((__instance.dysonSphere != null) ? __instance.dysonSphere.energyRespCoef : 0f);
        int num9 = (int)((float)Math.Min(Math.Abs(__instance.factory.planet.rotationPeriod), Math.Abs(__instance.factory.planet.orbitalPeriod)) * 60f / 2160f);
        if (num9 < 1)
        {
            num9 = 1;
        }
        else if (num9 > 60)
        {
            num9 = 60;
        }
        if (__instance.factory.planet.singularity == EPlanetSingularity.TidalLocked)
        {
            num9 = 60;
        }
        bool flag2 = time % num9 == 0L || GameMain.onceGameTick <= 2;
        int num10 = (int)(time % 90);
        EntityData[] entityPool = __instance.factory.entityPool;
        for (int i = 1; i < __instance.netCursor; i++)
        {
            PowerNetwork powerNetwork = __instance.netPool[i];
            if (powerNetwork == null || powerNetwork.id != i)
            {
                continue;
            }
            List<int> consumers = powerNetwork.consumers;
            int count = consumers.Count;
            long num11 = 0L;
            for (int j = 0; j < count; j++)
            {
                long requiredEnergy = __instance.consumerPool[consumers[j]].requiredEnergy;
                num11 += requiredEnergy;
                num2 += requiredEnergy;
            }
            foreach (PowerNetworkStructures.Node node in powerNetwork.nodes)
            {
                int id = node.id;
                if (__instance.nodePool[id].id != id || !__instance.nodePool[id].isCharger)
                {
                    continue;
                }
                if (__instance.nodePool[id].coverRadius <= 20f)
                {
                    double num12 = 0.0;
                    if (flag)
                    {
                        float num13 = __instance.nodePool[id].powerPoint.x * 0.988f - vector.x;
                        float num14 = __instance.nodePool[id].powerPoint.y * 0.988f - vector.y;
                        float num15 = __instance.nodePool[id].powerPoint.z * 0.988f - vector.z;
                        float num16 = __instance.nodePool[id].coverRadius;
                        if (num16 < 9f)
                        {
                            num16 += 2.01f;
                        }
                        else if (num16 > 20f)
                        {
                            num16 += 0.5f;
                        }
                        float num17 = num13 * num13 + num14 * num14 + num15 * num15;
                        float num18 = num16 * num16;
                        if (num17 <= num18)
                        {
                            double consumerRatio = powerNetwork.consumerRatio;
                            float num19 = (num18 - num17) / (3f * num16);
                            if (num19 > 1f)
                            {
                                num19 = 1f;
                            }
                            num12 = (double)num7 * consumerRatio * consumerRatio * (double)num19;
                        }
                    }
                    double num20 = (double)__instance.nodePool[id].idleEnergyPerTick * (1.0 - num12) + (double)__instance.nodePool[id].workEnergyPerTick * num12;
                    if (__instance.nodePool[id].requiredEnergy < __instance.nodePool[id].idleEnergyPerTick)
                    {
                        __instance.nodePool[id].requiredEnergy = __instance.nodePool[id].idleEnergyPerTick;
                    }
                    if ((double)__instance.nodePool[id].requiredEnergy < num20 - 0.01)
                    {
                        num20 = num20 * 0.02 + (double)__instance.nodePool[id].requiredEnergy * 0.98;
                        __instance.nodePool[id].requiredEnergy = (int)(num20 + 0.9999);
                    }
                    else if ((double)__instance.nodePool[id].requiredEnergy > num20 + 0.01)
                    {
                        num20 = num20 * 0.2 + (double)__instance.nodePool[id].requiredEnergy * 0.8;
                        __instance.nodePool[id].requiredEnergy = (int)num20;
                    }
                }
                else
                {
                    __instance.nodePool[id].requiredEnergy = __instance.nodePool[id].idleEnergyPerTick;
                }
                long num21 = __instance.nodePool[id].requiredEnergy;
                num11 += num21;
                num2 += num21;
            }
            long num22 = 0L;
            List<int> exchangers = powerNetwork.exchangers;
            int count2 = exchangers.Count;
            long num23 = 0L;
            long num24 = 0L;
            int num25 = 0;
            long num26 = 0L;
            long num27 = 0L;
            bool flag3 = false;
            bool flag4 = false;
            for (int k = 0; k < count2; k++)
            {
                num25 = exchangers[k];
                __instance.excPool[num25].StateUpdate();
                __instance.excPool[num25].BeltUpdate(__instance.factory);
                flag3 = __instance.excPool[num25].state >= 1f;
                flag4 = __instance.excPool[num25].state <= -1f;
                if (!flag3 && !flag4)
                {
                    __instance.excPool[num25].capsCurrentTick = 0L;
                    __instance.excPool[num25].currEnergyPerTick = 0L;
                }
                int entityId = __instance.excPool[num25].entityId;
                float num28 = (__instance.excPool[num25].state + 1f) * entityAnimPool[entityId].working_length * 0.5f;
                if (num28 >= 3.99f)
                {
                    num28 = 3.99f;
                }
                entityAnimPool[entityId].time = num28;
                entityAnimPool[entityId].state = 0u;
                entityAnimPool[entityId].power = (float)__instance.excPool[num25].currPoolEnergy / (float)__instance.excPool[num25].maxPoolEnergy;
                if (flag4)
                {
                    long num29 = __instance.excPool[num25].OutputCaps();
                    num26 += num29;
                    num22 = num26;
                    __instance.currentGeneratorCapacities[__instance.excPool[num25].subId] += num29;
                }
                else if (flag3)
                {
                    num27 += __instance.excPool[num25].InputCaps();
                }
            }
            List<int> generators = powerNetwork.generators;
            int count3 = generators.Count;
            int num30 = 0;
            long num31 = 0L;
            for (int l = 0; l < count3; l++)
            {
                num30 = generators[l];
                if (__instance.genPool[num30].wind)
                {
                    num31 = __instance.genPool[num30].EnergyCap_Wind(windStrength);
                    num22 += num31;
                }
                else if (__instance.genPool[num30].photovoltaic)
                {
                    if (flag2)
                    {
                        num31 = __instance.genPool[num30].EnergyCap_PV(normalized.x, normalized.y, normalized.z, luminosity);
                        num22 += num31;
                    }
                    else
                    {
                        num31 = __instance.genPool[num30].capacityCurrentTick;
                        num22 += num31;
                    }
                }
                else if (__instance.genPool[num30].gamma)
                {
                    num31 = __instance.genPool[num30].EnergyCap_Gamma(response);
                    num22 += num31;
                }
                else if (__instance.genPool[num30].geothermal)
                {
                    num31 = __instance.genPool[num30].EnergyCap_GTH();
                    num22 += num31;
                }
                else
                {
                    num31 = __instance.genPool[num30].EnergyCap_Fuel();
                    num22 += num31;
                    entitySignPool[__instance.genPool[num30].entityId].signType = ((num31 <= 30) ? 8u : 0u);
                }
                __instance.currentGeneratorCapacities[__instance.genPool[num30].subId] += num31;
            }
            num += num22 - num26;
            long num32 = num22 - num11;
            long num33 = 0L;
            if (num32 > 0 && powerNetwork.exportDemandRatio > 0.0)
            {
                if (powerNetwork.exportDemandRatio > 1.0)
                {
                    powerNetwork.exportDemandRatio = 1.0;
                }
                num33 = (long)((double)num32 * powerNetwork.exportDemandRatio + 0.5);
                num32 -= num33;
                num11 += num33;
            }
            powerNetwork.exportDemandRatio = 0.0;
            powerNetwork.energyStored = 0L;
            List<int> accumulators = powerNetwork.accumulators;
            int count4 = accumulators.Count;
            long num34 = 0L;
            long num35 = 0L;
            int num36 = 0;
            if (num32 >= 0)
            {
                for (int m = 0; m < count4; m++)
                {
                    num36 = accumulators[m];
                    __instance.accPool[num36].curPower = 0L;
                    long num37 = __instance.accPool[num36].InputCap();
                    if (num37 > 0)
                    {
                        num37 = ((num37 < num32) ? num37 : num32);
                        __instance.accPool[num36].curEnergy += num37;
                        __instance.accPool[num36].curPower = num37;
                        num32 -= num37;
                        num34 += num37;
                        num4 += num37;
                    }
                    powerNetwork.energyStored += __instance.accPool[num36].curEnergy;
                    int entityId2 = __instance.accPool[num36].entityId;
                    entityAnimPool[entityId2].state = ((__instance.accPool[num36].curEnergy > 0) ? 1u : 0u);
                    entityAnimPool[entityId2].power = (float)((double)__instance.accPool[num36].curEnergy / (double)__instance.accPool[num36].maxEnergy);
                }
            }
            else
            {
                long num38 = -num32;
                for (int n = 0; n < count4; n++)
                {
                    num36 = accumulators[n];
                    __instance.accPool[num36].curPower = 0L;
                    long num39 = __instance.accPool[num36].OutputCap();
                    if (num39 > 0)
                    {
                        num39 = ((num39 < num38) ? num39 : num38);
                        __instance.accPool[num36].curEnergy -= num39;
                        __instance.accPool[num36].curPower = -num39;
                        num38 -= num39;
                        num35 += num39;
                        num3 += num39;
                    }
                    powerNetwork.energyStored += __instance.accPool[num36].curEnergy;
                    int entityId3 = __instance.accPool[num36].entityId;
                    entityAnimPool[entityId3].state = ((__instance.accPool[num36].curEnergy > 0) ? 2u : 0u);
                    entityAnimPool[entityId3].power = (float)((double)__instance.accPool[num36].curEnergy / (double)__instance.accPool[num36].maxEnergy);
                }
            }
            double num40 = ((num32 < num27) ? ((double)num32 / (double)num27) : 1.0);
            for (int num41 = 0; num41 < count2; num41++)
            {
                num25 = exchangers[num41];
                if (__instance.excPool[num25].state >= 1f && num40 >= 0.0)
                {
                    long num42 = (long)(num40 * (double)__instance.excPool[num25].capsCurrentTick + 0.99999);
                    long remaining = ((num32 < num42) ? num32 : num42);
                    long num43 = __instance.excPool[num25].InputUpdate(remaining, entityAnimPool, productRegister, consumeRegister);
                    num32 -= num43;
                    num23 += num43;
                    num4 += num43;
                }
                else
                {
                    __instance.excPool[num25].currEnergyPerTick = 0L;
                }
            }
            long num44 = ((num22 < num11 + num23) ? (num22 + num34 + num23) : (num11 + num34 + num23));
            double num45 = ((num44 < num26) ? ((double)num44 / (double)num26) : 1.0);
            for (int num46 = 0; num46 < count2; num46++)
            {
                num25 = exchangers[num46];
                if (__instance.excPool[num25].state <= -1f)
                {
                    long num47 = (long)(num45 * (double)__instance.excPool[num25].capsCurrentTick + 0.99999);
                    long energyPay = ((num44 < num47) ? num44 : num47);
                    long num48 = __instance.excPool[num25].OutputUpdate(energyPay, entityAnimPool, productRegister, consumeRegister);
                    num24 += num48;
                    num3 += num48;
                    num44 -= num48;
                }
            }
            powerNetwork.energyCapacity = num22 - num26;
            powerNetwork.energyRequired = num11 - num33;
            powerNetwork.energyExport = num33;
            powerNetwork.energyServed = ((num22 + num35 < num11) ? (num22 + num35) : num11);
            powerNetwork.energyAccumulated = num34 - num35;
            powerNetwork.energyExchanged = num23 - num24;
            powerNetwork.energyExchangedInputTotal = num23;
            powerNetwork.energyExchangedOutputTotal = num24;
            if (num33 > 0)
            {
                PlanetATField planetATField = __instance.factory.planetATField;
                planetATField.energy += num33;
                planetATField.atFieldRechargeCurrent = num33 * 60;
            }
            num22 += num35;
            num11 += num34;
            num5 += ((num22 >= num11) ? (num2 + num33) : num22);
            long num49 = ((num24 - num11 > 0) ? (num24 - num11) : 0);
            double num50 = ((num22 >= num11) ? 1.0 : ((double)num22 / (double)num11));
            num11 += num23 - num49;
            num22 -= num24;
            double num51 = ((num22 > num11) ? ((double)num11 / (double)num22) : 1.0);
            powerNetwork.consumerRatio = num50;
            powerNetwork.generaterRatio = num51;
            powerNetwork.energyDischarge = num35 + num24;
            powerNetwork.energyCharge = num34 + num23;
            float num52 = ((num22 > 0 || powerNetwork.energyStored > 0 || num24 > 0) ? ((float)num50) : 0f);
            float num53 = ((num22 > 0 || powerNetwork.energyStored > 0 || num24 > 0) ? ((float)num51) : 0f);
            __instance.networkServes[i] = num52;
            __instance.networkGenerates[i] = num53;
            float num54 = 0f;
            for (int num55 = 0; num55 < count3; num55++)
            {
                num30 = generators[num55];
                long num56 = 0L;
                float speed = 1f;
                bool flag5 = !__instance.genPool[num30].wind && !__instance.genPool[num30].photovoltaic && !__instance.genPool[num30].gamma && !__instance.genPool[num30].geothermal;
                if (flag5)
                {
                    __instance.genPool[num30].currentStrength = ((num44 > 0 && __instance.genPool[num30].capacityCurrentTick > 0) ? 1 : 0);
                }
                if (num44 > 0 && __instance.genPool[num30].productId == 0)
                {
                    long num57 = (long)(num51 * (double)__instance.genPool[num30].capacityCurrentTick + 0.99999);
                    num56 = ((num44 < num57) ? num44 : num57);
                    if (num56 > 0)
                    {
                        num44 -= num56;
                        if (flag5)
                        {
                            __instance.genPool[num30].GenEnergyByFuel(num56, consumeRegister);
                            speed = 2f;
                        }
                    }
                }
                __instance.genPool[num30].generateCurrentTick = num56;
                int entityId4 = __instance.genPool[num30].entityId;
                if (__instance.genPool[num30].wind)
                {
                    speed = 0.7f;
                    entityAnimPool[entityId4].Step2((entityAnimPool[entityId4].power > 0.1f || num56 > 0) ? 1u : 0u, num6, windStrength, speed);
                }
                else if (__instance.genPool[num30].gamma)
                {
                    bool keyFrame = (num30 + num10) % 90 == 0;
                    __instance.genPool[num30].GameTick_Gamma(useIonLayer, useCata, keyFrame, __instance.factory, productRegister, consumeRegister);
                    entityAnimPool[entityId4].time += num6;
                    if (entityAnimPool[entityId4].time > 1f)
                    {
                        entityAnimPool[entityId4].time -= 1f;
                    }
                    entityAnimPool[entityId4].power = (float)((double)__instance.genPool[num30].capacityCurrentTick / (double)__instance.genPool[num30].genEnergyPerTick);
                    entityAnimPool[entityId4].state = (uint)((__instance.genPool[num30].productId > 0) ? 2 : 0) + ((__instance.genPool[num30].catalystPoint > 0) ? 1u : 0u);
                    entityAnimPool[entityId4].working_length = entityAnimPool[entityId4].working_length * 0.99f + ((__instance.genPool[num30].catalystPoint > 0) ? 0.01f : 0f);
                    if (isActive)
                    {
                        if (__instance.genPool[num30].productCount >= 20f)
                        {
                            entitySignPool[entityId4].signType = 6u;
                        }
                        else
                        {
                            entitySignPool[entityId4].signType = 0u;
                        }
                    }
                }
                else if (__instance.genPool[num30].fuelMask > 1)
                {
                    num54 = (float)((double)entityAnimPool[entityId4].power * 0.98 + 0.02 * (double)((num56 > 0) ? 1 : 0));
                    if (num56 > 0 && num54 < 0f)
                    {
                        num54 = 0f;
                    }
                    entityAnimPool[entityId4].Step2((entityAnimPool[entityId4].power > 0.1f || num56 > 0) ? 1u : 0u, num6, num54, speed);
                }
                else if (__instance.genPool[num30].geothermal)
                {
                    float num58 = __instance.genPool[num30].warmup + __instance.genPool[num30].warmupSpeed;
                    __instance.genPool[num30].warmup = ((num58 > 1f) ? 1f : ((num58 < 0f) ? 0f : num58));
                    entityAnimPool[entityId4].state = ((num56 > 0) ? 1u : 0u);
                    entityAnimPool[entityId4].Step(entityAnimPool[entityId4].state, num6, 2f, 0f);
                    entityAnimPool[entityId4].working_length = __instance.genPool[num30].warmup;
                    if (num56 > 0)
                    {
                        if (entityAnimPool[entityId4].power < 1f)
                        {
                            entityAnimPool[entityId4].power += num6 / 6f;
                        }
                    }
                    else if (entityAnimPool[entityId4].power > 0f)
                    {
                        entityAnimPool[entityId4].power -= num6 / 6f;
                    }
                    entityAnimPool[entityId4].prepare_length += (float)Math.PI * num6 / 8f;
                    if (entityAnimPool[entityId4].prepare_length > (float)Math.PI * 2f)
                    {
                        entityAnimPool[entityId4].prepare_length -= (float)Math.PI * 2f;
                    }
                }
                else
                {
                    num54 = (float)((double)entityAnimPool[entityId4].power * 0.98 + 0.02 * (double)num56 / (double)__instance.genPool[num30].genEnergyPerTick);
                    if (num56 > 0 && num54 < 0.2f)
                    {
                        num54 = 0.2f;
                    }
                    entityAnimPool[entityId4].Step2((entityAnimPool[entityId4].power > 0.1f || num56 > 0) ? 1u : 0u, num6, num54, speed);
                }
            }
        }
        lock (factoryProductionStat)
        {
            factoryProductionStat.powerGenRegister = num;
            factoryProductionStat.powerConRegister = num2;
            factoryProductionStat.powerDisRegister = num3;
            factoryProductionStat.powerChaRegister = num4;
            factoryProductionStat.energyConsumption += num5;
        }
        if (isActive)
        {
            for (int num59 = 0; num59 < __instance.netCursor; num59++)
            {
                PowerNetwork powerNetwork2 = __instance.netPool[num59];
                if (powerNetwork2 == null || powerNetwork2.id != num59)
                {
                    continue;
                }
                List<int> consumers2 = powerNetwork2.consumers;
                int count5 = consumers2.Count;
                if (num59 == 0)
                {
                    for (int num60 = 0; num60 < count5; num60++)
                    {
                        entitySignPool[__instance.consumerPool[consumers2[num60]].entityId].signType = 1u;
                    }
                }
                else if (powerNetwork2.consumerRatio < 0.10000000149011612)
                {
                    for (int num61 = 0; num61 < count5; num61++)
                    {
                        entitySignPool[__instance.consumerPool[consumers2[num61]].entityId].signType = 2u;
                    }
                }
                else if (powerNetwork2.consumerRatio < 0.5)
                {
                    for (int num62 = 0; num62 < count5; num62++)
                    {
                        entitySignPool[__instance.consumerPool[consumers2[num62]].entityId].signType = 3u;
                    }
                }
                else
                {
                    for (int num63 = 0; num63 < count5; num63++)
                    {
                        entitySignPool[__instance.consumerPool[consumers2[num63]].entityId].signType = 0u;
                    }
                }
            }
        }
        for (int num64 = 1; num64 < __instance.nodeCursor; num64++)
        {
            if (__instance.nodePool[num64].id != num64)
            {
                continue;
            }
            int entityId5 = __instance.nodePool[num64].entityId;
            int networkId = __instance.nodePool[num64].networkId;
            if (__instance.nodePool[num64].isCharger)
            {
                float num65 = __instance.networkServes[networkId];
                int num66 = __instance.nodePool[num64].requiredEnergy - __instance.nodePool[num64].idleEnergyPerTick;
                if (__instance.nodePool[num64].coverRadius < 20f)
                {
                    entityAnimPool[entityId5].StepPoweredClamped(num65, num6, (num66 <= 0) ? 1u : 2u);
                }
                else
                {
                    entityAnimPool[entityId5].StepPoweredClamped2(num65, num6, (num66 <= 0) ? 1u : 2u);
                }
                if (num66 <= 0 || entityAnimPool[entityId5].state != 2)
                {
                    continue;
                }
                lock (mainPlayer.mecha)
                {
                    num66 = (int)((float)num66 * num65);
                    mainPlayer.mecha.coreEnergy += num66;
                    mainPlayer.mecha.MarkEnergyChange(2, num66);
                    mainPlayer.mecha.AddChargerDevice(entityId5);
                    if (mainPlayer.mecha.coreEnergy > mainPlayer.mecha.coreEnergyCap)
                    {
                        mainPlayer.mecha.coreEnergy = mainPlayer.mecha.coreEnergyCap;
                    }
                }
            }
            else if (entityPool[entityId5].powerGenId == 0 && entityPool[entityId5].powerAccId == 0 && entityPool[entityId5].powerExcId == 0)
            {
                float num67 = __instance.networkServes[networkId];
                entityAnimPool[entityId5].Step2((num67 > 0.1f) ? 1u : 0u, num6, (float)((double)entityAnimPool[entityId5].power * 0.97 + 0.03 * (double)num67), 0.4f);
            }
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public TypedObjectIndex GetAsTypedObjectIndex(int index, EntityData[] entities)
    {
        ref readonly EntityData entity = ref entities[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            if (!_assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
            {
                return new TypedObjectIndex(EntityType.None, entity.assemblerId);
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
