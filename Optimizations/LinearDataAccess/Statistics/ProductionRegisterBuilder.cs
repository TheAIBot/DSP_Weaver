using System;
using System.Collections.Generic;
using System.Linq;

namespace Weaver.Optimizations.LinearDataAccess.Statistics;

internal readonly struct OptimizedItemId
{
    public readonly short ItemIndex;
    public readonly short OptimizedItemIndex;

    public OptimizedItemId(int itemIndex, int optimizedItemIndex)
    {
        if (itemIndex > short.MaxValue || itemIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(itemIndex)} first in a short is not correct.");
        }
        if (optimizedItemIndex > short.MaxValue || optimizedItemIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(optimizedItemIndex)} first in a short is not correct.");
        }

        ItemIndex = (short)itemIndex;
        OptimizedItemIndex = (short)optimizedItemIndex;
    }

    public override readonly bool Equals(object obj)
    {
        if (obj is not OptimizedItemId other)
        {
            return false;
        }

        return ItemIndex == other.ItemIndex &&
               OptimizedItemIndex == other.OptimizedItemIndex;
    }

    public override readonly int GetHashCode()
    {
        var hasCode = new HashCode();
        hasCode.Add(ItemIndex);
        hasCode.Add(OptimizedItemIndex);

        return hasCode.ToHashCode();
    }
}

internal sealed class PlanetWideProductionRegisterBuilder
{
    private readonly PlanetFactory _planet;
    private readonly Dictionary<int, int> _productIndexToOptimizedProductIndex = [];
    private readonly Dictionary<int, int> _consumeIndexToOptimizedConsumeIndex = [];
    private readonly List<OptimizedProductionStatistics> _optimizedProductionStatistics = [];
    private readonly HashSet<int> _additionalItemsIdsToWatch = [];

    public PlanetWideProductionRegisterBuilder(PlanetFactory planet)
    {
        _planet = planet;
    }

    public SubFactoryProductionRegisterBuilder GetSubFactoryBuilder()
    {
        return new SubFactoryProductionRegisterBuilder(this);
    }

    public int AddProduct(int productIndex)
    {
        if (!_productIndexToOptimizedProductIndex.TryGetValue(productIndex, out int optimizedProductIndex))
        {
            optimizedProductIndex = _productIndexToOptimizedProductIndex.Count;
            _productIndexToOptimizedProductIndex.Add(productIndex, optimizedProductIndex);
        }

        return optimizedProductIndex;
    }

    public int AddConsume(int consumeIndex)
    {
        if (!_consumeIndexToOptimizedConsumeIndex.TryGetValue(consumeIndex, out int optimizedConsumeIndex))
        {
            optimizedConsumeIndex = _consumeIndexToOptimizedConsumeIndex.Count;
            _consumeIndexToOptimizedConsumeIndex.Add(consumeIndex, optimizedConsumeIndex);
        }

        return optimizedConsumeIndex;
    }

    public void AddOptimizedProductionStatistics(ref readonly OptimizedProductionStatistics optimizedProductionStatistics)
    {
        _optimizedProductionStatistics.Add(optimizedProductionStatistics);
    }

    public void AdditionalItemsIdsToWatch(int itemId)
    {
        _additionalItemsIdsToWatch.Add(itemId);
    }

    public OptimizedPlanetWideProductionStatistics Build()
    {
        OptimizedItemId[] productIndexes = _productIndexToOptimizedProductIndex.Select(x => new OptimizedItemId(x.Key, x.Value))
                                                                               .OrderBy(x => x.ItemIndex)
                                                                               .ToArray();
        OptimizedItemId[] consumeIndexes = _consumeIndexToOptimizedConsumeIndex.Select(x => new OptimizedItemId(x.Key, x.Value))
                                                                               .OrderBy(x => x.ItemIndex)
                                                                               .ToArray();

        if (CanPickupItemsFromEnemy(_planet))
        {
            int[] enemyDropItemIds = ItemProto.enemyDropRangeTable;
            for (int i = 0; i < enemyDropItemIds.Length; i++)
            {
                _additionalItemsIdsToWatch.Add(enemyDropItemIds[i]);
            }
        }

        for (int i = 0; i < productIndexes.Length; i++)
        {
            _additionalItemsIdsToWatch.Remove(productIndexes[i].ItemIndex);
        }

        for (int i = 0; i < consumeIndexes.Length; i++)
        {
            _additionalItemsIdsToWatch.Remove(consumeIndexes[i].ItemIndex);
        }

        return new OptimizedPlanetWideProductionStatistics(productIndexes,
                                                           consumeIndexes,
                                                           _optimizedProductionStatistics.ToArray(),
                                                           _additionalItemsIdsToWatch.OrderBy(x => x).ToArray(),
                                                           GameMain.statistics.production.factoryStatPool[_planet.index]);
    }

