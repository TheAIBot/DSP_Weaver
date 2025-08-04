using System.Collections.Generic;

namespace Weaver.Optimizations;

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