using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Ejectors;

internal sealed class EjectorExecutor
{
    private int[] _ejectorIndexes = null!;
    private int[] _ejectorNetworkIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public void GameTick(PlanetFactory planet,
                         long time,
                         int[] ejectorPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;
        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;

        DysonSwarm? swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            int networkIndex = ejectorNetworkIds[ejectorIndexIndex];
            float power3 = networkServes[networkIndex];
            ref EjectorComponent ejector = ref ejectors[ejectorIndex];
            ejector.InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);

            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndexIndex, networkIndex, ref ejector);
        }
    }

    public void UpdatePower(PlanetFactory planet,
                            int[] ejectorPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] ejectorNetworkIds = _ejectorNetworkIds;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            int networkIndex = ejectorNetworkIds[ejectorIndexIndex];
            ref readonly EjectorComponent ejector = ref ejectors[ejectorIndex];
            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndexIndex, networkIndex, in ejector);
        }
    }

    private static void UpdatePower(int[] ejectorPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int ejectorIndexIndex,
                                    int networkIndex,
                                    ref readonly EjectorComponent ejector)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, in ejector);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(PlanetFactory planet,
                                                                         int[] ejectorPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            ref readonly EjectorComponent ejector = ref ejectors[ejectorIndex];
            UpdatePowerConsumptionPerPrototype(ejectorPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               ejectorIndexIndex,
                                               in ejector);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] ejectorPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int ejectorIndexIndex,
                                                           ref readonly EjectorComponent ejector)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[ejectorIndexIndex]] += GetPowerConsumption(powerConsumerType, in ejector);
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder)
    {
        _ejectorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();

        int[] ejectorNetworkIds = new int[_ejectorIndexes.Length];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorIndex];

            int networkIndex = planet.powerSystem.consumerPool[ejector.pcId].networkId;
            ejectorNetworkIds[ejectorIndexIndex] = networkIndex;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;

            subFactoryPowerSystemBuilder.AddEjector(in ejector, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[ejector.entityId]);
        }

        _ejectorNetworkIds = ejectorNetworkIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly EjectorComponent ejector)
    {
        return powerConsumerType.GetRequiredEnergy(ejector.direction != 0, 1000 + Cargo.powerTable[ejector.incLevel]);
    }
}
