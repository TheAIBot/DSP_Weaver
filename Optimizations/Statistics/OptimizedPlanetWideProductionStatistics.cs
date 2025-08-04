using System;

namespace Weaver.Optimizations.Statistics;

internal sealed class OptimizedPlanetWideProductionStatistics
{
    private readonly OptimizedItemId[] _productIndexes;
    private readonly OptimizedItemId[] _consumeIndexes;
    private readonly ItemIdWithOptimizedRegisterIndex[] _additionalProductsToWatch;
    private readonly ItemIdWithOptimizedRegisterIndex[] _additionalConsumesToWatch;
    private readonly OptimizedProductionStatistics[] _optimizedProductionStatistics = [];
    private readonly FactoryProductionStat _factoryProductionStat;
    private readonly int[] _sumProductRegister;
    private readonly int[] _sumConsumeRegister;

    public OptimizedPlanetWideProductionStatistics(OptimizedItemId[] productIndexes,
                                                   OptimizedItemId[] consumeIndexes,
                                                   ItemIdWithOptimizedRegisterIndex[] additionalProductsToWatch,
                                                   ItemIdWithOptimizedRegisterIndex[] additionalConsumesToWatch,
                                                   OptimizedProductionStatistics[] optimizedProductionStatistics,
                                                   FactoryProductionStat factoryProductionStat)
    {
        _productIndexes = productIndexes;
        _consumeIndexes = consumeIndexes;
        _additionalProductsToWatch = additionalProductsToWatch;
        _additionalConsumesToWatch = additionalConsumesToWatch;
        _optimizedProductionStatistics = optimizedProductionStatistics;
        _factoryProductionStat = factoryProductionStat;
        _sumProductRegister = new int[productIndexes.Length];
        _sumConsumeRegister = new int[consumeIndexes.Length];
    }

    public void UpdateStatistics(long time, int[] gameProductRegister, int[] gameConsumeRegister)
    {
        int[] sumProductRegister = _sumProductRegister;
        int[] sumConsumeRegister = _sumConsumeRegister;

        OptimizedProductionStatistics[] optimizedProductionStatistics = _optimizedProductionStatistics;
        for (int i = 0; i < optimizedProductionStatistics.Length; i++)
        {
            optimizedProductionStatistics[i].AddToPlanetWideProductionStatistics(sumProductRegister, sumConsumeRegister);
        }

        AddItemsToWatch(gameProductRegister, sumProductRegister, _additionalProductsToWatch);
        AddItemsToWatch(gameConsumeRegister, sumConsumeRegister, _additionalConsumesToWatch);

        GameTick(time);

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

    public static void AddItemsToWatch(int[] gameRegister, int[] optimizedRegister, ItemIdWithOptimizedRegisterIndex[] additionalItemsToWatch)
    {
        for (int i = 0; i < additionalItemsToWatch.Length; i++)
        {
            ItemIdWithOptimizedRegisterIndex itemToWatch = additionalItemsToWatch[i];
            optimizedRegister[itemToWatch.OptimizedRegisterIndex] += gameRegister[itemToWatch.ItemIndex];
            gameRegister[itemToWatch.ItemIndex] = 0;
        }
    }
}