    private static bool CanPickupItemsFromEnemy(PlanetFactory planet)
    {
        if (planet.defenseSystem.battleBases.count == 0)
        {
            return false;
        }

        bool hasBattleBaseThatCanPickUpItems = false;
        for (int i = 1; i < planet.defenseSystem.battleBases.cursor; i++)
        {
            ref readonly BattleBaseComponent component = ref planet.defenseSystem.battleBases.buffer[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            if (component.autoPickEnabled)
            {
                hasBattleBaseThatCanPickUpItems = true;
            }
        }

        if (!hasBattleBaseThatCanPickUpItems)
        {
            return false;
        }

        // Can't check if there is any enemies because they might
        // show up at a later time.
        return true;
    }
}

internal sealed class OptimizedPlanetWideProductionStatistics
{
    private readonly OptimizedItemId[] _productIndexes;
    private readonly OptimizedItemId[] _consumeIndexes;
    private readonly OptimizedProductionStatistics[] _optimizedProductionStatistics = [];
    private readonly int[] _additionalItemsIdsToWatch;
    private readonly FactoryProductionStat _factoryProductionStat;
    private readonly int[] _sumProductRegister;
    private readonly int[] _sumConsumeRegister;

    public OptimizedPlanetWideProductionStatistics(OptimizedItemId[] productIndexes,
                                                   OptimizedItemId[] consumeIndexes,
                                                   OptimizedProductionStatistics[] optimizedProductionStatistics,
                                                   int[] additionalItemsIdsToWatch,
                                                   FactoryProductionStat factoryProductionStat)
    {
        _productIndexes = productIndexes;
        _consumeIndexes = consumeIndexes;
        _optimizedProductionStatistics = optimizedProductionStatistics;
        _additionalItemsIdsToWatch = additionalItemsIdsToWatch;
        _factoryProductionStat = factoryProductionStat;
        _sumProductRegister = new int[productIndexes.Length];
        _sumConsumeRegister = new int[consumeIndexes.Length];
    }

    public void UpdateStatistics(long time)
    {
        int[] sumProductRegister = _sumProductRegister;
        int[] sumConsumeRegister = _sumConsumeRegister;

        OptimizedProductionStatistics[] optimizedProductionStatistics = _optimizedProductionStatistics;
        for (int i = 0; i < optimizedProductionStatistics.Length; i++)
        {
            optimizedProductionStatistics[i].AddToPlanetWideProductionStatistics(sumProductRegister, sumConsumeRegister);
        }

        GameTick(time);

        ClearAdditionalItemsToWatch();
        Array.Clear(sumProductRegister, 0, sumProductRegister.Length);
        Array.Clear(sumConsumeRegister, 0, sumConsumeRegister.Length);
        for (int i = 0; i < optimizedProductionStatistics.Length; i++)
        {
            optimizedProductionStatistics[i].Clear();
        }
    }

