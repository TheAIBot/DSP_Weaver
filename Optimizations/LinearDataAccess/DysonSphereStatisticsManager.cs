using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class DysonSphereStatisticsManager
{
    private readonly HashSet<int[]> _dysonSphereProductionRegisters = [];
    private const int _sailItemId = 11901;
    private const int _structureNodeItemId = 11902;
    private const int _cellItemId = 11903;
    private static readonly int[] _dysonItemIds = [_sailItemId, _structureNodeItemId, _cellItemId];

    public bool IsDysonSphereStatistics(FactoryProductionStat factoryStats)
    {
        return _dysonSphereProductionRegisters.Contains(factoryStats.productRegister);
    }

    public void FindAllDysonSphereProductRegisters()
    {
        for (int i = 0; i < GameMain.data.dysonSpheres.Length; i++)
        {
            DysonSphere dysonSphere = GameMain.data.dysonSpheres[i];
            if (dysonSphere == null)
            {
                continue;
            }

            int[]? dysonSphereProductRegister = dysonSphere.productRegister;
            if (dysonSphereProductRegister == null)
            {
                continue;
            }

            _dysonSphereProductionRegisters.Add(dysonSphereProductRegister);
        }
    }

    public void ClearDysonSphereProductRegisters()
    {
        _dysonSphereProductionRegisters.Clear();
    }

    public void DysonSphereGameTick(FactoryProductionStat factoryStats, long time)
    {
        GameTick(factoryStats, time);
    }

    public void DysonSphereClearRegisters(FactoryProductionStat factoryStats)
    {
        int[] dysonitemIds = _dysonItemIds;
        for (int i = 0; i < dysonitemIds.Length; i++)
        {
            factoryStats.productRegister[dysonitemIds[i]] = 0;
            factoryStats.consumeRegister[dysonitemIds[i]] = 0;
        }
    }

    public void GameTick(FactoryProductionStat factoryStats, long time)
    {
        if (time % 1 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int num3 = 6 + num;
            int num4 = 7 + num;
            int num5 = 13;
            int num6 = 4200;
            int[] dysonitemIds = _dysonItemIds;
            int num7 = dysonitemIds.Length;
            for (int i = 0; i < num7; i++)
            {
                int num8 = dysonitemIds[i];
                int num9 = num8;
                int num10 = factoryStats.productRegister[num8];
                int num11 = factoryStats.consumeRegister[num8];
                int num12 = factoryStats.productIndices[num9];
                if (num12 <= 0)
                {
                    if (num10 <= 0 && num11 <= 0)
                    {
                        continue;
                    }
                    int num13 = factoryStats.productCursor;
                    factoryStats.CreateProductStat(num9);
                    factoryStats.productIndices[num9] = num13;
                    num12 = num13;
                }
                ProductStat obj = factoryStats.productPool[num12];
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
            //for (int j = 0; j < factoryStats.powerPool.Length; j++)
            //{
            //    PowerStat obj2 = factoryStats.powerPool[j];
            //    long[] energy = obj2.energy;
            //    long[] total2 = obj2.total;
            //    int[] cursor2 = obj2.cursor;
            //    long num18 = 0L;
            //    switch (j)
            //    {
            //        case 0:
            //            num18 = factoryStats.powerGenRegister;
            //            break;
            //        case 1:
            //            num18 = factoryStats.powerConRegister;
            //            break;
            //        case 2:
            //            num18 = factoryStats.powerChaRegister;
            //            break;
            //        case 3:
            //            num18 = factoryStats.powerDisRegister;
            //            break;
            //        case 4:
            //            num18 = factoryStats.hashRegister;
            //            break;
            //    }
            //    int num19 = cursor2[num];
            //    long num20 = num18 - energy[num19];
            //    energy[num19] = num18;
            //    total2[num] += num20;
            //    total2[num2] += num18;
            //    cursor2[num]++;
            //    if (cursor2[num] >= 600)
            //    {
            //        cursor2[num] -= 600;
            //    }
            //}
        }
        //if (time % 6 == 0L)
        //{
        //    int level = 1;
        //    factoryStats.ComputeTheMiddleLevel(level);
        //}
        //if (time % 60 == 0L)
        //{
        //    int level2 = 2;
        //    factoryStats.ComputeTheMiddleLevel(level2);
        //}
        //if (time % 360 == 0L)
        //{
        //    int level3 = 3;
        //    factoryStats.ComputeTheMiddleLevel(level3);
        //}
        //if (time % 3600 == 0L)
        //{
        //    int level4 = 4;
        //    factoryStats.ComputeTheMiddleLevel(level4);
        //}
        //if (time % 36000 == 0L)
        //{
        //    int level5 = 5;
        //    factoryStats.ComputeTheMiddleLevel(level5);
        //}
    }
}