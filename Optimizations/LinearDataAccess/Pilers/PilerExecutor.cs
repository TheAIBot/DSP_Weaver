using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Pilers;

internal sealed class PilerExecutor
{
    private int[] _pilerIndexes;

    public void GameTick(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        AnimData[] entityAnimPool = planet.entityAnimPool;

        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].InternalUpdate(cargoTraffic, entityAnimPool, out float _);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _pilerIndexes = subFactoryGraph.GetAllNodes()
                                       .Where(x => x.EntityTypeIndex.EntityType == EntityType.Piler)
                                       .Select(x => x.EntityTypeIndex.Index)
                                       .OrderBy(x => x)
                                       .ToArray();
    }
}
