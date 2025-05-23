using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Ejectors;

internal sealed class EjectorExecutor
{
    private int[] _ejectorIndexes = null!;
    private int[] _ejectorNetworkIds = null!;

    public void GameTick(PlanetFactory planet, long time)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;

        DysonSwarm? swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];

            float power3 = networkServes[ejectorNetworkIds[ejectorIndexIndex]];
            planet.factorySystem.ejectorPool[ejectorIndex].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            if (planet.factorySystem.ejectorPool[ejectorIndex].id == ejectorIndex)
            {
                planet.factorySystem.ejectorPool[ejectorIndex].SetPCState(consumerPool);
            }
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
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

            ejectorNetworkIds[ejectorIndexIndex] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }
}
