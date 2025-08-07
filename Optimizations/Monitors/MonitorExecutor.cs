using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.Monitors;

internal sealed class MonitorExecutor
{
    private int[] _monitorIndexes = null!;
    private int[] _networkIds = null!;
    private OptimizedMonitor[] _optimizedMonitors = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int Count => _optimizedMonitors.Length;

    public void GameTick(PlanetFactory planet,
                         int[] monitorPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
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
            int networkIndex = networkIds[monitorIndexIndex];
            float power = networkServes[networkIndex];
            optimizedMonitors[monitorIndexIndex].InternalUpdate(ref monitors[monitorIndex], power, sandboxToolsEnabled, speakerPool);

            UpdatePower(monitorPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, monitorIndexIndex, networkIndex);
        }
    }

    public void UpdatePower(int[] monitorPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;

        for (int monitorIndex = 0; monitorIndex < networkIds.Length; monitorIndex++)
        {
            int networkIndex = networkIds[monitorIndex];
            UpdatePower(monitorPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, monitorIndex, networkIndex);
        }
    }

    private static void UpdatePower(int[] monitorPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int monitorIndex,
                                    int networkIndex)
    {
        int powerConsumerTypeIndex = monitorPowerConsumerIndexes[monitorIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] monitorPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int monitorIndex = 0; monitorIndex < prototypeIdIndexes.Length; monitorIndex++)
        {
            UpdatePowerConsumptionPerPrototype(monitorPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               monitorIndex);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] monitorPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int monitorIndexIndex)
    {
        int powerConsumerTypeIndex = monitorPowerConsumerIndexes[monitorIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[monitorIndexIndex]] += GetPowerConsumption(powerConsumerType);
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> monitorIndexes = [];
        List<int> networkIds = [];
        List<OptimizedMonitor> optimizedMonitors = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

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

            if (!beltExecutor.TryOptimizedCargoPath(planet, monitor.targetBeltId, out OptimizedCargoPath? targetBelt))
            {
                continue;
            }
            BeltComponent targetBeltComponent = planet.cargoTraffic.beltPool[monitor.targetBeltId];
            int targetBeltOffset = targetBeltComponent.segIndex + targetBeltComponent.segPivotOffset;

            int networkIndex = planet.powerSystem.consumerPool[monitor.pcId].networkId;
            subFactoryPowerSystemBuilder.AddMonitor(in monitor, networkIndex);
            monitorIndexes.Add(monitorIndex);
            networkIds.Add(networkIndex);
            optimizedMonitors.Add(new OptimizedMonitor(targetBelt, targetBeltComponent.speed, targetBeltOffset));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[monitor.entityId]);
        }

        _monitorIndexes = monitorIndexes.ToArray();
        _networkIds = networkIds.ToArray();
        _optimizedMonitors = optimizedMonitors.ToArray();
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType)
    {
        return powerConsumerType.GetRequiredEnergy(true);
    }
}