    private void GameTick(long time)
    {
        FactoryProductionStat factoryProductionStat = _factoryProductionStat;

        ProductionHelper.PartialProductionStatisticsGameTick(factoryProductionStat, time, _additionalItemsIdsToWatch);

        if (time % 1 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int num3 = 6 + num;
            int num4 = 7 + num;
            int num5 = 13;
            int num6 = 4200;

            {
                OptimizedItemId[] productIndexes = _productIndexes;
                int[] sumProductRegister = _sumProductRegister;
                for (int i = 0; i < productIndexes.Length; i++)
                {
                    OptimizedItemId productIndex = productIndexes[i];

                    int itemId = productIndex.ItemIndex;
                    int num10 = sumProductRegister[productIndex.OptimizedItemIndex];
                    int num12 = factoryProductionStat.productIndices[itemId];
                    if (num12 <= 0)
                    {
                        if (num10 <= 0)
                        {
                            continue;
                        }
                        int num13 = factoryProductionStat.productCursor;
                        factoryProductionStat.CreateProductStat(itemId);
                        factoryProductionStat.productIndices[itemId] = num13;
                        num12 = num13;
                    }
                    ProductStat obj = factoryProductionStat.productPool[num12];
                    int[] count = obj.count;
                    int[] cursor = obj.cursor;
                    long[] total = obj.total;
                    int num14 = cursor[num];
                    int num15 = num10 - count[num14];
                    count[num14] = num10;
                    total[num] += num15;
                    total[num2] += num10;
                    cursor[num]++;
                    if (cursor[num] >= 600)
                    {
                        cursor[num] -= 600;
                    }
                }
            }

            {
                OptimizedItemId[] consumeIndexes = _consumeIndexes;
                int[] sumConsumeRegister = _sumConsumeRegister;
                for (int i = 0; i < consumeIndexes.Length; i++)
                {
                    OptimizedItemId consumeIndex = consumeIndexes[i];

                    int itemId = consumeIndex.ItemIndex;
                    int num11 = sumConsumeRegister[consumeIndex.OptimizedItemIndex];
                    int num12 = factoryProductionStat.productIndices[itemId];
                    if (num12 <= 0)
                    {
                        if (num11 <= 0)
                        {
                            continue;
                        }
                        int num13 = factoryProductionStat.productCursor;
                        factoryProductionStat.CreateProductStat(itemId);
                        factoryProductionStat.productIndices[itemId] = num13;
                        num12 = num13;
                    }
                    ProductStat obj = factoryProductionStat.productPool[num12];
                    int[] count = obj.count;
                    int[] cursor = obj.cursor;
                    long[] total = obj.total;
                    int num16 = cursor[num3];
                    int num17 = num11 - count[num16];
                    count[num16] = num11;
                    total[num4] += num17;
                    total[num5] += num11;
                    cursor[num3]++;
                    if (cursor[num3] >= num6)
                    {
                        cursor[num3] -= 600;
                    }
                }
            }
            for (int j = 0; j < factoryProductionStat.powerPool.Length; j++)
            {
                PowerStat obj2 = factoryProductionStat.powerPool[j];
                long[] energy = obj2.energy;
                long[] total2 = obj2.total;
                int[] cursor2 = obj2.cursor;
                long num18 = 0L;
                switch (j)
                {
                    case 0:
                        num18 = factoryProductionStat.powerGenRegister;
                        break;
                    case 1:
                        num18 = factoryProductionStat.powerConRegister;
                        break;
                    case 2:
                        num18 = factoryProductionStat.powerChaRegister;
                        break;
                    case 3:
                        num18 = factoryProductionStat.powerDisRegister;
                        break;
                    case 4:
                        num18 = factoryProductionStat.hashRegister;
                        break;
                }
                int num19 = cursor2[num];
                long num20 = num18 - energy[num19];
                energy[num19] = num18;
                total2[num] += num20;
                total2[num2] += num18;
                cursor2[num]++;
                if (cursor2[num] >= 600)
                {
                    cursor2[num] -= 600;
                }
            }
        }
        if (time % 6 == 0L)
        {
            int level = 1;
            factoryProductionStat.ComputeTheMiddleLevel(level);
        }
        if (time % 60 == 0L)
        {
            int level2 = 2;
            factoryProductionStat.ComputeTheMiddleLevel(level2);
        }
        if (time % 360 == 0L)
        {
            int level3 = 3;
            factoryProductionStat.ComputeTheMiddleLevel(level3);
        }
        if (time % 3600 == 0L)
        {
            int level4 = 4;
            factoryProductionStat.ComputeTheMiddleLevel(level4);
        }
        if (time % 36000 == 0L)
        {
            int level5 = 5;
            factoryProductionStat.ComputeTheMiddleLevel(level5);
        }
    }

    private void ClearAdditionalItemsToWatch()
    {
        FactoryProductionStat factoryProductionStat = _factoryProductionStat;
        int[] additionalItemsIdsToWatch = _additionalItemsIdsToWatch;

        int[] productRegister = factoryProductionStat.productRegister;
        for (int i = 0; i < additionalItemsIdsToWatch.Length; i++)
        {
            productRegister[additionalItemsIdsToWatch[i]] = 0;
        }

        int[] consumeRegister = factoryProductionStat.consumeRegister;
        for (int i = 0; i < additionalItemsIdsToWatch.Length; i++)
        {
            consumeRegister[additionalItemsIdsToWatch[i]] = 0;
        }
    }
}

internal sealed class SubFactoryProductionRegisterBuilder
{
    private readonly PlanetWideProductionRegisterBuilder _planetWideProductionRegisterBuilder;
    private readonly Dictionary<int, OptimizedIndexes> _productIndexToOptimizedProductIndexes = [];
    private readonly Dictionary<int, OptimizedIndexes> _consumeIndexToOptimizedConsumeIndexes = [];

    public SubFactoryProductionRegisterBuilder(PlanetWideProductionRegisterBuilder planetWideProductionRegisterBuilder)
    {
        _planetWideProductionRegisterBuilder = planetWideProductionRegisterBuilder;
    }

