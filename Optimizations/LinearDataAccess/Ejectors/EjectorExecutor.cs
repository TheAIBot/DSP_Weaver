using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Ejectors;

internal sealed class EjectorExecutor
{
    private int[] _ejectorIndexes = null!;
    private int[] _ejectorNetworkIds = null!;

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
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;

        DysonSwarm? swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
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
            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndexIndex, networkIndex, ref ejectors[ejectorIndex]);
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

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _ejectorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();

        int[] ejectorNetworkIds = new int[_ejectorIndexes.Length];

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

            optimizedPowerSystemBuilder.AddEjector(in ejector, networkIndex);
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly EjectorComponent ejector)
    {
        return powerConsumerType.GetRequiredEnergy(ejector.direction != 0, 1000 + Cargo.powerTable[ejector.incLevel]);
    }
}
