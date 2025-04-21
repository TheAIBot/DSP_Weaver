using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

internal sealed class BeltExecutor
{
    private int[] _beltIndexes;

    public void GameTick(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        for (int beltIndexIndex = 0; beltIndexIndex < _beltIndexes.Length; beltIndexIndex++)
        {
            int beltIndex = _beltIndexes[beltIndexIndex];
            cargoTraffic.pathPool[beltIndex].Update();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _beltIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Belt)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();
    }
}