    public OptimizedItemId AddProduct(int productIndex)
    {
        int planetWideOptimizedIndex = _planetWideProductionRegisterBuilder.AddProduct(productIndex);

        if (!_productIndexToOptimizedProductIndexes.TryGetValue(productIndex, out OptimizedIndexes optimizedIndexes))
        {
            optimizedIndexes = new OptimizedIndexes(_productIndexToOptimizedProductIndexes.Count, planetWideOptimizedIndex);
            _productIndexToOptimizedProductIndexes.Add(productIndex, optimizedIndexes);
        }

        return new OptimizedItemId(productIndex, optimizedIndexes.SubFactoryOptimizedIndex);
    }

    public OptimizedItemId[] AddProduct(int[] productIndexes)
    {
        var optimizedIndexes = new OptimizedItemId[productIndexes.Length];
        for (int i = 0; i < productIndexes.Length; i++)
        {
            optimizedIndexes[i] = AddProduct(productIndexes[i]);
        }

        return optimizedIndexes;
    }

    public OptimizedItemId AddConsume(int consumeIndex)
    {
        int planetWideOptimizedIndex = _planetWideProductionRegisterBuilder.AddConsume(consumeIndex);

        if (!_consumeIndexToOptimizedConsumeIndexes.TryGetValue(consumeIndex, out OptimizedIndexes optimizedIndexes))
        {
            optimizedIndexes = new OptimizedIndexes(_consumeIndexToOptimizedConsumeIndexes.Count, planetWideOptimizedIndex);
            _consumeIndexToOptimizedConsumeIndexes.Add(consumeIndex, optimizedIndexes);
        }

        return new OptimizedItemId(consumeIndex, optimizedIndexes.SubFactoryOptimizedIndex);
    }

    public OptimizedItemId[] AddConsume(int[] consumeIndexes)
    {
        var optimizedIndexes = new OptimizedItemId[consumeIndexes.Length];
        for (int i = 0; i < consumeIndexes.Length; i++)
        {
            optimizedIndexes[i] = AddConsume(consumeIndexes[i]);
        }

        return optimizedIndexes;
    }

    public OptimizedProductionStatistics Build()
    {
        int[] planetWideOptimizedProductIndex = _productIndexToOptimizedProductIndexes.OrderBy(x => x.Value.SubFactoryOptimizedIndex)
                                                                                      .Select(x => x.Value.PlanetWideOptimizedIndex)
                                                                                      .ToArray();
        int[] planetWideOptimizedConsumeIndex = _consumeIndexToOptimizedConsumeIndexes.OrderBy(x => x.Value.SubFactoryOptimizedIndex)
                                                                                      .Select(x => x.Value.PlanetWideOptimizedIndex)
                                                                                      .ToArray();

        var optimizedStatistics = new OptimizedProductionStatistics(planetWideOptimizedProductIndex,
                                                                    planetWideOptimizedConsumeIndex);
        _planetWideProductionRegisterBuilder.AddOptimizedProductionStatistics(ref optimizedStatistics);
        return optimizedStatistics;
    }

    private record struct OptimizedIndexes(int SubFactoryOptimizedIndex, int PlanetWideOptimizedIndex);
}

internal readonly struct OptimizedProductionStatistics
{
    private readonly int[] _planetWideOptimizedProductIndex;
    private readonly int[] _planetWideOptimizedConsumeIndex;
    public readonly int[] ProductRegister;
    public readonly int[] ConsumeRegister;

    public OptimizedProductionStatistics(int[] planetWideOptimizedProductIndex,
                                         int[] planetWideOptimizedConsumeIndex)
    {
        _planetWideOptimizedProductIndex = planetWideOptimizedProductIndex;
        _planetWideOptimizedConsumeIndex = planetWideOptimizedConsumeIndex;
        ProductRegister = new int[planetWideOptimizedProductIndex.Length];
        ConsumeRegister = new int[planetWideOptimizedConsumeIndex.Length];
    }

    public readonly void AddToPlanetWideProductionStatistics(int[] sumProductRegister, int[] sumConsumeRegister)
    {
        int[] planetWideOptimizedProductIndex = _planetWideOptimizedProductIndex;
        int[] productRegister = ProductRegister;
        for (int i = 0; i < planetWideOptimizedProductIndex.Length; i++)
        {
            sumProductRegister[planetWideOptimizedProductIndex[i]] += productRegister[i];
        }

        int[] planetWideOptimizedConsumeIndex = _planetWideOptimizedConsumeIndex;
        int[] consumeRegister = ConsumeRegister;
        for (int i = 0; i < planetWideOptimizedConsumeIndex.Length; i++)
        {
            sumConsumeRegister[planetWideOptimizedConsumeIndex[i]] += consumeRegister[i];
        }
    }

    public readonly void Clear()
    {
        Array.Clear(ProductRegister, 0, ProductRegister.Length);
        Array.Clear(ConsumeRegister, 0, ConsumeRegister.Length);
    }
}