using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Tanks;

internal sealed class TankExecutor
{
    private int[] _tankIndexes;

    public void GameTick(PlanetFactory planet)
    {
        FactoryStorage storage = planet.factoryStorage;
        for (int tankIndexIndex = 0; tankIndexIndex < _tankIndexes.Length; tankIndexIndex++)
        {
            int tankIndex = _tankIndexes[tankIndexIndex];
            storage.tankPool[tankIndex].GameTick(planet);
            storage.tankPool[tankIndex].TickOutput(planet);
            if (storage.tankPool[tankIndex].fluidCount == 0)
            {
                storage.tankPool[tankIndex].fluidId = 0;
            }
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _tankIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Tank)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();
    }
}
