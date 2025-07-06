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
        ProductionHelper.PartialProductionStatisticsGameTick(factoryStats, time, _dysonItemIds);
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
}

internal static class ProductionHelper
{
    public static void PartialProductionStatisticsGameTick(FactoryProductionStat factoryStats, long time, int[] itemsIdsToCheck)
    {
        if (time % 1 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int num3 = 6 + num;
            int num4 = 7 + num;
            int num5 = 13;
            int num6 = 4200;
            int num7 = itemsIdsToCheck.Length;
            for (int i = 0; i < num7; i++)
            {
                int num8 = itemsIdsToCheck[i];
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
        }
    }
}