using System;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Miners;

namespace Weaver.Optimizations.LinearDataAccess.Stations;

/// <summary>
/// This optimized station only optimizes belt interactions.
/// It also handles sandbox mode because why not.
/// Everything else is handled bt the games <see cref="StationComponent"/>.
/// A stations configuration can be updated at any time so all its state is stored
/// inside the <see cref="StationComponent"/> to ensure belt i/o is updated immediately.
/// </summary>
internal readonly struct OptimizedStation
{
    public readonly StationComponent stationComponent;
    private readonly OptimizedCargoPath?[] _cargoPaths;
    private readonly int? _optimizedMinerIndex;

    public OptimizedStation(StationComponent stationComponent,
                            OptimizedCargoPath?[] cargoPaths,
                            int? optimizedMinerIndex)
    {
        this.stationComponent = stationComponent;
        _cargoPaths = cargoPaths;
        _optimizedMinerIndex = optimizedMinerIndex;
    }

    public void UpdateOutputSlots(int maxPilerCount)
    {
        lock (stationComponent.storage)
        {
            int num = ((stationComponent.pilerCount == 0) ? maxPilerCount : stationComponent.pilerCount);
            int num2 = stationComponent.slots.Length;
            int num3 = stationComponent.storage.Length;
            int num4 = -1;
            if (!stationComponent.isVeinCollector)
            {
                for (int i = 0; i < num2; i++)
                {
                    ref SlotData reference = ref stationComponent.slots[i];
                    if (reference.dir == IODir.Output)
                    {
                        if (reference.counter > 0)
                        {
                            reference.counter--;
                        }
                        else
                        {
                            if (_cargoPaths[i] == null)
                            {
                                continue;
                            }
                            OptimizedCargoPath? cargoPath = _cargoPaths[i];
                            if (cargoPath == null || cargoPath.buffer[9] != 0)
                            {
                                continue;
                            }
                            int num5 = reference.storageIdx - 1;
                            int num6 = 0;
                            if (num5 >= 0)
                            {
                                if (num5 < num3)
                                {
                                    num6 = stationComponent.storage[num5].itemId;
                                    if (num6 > 0 && stationComponent.storage[num5].count > 0)
                                    {
                                        int num7 = ((stationComponent.storage[num5].count < num) ? stationComponent.storage[num5].count : num);
                                        int n = stationComponent.storage[num5].count;
                                        int m = stationComponent.storage[num5].inc;
                                        int num8 = split_inc(ref n, ref m, num7);
                                        if (cargoPath.TryInsertItemAtHeadAndFillBlank(num6, (byte)num7, (byte)num8))
                                        {
                                            stationComponent.storage[num5].count = n;
                                            stationComponent.storage[num5].inc = m;
                                            reference.counter = 1;
                                        }
                                    }
                                }
                                else
                                {
                                    num6 = 1210;
                                    if (stationComponent.warperCount > 0 && cargoPath.TryInsertItemAtHeadAndFillBlank(num6, 1, 0))
                                    {
                                        stationComponent.warperCount--;
                                        reference.counter = 1;
                                    }
                                }
                            }
                        }
                    }
                    else if (reference.dir != IODir.Input)
                    {
                        _cargoPaths[i] = null;
                        reference.beltId = 0;
                        reference.counter = 0;
                    }
                }
                return;
            }
            for (int j = 0; j < num; j++)
            {
                for (int k = 0; k < num2; k++)
                {
                    int num10 = (stationComponent.outSlotOffset + k) % num2;
                    ref SlotData reference2 = ref stationComponent.slots[num10];
                    if (reference2.dir == IODir.Output)
                    {
                        if (_cargoPaths[j] == null)
                        {
                            continue;
                        }
                        OptimizedCargoPath? cargoPath2 = _cargoPaths[j];
                        if (cargoPath2 == null)
                        {
                            continue;
                        }
                        int num11 = 0;
                        int num12 = 0;
                        if (num11 >= 0 && num11 < num3)
                        {
                            num12 = stationComponent.storage[num11].itemId;
                            if (num12 > 0 && stationComponent.storage[num11].count > 0)
                            {
                                int num13 = stationComponent.storage[num11].inc / stationComponent.storage[num11].count;
                                if (cargoPath2.TryUpdateItemAtHeadAndFillBlank(num12, num, 1, (byte)num13))
                                {
                                    stationComponent.storage[num11].count--;
                                    stationComponent.storage[num11].inc -= num13;
                                    num4 = (num10 + 1) % num2;
                                }
                            }
                        }
                    }
                    else if (reference2.dir != IODir.Input)
                    {
                        _cargoPaths[j] = null;
                        reference2.beltId = 0;
                        reference2.counter = 0;
                    }
                }
            }
            if (num4 >= 0)
            {
                stationComponent.outSlotOffset = num4;
            }
        }
    }

    public void UpdateInputSlots()
    {
        lock (stationComponent.storage)
        {
            int num = stationComponent.slots.Length;
            _ = stationComponent.storage.Length;
            int num2 = stationComponent.needs[0] + stationComponent.needs[1] + stationComponent.needs[2] + stationComponent.needs[3] + stationComponent.needs[4] + stationComponent.needs[5];
            for (int i = 0; i < num; i++)
            {
                ref SlotData reference = ref stationComponent.slots[i];
                if (reference.dir == IODir.Input)
                {
                    if (reference.counter > 0)
                    {
                        reference.counter--;
                    }
                    else
                    {
                        if (num2 == 0 || _cargoPaths[i] == null)
                        {
                            continue;
                        }
                        OptimizedCargoPath? cargoPath = _cargoPaths[i];
                        if (cargoPath == null)
                        {
                            continue;
                        }
                        int needIdx = -1;
                        byte stack;
                        byte inc;
                        int num3 = cargoPath.TryPickItemAtRear(stationComponent.needs, out needIdx, out stack, out inc);
                        if (needIdx >= 0)
                        {
                            InputItem(num3, needIdx, stack, inc);
                            reference.storageIdx = needIdx + 1;
                            reference.counter = 1;
                        }
                    }
                }
                else if (reference.dir != IODir.Output)
                {
                    _cargoPaths[i] = null;
                    reference.beltId = 0;
                    reference.counter = 0;
                }
            }
        }
    }

    public void UpdateCollection(PlanetFactory factory, float collectSpeedRate, int[] productRegister, ref MiningFlags miningFlags)
    {
        if (stationComponent.collectionPerTick == null)
        {
            return;
        }
        for (int i = 0; i < stationComponent.collectionIds.Length; i++)
        {
            lock (stationComponent.storage)
            {
                if (stationComponent.storage[i].count >= stationComponent.storage[i].max)
                {
                    continue;
                }
                stationComponent.currentCollections[i] += stationComponent.collectionPerTick[i] * collectSpeedRate;
                int num = (int)stationComponent.currentCollections[i];
                if (num != 0)
                {
                    lock (productRegister)
                    {
                        stationComponent.storage[i].count += num;
                        productRegister[stationComponent.storage[i].itemId] += num;
                        stationComponent.currentCollections[i] -= num;
                        miningFlags.AddMiningFlagUnsafe(LDB.veins.GetVeinTypeByItemId(stationComponent.storage[i].itemId));
                    }
                }
            }
        }
    }

    public void UpdateVeinCollection(OptimizedVeinMiner<StationMinerOutput>[] stationMiners, ref MiningFlags miningFlags)
    {
        lock (stationComponent.storage)
        {
            if (stationComponent.storage[0].localSupplyCount >= stationComponent.storage[0].max)
            {
                return;
            }

            if (_optimizedMinerIndex == null)
            {
                throw new InvalidOperationException($"{nameof(OptimizedStation)} attempted to update its miner counterpart but {nameof(_optimizedMinerIndex)} was null.");
            }

            ref OptimizedVeinMiner<StationMinerOutput> miner = ref stationMiners[_optimizedMinerIndex.Value];
            if (miner.productId == 0 || miner.productId != stationComponent.collectionIds[0] || miner.ProductCount <= 0)
            {
                return;
            }
            int productCount = miner.ProductCount;
            int num = stationComponent.storage[0].count;
            int max = stationComponent.storage[0].max;
            if (stationComponent.storage[0].localOrder < -max / 2)
            {
                num = ((stationComponent.storage[0].localOrder >= -max / 2 - max) ? (num + (stationComponent.storage[0].localOrder + max / 2)) : (num - stationComponent.storage[0].max));
            }
            int num2 = stationComponent.storage[0].max - num;
            num2 = ((num2 > productCount) ? productCount : num2);
            if (num2 != 0)
            {
                stationComponent.storage[0].count += num2;
                miner.ProductCount -= num2;
                if (miner.ProductCount == 0)
                {
                    miner.productId = 0;
                }
                miningFlags.AddMiningFlagUnsafe(LDB.veins.GetVeinTypeByItemId(stationComponent.storage[0].itemId));
            }
        }
    }

    private static int split_inc(ref int n, ref int m, int p)
    {
        if (n == 0)
        {
            return 0;
        }
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        num = ((num2 > 0) ? (num * p + num2) : (num * p));
        m -= num;
        return num;
    }

    public void InputItem(int itemId, int needIdx, int stack, int inc)
    {
        if (itemId <= 0)
        {
            return;
        }
        lock (stationComponent.storage)
        {
            if (needIdx < stationComponent.storage.Length && stationComponent.storage[needIdx].itemId == itemId)
            {
                stationComponent.storage[needIdx].count += stack;
                stationComponent.storage[needIdx].inc += inc;
            }
            else if (itemId == 1210)
            {
                stationComponent.warperCount += stack;
            }
        }
    }
}
