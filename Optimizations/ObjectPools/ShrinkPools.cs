using HarmonyLib;
using System;

namespace Weaver.Optimizations.ObjectPools;

public class ShrinkPools
{
    private static int HasCalculatedData = 0;
    private static bool HasOptimizedPools = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    private static void GameTick_DetectPoolHoles(GameData __instance)
    {
        if (DSPGame.IsMenuDemo)
        {
            return;
        }

        if (HasCalculatedData > 1)
        {
            return;
        }
        HasCalculatedData++;

        int totalSlots = 0;
        int usedSlots = 0;
        int unusedSlots = 0;
        int holes = 0;
        foreach (PlanetFactory planet in __instance.factories)
        {
            if (planet == null)
            {
                continue;
            }

            //GatherPoolData(planet.entityPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.prebuildPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.craftPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.enemyPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.veinPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.vegePool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            //GatherPoolData(planet.ruinPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);

            if (planet.factorySystem != null)
            {
                GatherPoolData(planet.factorySystem.minerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.inserterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.assemblerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.fractionatorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.ejectorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.siloPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factorySystem.labPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            }

            if (planet.factoryStorage != null)
            {
                GatherPoolDataForClass(planet.factoryStorage.storagePool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.factoryStorage.tankPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            }

            if (planet.cargoTraffic != null)
            {
                GatherPoolDataForClass(planet.cargoTraffic.pathPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.cargoTraffic.beltPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.cargoTraffic.splitterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.cargoTraffic.monitorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.cargoTraffic.spraycoaterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.cargoTraffic.pilerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            }

            if (planet.transport != null)
            {
                GatherPoolDataForClass(planet.transport.stationPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolDataForClass(planet.transport.dispenserPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            }

            if (planet.powerSystem != null)
            {
                GatherPoolData(planet.powerSystem.genPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.powerSystem.nodePool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.powerSystem.consumerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.powerSystem.accPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolData(planet.powerSystem.excPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                GatherPoolDataForClass(planet.powerSystem.netPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
            }
        }

        WeaverFixes.Logger.LogMessage($"Total:   {totalSlots:N0}");
        WeaverFixes.Logger.LogMessage($"Used:    {usedSlots:N0}");
        WeaverFixes.Logger.LogMessage($"Unused:  {unusedSlots:N0}");
        WeaverFixes.Logger.LogMessage($"Holes:   {holes:N0}");
        WeaverFixes.Logger.LogMessage($"Used Ratio: {(((float)usedSlots / totalSlots) * 100):N2}%");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    private static void GameTick_OptimizePools(GameData __instance)
    {
        if (DSPGame.IsMenuDemo)
        {
            return;
        }

        if (HasOptimizedPools)
        {
            return;
        }
        HasOptimizedPools = true;

        foreach (PlanetFactory planet in __instance.factories)
        {
            if (planet == null)
            {
                continue;
            }

            if (planet.factorySystem != null)
            {
                //GatherPoolData(planet.factorySystem.minerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.minerPool,
                           x => x.Value.id,
                           planet.factorySystem.SetMinerCapacity,
                           ref planet.factorySystem.minerCursor,
                           ref planet.factorySystem.minerCapacity,
                           ref planet.factorySystem.minerRecycle,
                           ref planet.factorySystem.minerRecycleCursor);

                //GatherPoolData(planet.factorySystem.inserterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.inserterPool,
                           x => x.Value.id,
                           planet.factorySystem.SetInserterCapacity,
                           ref planet.factorySystem.inserterCursor,
                           ref planet.factorySystem.inserterCapacity,
                           ref planet.factorySystem.inserterRecycle,
                           ref planet.factorySystem.inserterRecycleCursor);

                //GatherPoolData(planet.factorySystem.assemblerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.assemblerPool,
                           x => x.Value.id,
                           planet.factorySystem.SetAssemblerCapacity,
                           ref planet.factorySystem.assemblerCursor,
                           ref planet.factorySystem.assemblerCapacity,
                           ref planet.factorySystem.assemblerRecycle,
                           ref planet.factorySystem.assemblerRecycleCursor);

                //GatherPoolData(planet.factorySystem.fractionatorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.fractionatorPool,
                           x => x.Value.id,
                           planet.factorySystem.SetFractionatorCapacity,
                           ref planet.factorySystem.fractionatorCursor,
                           ref planet.factorySystem.fractionatorCapacity,
                           ref planet.factorySystem.fractionatorRecycle,
                           ref planet.factorySystem.fractionatorRecycleCursor);

                //GatherPoolData(planet.factorySystem.ejectorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.ejectorPool,
                           x => x.Value.id,
                           planet.factorySystem.SetEjectorCapacity,
                           ref planet.factorySystem.ejectorCursor,
                           ref planet.factorySystem.ejectorCapacity,
                           ref planet.factorySystem.ejectorRecycle,
                           ref planet.factorySystem.ejectorRecycleCursor);

                //GatherPoolData(planet.factorySystem.siloPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.siloPool,
                           x => x.Value.id,
                           planet.factorySystem.SetSiloCapacity,
                           ref planet.factorySystem.siloCursor,
                           ref planet.factorySystem.siloCapacity,
                           ref planet.factorySystem.siloRecycle,
                           ref planet.factorySystem.siloRecycleCursor);

                //GatherPoolData(planet.factorySystem.labPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factorySystem.labPool,
                           x => x.Value.id,
                           planet.factorySystem.SetLabCapacity,
                           ref planet.factorySystem.labCursor,
                           ref planet.factorySystem.labCapacity,
                           ref planet.factorySystem.labRecycle,
                           ref planet.factorySystem.labRecycleCursor);
            }

            if (planet.factoryStorage != null)
            {
                //GatherPoolDataForClass(planet.factoryStorage.storagePool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePoolForClass(ref planet.factoryStorage.storagePool,
                                   x => x.id,
                                   planet.factoryStorage.SetStorageCapacity,
                                   ref planet.factoryStorage.storageCursor,
                                   ref planet.factoryStorage.storageCapacity,
                                   ref planet.factoryStorage.storageRecycle,
                                   ref planet.factoryStorage.storageRecycleCursor);

                //GatherPoolData(planet.factoryStorage.tankPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.factoryStorage.tankPool,
                           x => x.Value.id,
                           planet.factoryStorage.SetTankCapacity,
                           ref planet.factoryStorage.tankCursor,
                           ref planet.factoryStorage.tankCapacity,
                           ref planet.factoryStorage.tankRecycle,
                           ref planet.factoryStorage.tankRecycleCursor);
            }

            if (planet.cargoTraffic != null)
            {
                //GatherPoolDataForClass(planet.cargoTraffic.pathPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePoolForClass(ref planet.cargoTraffic.pathPool,
                                   x => x.id,
                                   planet.cargoTraffic.SetPathCapacity,
                                   ref planet.cargoTraffic.pathCursor,
                                   ref planet.cargoTraffic.pathCapacity,
                                   ref planet.cargoTraffic.pathRecycle,
                                   ref planet.cargoTraffic.pathRecycleCursor);

                //GatherPoolData(planet.cargoTraffic.beltPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.cargoTraffic.beltPool,
                           x => x.Value.id,
                           planet.cargoTraffic.SetBeltCapacity,
                           ref planet.cargoTraffic.beltCursor,
                           ref planet.cargoTraffic.beltCapacity,
                           ref planet.cargoTraffic.beltRecycle,
                           ref planet.cargoTraffic.beltRecycleCursor);

                //GatherPoolData(planet.cargoTraffic.splitterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.cargoTraffic.splitterPool,
                           x => x.Value.id,
                           planet.cargoTraffic.SetSplitterCapacity,
                           ref planet.cargoTraffic.splitterCursor,
                           ref planet.cargoTraffic.splitterCapacity,
                           ref planet.cargoTraffic.splitterRecycle,
                           ref planet.cargoTraffic.splitterRecycleCursor);

