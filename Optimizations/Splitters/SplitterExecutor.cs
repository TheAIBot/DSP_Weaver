using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Splitters;

internal sealed class SplitterExecutor
{
    private OptimizedSplitter[] _optimizedSplitters = null!;

    public void GameTick(PlanetFactory planet, OptimizedSubFactory subFactory, BeltExecutor beltExecutor)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        OptimizedSplitter[] optimizedSplitters = _optimizedSplitters;
        for (int i = 0; i < optimizedSplitters.Length; i++)
        {
            optimizedSplitters[i].UpdateSplitter(planet, cargoTraffic, subFactory, beltExecutor);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        List<OptimizedSplitter> optimizedSplitters = [];
        foreach (int splitterIndex in subFactoryGraph.GetAllNodes()
                                                     .Where(x => x.EntityTypeIndex.EntityType == EntityType.Splitter)
                                                     .Select(x => x.EntityTypeIndex.Index)
                                                     .OrderBy(x => x))
        {
            ref readonly SplitterComponent splitter = ref planet.cargoTraffic.splitterPool[splitterIndex];
            if (splitter.id != splitterIndex)
            {
                continue;
            }

            optimizedSplitters.Add(new OptimizedSplitter(in splitter));
        }

        _optimizedSplitters = optimizedSplitters.ToArray();
    }
}
