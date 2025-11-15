using System.Collections.Generic;
using System.Linq;

namespace Weaver.Optimizations.Statistics;

internal sealed class SubFactoryProductionRegisterBuilder
{
    private readonly PlanetWideProductionRegisterBuilder _planetWideProductionRegisterBuilder;
    private readonly Dictionary<int, OptimizedIndexes> _productIndexToOptimizedProductIndexes = [];
    private readonly Dictionary<int, OptimizedIndexes> _consumeIndexToOptimizedConsumeIndexes = [];

    public SubFactoryProductionRegisterBuilder(PlanetWideProductionRegisterBuilder planetWideProductionRegisterBuilder)
    {
        _planetWideProductionRegisterBuilder = planetWideProductionRegisterBuilder;
    }

    public OptimizedItemId AddProduct(int productIndex)
    {
        int planetWideOptimizedIndex = _planetWideProductionRegisterBuilder.AddProduct(productIndex);

        if (!_productIndexToOptimizedProductIndexes.TryGetValue(productIndex, out OptimizedIndexes optimizedIndexes))
        {
            optimizedIndexes = new OptimizedIndexes(_productIndexToOptimizedProductIndexes.Count, planetWideOptimizedIndex);
            _productIndexToOptimizedProductIndexes.Add(productIndex, optimizedIndexes);
        }

        return new OptimizedItemId(productIndex, optimizedIndexes.SubFactoryOptimizedIndex);
    }

    public OptimizedItemId[] AddProduct(int[] productIndexes)
    {
        var optimizedIndexes = new OptimizedItemId[productIndexes.Length];
        for (int i = 0; i < productIndexes.Length; i++)
        {
            optimizedIndexes[i] = AddProduct(productIndexes[i]);
        }

        return optimizedIndexes;
    }

    public OptimizedItemId AddConsume(int consumeIndex)
    {
        int planetWideOptimizedIndex = _planetWideProductionRegisterBuilder.AddConsume(consumeIndex);

        if (!_consumeIndexToOptimizedConsumeIndexes.TryGetValue(consumeIndex, out OptimizedIndexes optimizedIndexes))
        {
            optimizedIndexes = new OptimizedIndexes(_consumeIndexToOptimizedConsumeIndexes.Count, planetWideOptimizedIndex);
            _consumeIndexToOptimizedConsumeIndexes.Add(consumeIndex, optimizedIndexes);
        }

        return new OptimizedItemId(consumeIndex, optimizedIndexes.SubFactoryOptimizedIndex);
    }

    public OptimizedItemId[] AddConsume(int[] consumeIndexes)
    {
        var optimizedIndexes = new OptimizedItemId[consumeIndexes.Length];
        for (int i = 0; i < consumeIndexes.Length; i++)
        {
            optimizedIndexes[i] = AddConsume(consumeIndexes[i]);
        }

        return optimizedIndexes;
    }

    public OptimizedProductionStatistics Build(UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        int[] planetWideOptimizedProductIndex = universeStaticDataBuilder.DeduplicateArrayUnmanaged(_productIndexToOptimizedProductIndexes.OrderBy(x => x.Value.SubFactoryOptimizedIndex)
                                                                                                                                          .Select(x => x.Value.PlanetWideOptimizedIndex)
                                                                                                                                          .ToArray());
        int[] planetWideOptimizedConsumeIndex = universeStaticDataBuilder.DeduplicateArrayUnmanaged(_consumeIndexToOptimizedConsumeIndexes.OrderBy(x => x.Value.SubFactoryOptimizedIndex)
                                                                                                                                          .Select(x => x.Value.PlanetWideOptimizedIndex)
                                                                                                                                          .ToArray());

        var optimizedStatistics = new OptimizedProductionStatistics(planetWideOptimizedProductIndex,
                                                                    planetWideOptimizedConsumeIndex);
        _planetWideProductionRegisterBuilder.AddOptimizedProductionStatistics(ref optimizedStatistics);
        return optimizedStatistics;
    }

    private record struct OptimizedIndexes(int SubFactoryOptimizedIndex, int PlanetWideOptimizedIndex);
}