                //GatherPoolData(planet.cargoTraffic.monitorPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.cargoTraffic.monitorPool,
                           x => x.Value.id,
                           planet.cargoTraffic.SetMonitorCapacity,
                           ref planet.cargoTraffic.monitorCursor,
                           ref planet.cargoTraffic.monitorCapacity,
                           ref planet.cargoTraffic.monitorRecycle,
                           ref planet.cargoTraffic.monitorRecycleCursor);

                //GatherPoolData(planet.cargoTraffic.spraycoaterPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.cargoTraffic.spraycoaterPool,
                           x => x.Value.id,
                           planet.cargoTraffic.SetSpraycoaterCapacity,
                           ref planet.cargoTraffic.spraycoaterCursor,
                           ref planet.cargoTraffic.spraycoaterCapacity,
                           ref planet.cargoTraffic.spraycoaterRecycle,
                           ref planet.cargoTraffic.spraycoaterRecycleCursor);

                //GatherPoolData(planet.cargoTraffic.pilerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.cargoTraffic.pilerPool,
                           x => x.Value.id,
                           planet.cargoTraffic.SetPilerCapacity,
                           ref planet.cargoTraffic.pilerCursor,
                           ref planet.cargoTraffic.pilerCapacity,
                           ref planet.cargoTraffic.pilerRecycle,
                           ref planet.cargoTraffic.pilerRecycleCursor);
            }

            if (planet.transport != null)
            {
                //GatherPoolDataForClass(planet.transport.stationPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePoolForClass(ref planet.transport.stationPool,
                                   x => x.id,
                                   planet.transport.SetStationCapacity,
                                   ref planet.transport.stationCursor,
                                   ref planet.transport.stationCapacity,
                                   ref planet.transport.stationRecycle,
                                   ref planet.transport.stationRecycleCursor);

                //GatherPoolDataForClass(planet.transport.dispenserPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePoolForClass(ref planet.transport.dispenserPool,
                                   x => x.id,
                                   planet.transport.SetDispenserCapacity,
                                   ref planet.transport.dispenserCursor,
                                   ref planet.transport.dispenserCapacity,
                                   ref planet.transport.dispenserRecycle,
                                   ref planet.transport.dispenserRecycleCursor);
            }

            if (planet.powerSystem != null)
            {
                //GatherPoolData(planet.powerSystem.genPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.powerSystem.genPool,
                           x => x.Value.id,
                           planet.powerSystem.SetGeneratorCapacity,
                           ref planet.powerSystem.genCursor,
                           ref planet.powerSystem.genCapacity,
                           ref planet.powerSystem.genRecycle,
                           ref planet.powerSystem.genRecycleCursor);

                //GatherPoolData(planet.powerSystem.nodePool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.powerSystem.nodePool,
                           x => x.Value.id,
                           planet.powerSystem.SetNodeCapacity,
                           ref planet.powerSystem.nodeCursor,
                           ref planet.powerSystem.nodeCapacity,
                           ref planet.powerSystem.nodeRecycle,
                           ref planet.powerSystem.nodeRecycleCursor);

                //GatherPoolData(planet.powerSystem.consumerPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.powerSystem.consumerPool,
                           x => x.Value.id,
                           planet.powerSystem.SetConsumerCapacity,
                           ref planet.powerSystem.consumerCursor,
                           ref planet.powerSystem.consumerCapacity,
                           ref planet.powerSystem.consumerRecycle,
                           ref planet.powerSystem.consumerRecycleCursor);

                //GatherPoolData(planet.powerSystem.accPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.powerSystem.accPool,
                           x => x.Value.id,
                           planet.powerSystem.SetAccumulatorCapacity,
                           ref planet.powerSystem.accCursor,
                           ref planet.powerSystem.accCapacity,
                           ref planet.powerSystem.accRecycle,
                           ref planet.powerSystem.accRecycleCursor);

                //GatherPoolData(planet.powerSystem.excPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePool(ref planet.powerSystem.excPool,
                           x => x.Value.id,
                           planet.powerSystem.SetExchangerCapacity,
                           ref planet.powerSystem.excCursor,
                           ref planet.powerSystem.excCapacity,
                           ref planet.powerSystem.excRecycle,
                           ref planet.powerSystem.excRecycleCursor);

                //GatherPoolDataForClass(planet.powerSystem.netPool, x => x.id, ref totalSlots, ref usedSlots, ref unusedSlots, ref holes);
                ResizePoolForClass(ref planet.powerSystem.netPool,
                                   x => x.id,
                                   planet.powerSystem.SetNetworkCapacity,
                                   ref planet.powerSystem.netCursor,
                                   ref planet.powerSystem.netCapacity,
                                   ref planet.powerSystem.netRecycle,
                                   ref planet.powerSystem.netRecycleCursor);
            }
        }

        WeaverFixes.Logger.LogMessage("Optimized pools");
    }

    private static void GatherPoolData<T>(T[] pool, Func<T, int> getId, ref int totalSlots, ref int usedSlots, ref int unusedSlots, ref int holes)
        where T : struct
    {
        totalSlots += pool.Length;
        int lastUsedIndex = 0;
        for (int i = 1; i < pool.Length; i++)
        {
            if (getId(pool[i]) == i)
            {
                usedSlots++;
                lastUsedIndex = i;
            }
            else
            {
                unusedSlots++;
            }
        }

        for (int i = 1; i <= lastUsedIndex; i++)
        {
            if (getId(pool[i]) != i)
            {
                holes++;
            }
        }
    }

    private static void GatherPoolDataForClass<T>(T[] pool, Func<T, int> getId, ref int totalSlots, ref int usedSlots, ref int unusedSlots, ref int holes)
        where T : class
    {
        totalSlots += pool.Length;
        int lastUsedIndex = 0;
        for (int i = 1; i < pool.Length; i++)
        {
            if (pool[i] != null && getId(pool[i]) == i)
            {
                usedSlots++;
                lastUsedIndex = i;
            }
            else
            {
                unusedSlots++;
            }
        }

        for (int i = 1; i <= lastUsedIndex; i++)
        {
            if (pool[i] == null || getId(pool[i]) != i)
            {
                holes++;
            }
        }
    }

    private static void ResizePool<T>(ref T[] pool,
                                      PassStructAsRefFunc<T, int> getId,
                                      Action<int> setCapacity,
                                      ref int cursor,
                                      ref int capacity,
                                      ref int[] recycle,
                                      ref int recycleCursor)
        where T : struct
    {
        int lastUsedIndex;
        for (lastUsedIndex = cursor - 1; lastUsedIndex >= 1; lastUsedIndex--)
        {
            if (getId(new ArrayStructRef<T>(pool, lastUsedIndex)) == lastUsedIndex)
            {
                break;
            }
        }

        cursor = lastUsedIndex + 1;
        setCapacity(cursor);
    }

    private static void ResizePoolForClass<T>(ref T[] pool,
                                              Func<T, int> getId,
                                              Action<int> setCapacity,
                                              ref int cursor,
                                              ref int capacity,
                                              ref int[] recycle,
                                              ref int recycleCursor)
    where T : class
    {
        int lastUsedIndex;
        for (lastUsedIndex = cursor - 1; lastUsedIndex >= 1; lastUsedIndex--)
        {
            if (getId(pool[lastUsedIndex]) == lastUsedIndex)
            {
                break;
            }
        }

        cursor = lastUsedIndex + 1;
        setCapacity(cursor);
    }

    private delegate TReturn PassStructAsRefFunc<TArgument, TReturn>(ArrayStructRef<TArgument> item) where TArgument : struct;

    private struct ArrayStructRef<T>
        where T : struct
    {
        private readonly T[] _array;
        private readonly int _index;

        public readonly ref T Value => ref _array[_index];

        public ArrayStructRef(T[] array, int index)
        {
            _array = array;
            _index = index;
        }
    }


}
