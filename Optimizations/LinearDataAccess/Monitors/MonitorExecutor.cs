using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Monitors;

internal sealed class MonitorExecutor
{
    private int[] _monitorIndexes = null!;
    private int[] _networkIds = null!;
    private OptimizedMonitor[] _optimizedMonitors = null!;

    public void GameTick(PlanetFactory planet)
    {
        SpeakerComponent[] speakerPool = planet.digitalSystem.speakerPool;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;
        float[] networkServes = planet.powerSystem.networkServes;
        MonitorComponent[] monitors = planet.cargoTraffic.monitorPool;
        int[] monitorIndexes = _monitorIndexes;
        int[] networkIds = _networkIds;
        OptimizedMonitor[] optimizedMonitors = _optimizedMonitors;

        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = monitorIndexes[monitorIndexIndex];
            float power = networkServes[networkIds[monitorIndexIndex]];
            optimizedMonitors[monitorIndexIndex].InternalUpdate(ref monitors[monitorIndex], power, sandboxToolsEnabled, speakerPool);
        }
    }

    public void UpdatePower(int[] monitorPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;
        OptimizedMonitor[] optimizedMonitors = _optimizedMonitors;

        for (int j = 0; j < optimizedMonitors.Length; j++)
        {
            int networkIndex = networkIds[j];
            int powerConsumerTypeIndex = monitorPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> monitorIndexes = [];
        List<int> networkIds = [];
        List<OptimizedMonitor> optimizedMonitors = [];

        foreach (int monitorIndex in subFactoryGraph.GetAllNodes()
                                                    .Where(x => x.EntityTypeIndex.EntityType == EntityType.Monitor)
                                                    .Select(x => x.EntityTypeIndex.Index)
                                                    .OrderBy(x => x))
        {
            ref readonly MonitorComponent monitor = ref planet.cargoTraffic.monitorPool[monitorIndex];
            if (monitor.id != monitorIndex)
            {
                continue;
            }

            if (monitor.targetBeltId <= 0)
            {
                continue;
            }

            BeltComponent targetBeltComponent = planet.cargoTraffic.beltPool[monitor.targetBeltId];
            int targetBeltOffset = targetBeltComponent.segIndex + targetBeltComponent.segPivotOffset;
            CargoPath? targetCargoPath = planet.cargoTraffic.pathPool[targetBeltComponent.segPathId];
            if (targetCargoPath == null)
            {
                continue;
            }

            OptimizedCargoPath targetBelt = beltExecutor.GetOptimizedCargoPath(targetCargoPath);
            int networkIndex = planet.powerSystem.consumerPool[monitor.pcId].networkId;
            optimizedPowerSystemBuilder.AddMonitor(in monitor, networkIndex);
            monitorIndexes.Add(monitorIndex);
            networkIds.Add(networkIndex);
            optimizedMonitors.Add(new OptimizedMonitor(targetBelt, targetBeltComponent.speed, targetBeltOffset));
        }

        _monitorIndexes = monitorIndexes.ToArray();
        _networkIds = networkIds.ToArray();
        _optimizedMonitors = optimizedMonitors.ToArray();
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType)
    {
        return powerConsumerType.GetRequiredEnergy(true);
    }
}
