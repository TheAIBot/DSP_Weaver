using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Splitters;

internal sealed class SplitterExecutor
{
    private OptimizedSplitter[] _optimizedSplitters = null!;

    public int Count => _optimizedSplitters.Length;

    public void GameTick(OptimizedSubFactory subFactory, OptimizedCargoPath[] optimizedCargoPaths)
    {
        OptimizedSplitter[] optimizedSplitters = _optimizedSplitters;
        for (int i = 0; i < optimizedSplitters.Length; i++)
        {
            optimizedSplitters[i].UpdateSplitter(subFactory, optimizedCargoPaths);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph, BeltExecutor beltExecutor)
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

            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.input0, out int input0Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.input1, out int input1Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.input2, out int input2Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.input3, out int input3Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.output0, out int output0Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.output1, out int output1Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.output2, out int output2Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, splitter.output3, out int output3Index);

            optimizedSplitters.Add(new OptimizedSplitter(in splitter,
                                                         input0Index,
                                                         input1Index,
                                                         input2Index,
                                                         input3Index,
                                                         output0Index,
                                                         output1Index,
                                                         output2Index,
                                                         output3Index));
        }

        _optimizedSplitters = optimizedSplitters.ToArray();
    }
}
