using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Silos;

internal sealed class SiloExecutor
{
    private int[] _siloIndexes;
    private int[] _siloNetworkIds;

    public void GameTick(PlanetFactory planet)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        FactorySystem factorySystem = planet.factorySystem;
        AstroData[] astroPoses = factorySystem.planet.galaxy.astrosData;

        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];

            float power4 = networkServes[_siloNetworkIds[siloIndexIndex]];
            factorySystem.siloPool[siloIndex].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            planet.factorySystem.siloPool[siloIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        int[] siloNetworkIds = new int[_siloIndexes.Length];

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            siloNetworkIds[siloIndexIndex] = planet.powerSystem.consumerPool[silo.pcId].networkId;

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;
        }

        _siloNetworkIds = siloNetworkIds;
    }
}
