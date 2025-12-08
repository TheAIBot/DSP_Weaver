using System;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Statistics;

internal readonly struct OptimizedProductionStatistics
{
    private readonly ReadonlyArray<int> _planetWideOptimizedProductIndex;
    private readonly ReadonlyArray<int> _planetWideOptimizedConsumeIndex;
    public readonly int[] ProductRegister;
    public readonly int[] ConsumeRegister;

    public OptimizedProductionStatistics(ReadonlyArray<int> planetWideOptimizedProductIndex,
                                         ReadonlyArray<int> planetWideOptimizedConsumeIndex)
    {
        _planetWideOptimizedProductIndex = planetWideOptimizedProductIndex;
        _planetWideOptimizedConsumeIndex = planetWideOptimizedConsumeIndex;
        ProductRegister = new int[planetWideOptimizedProductIndex.Length];
        ConsumeRegister = new int[planetWideOptimizedConsumeIndex.Length];
    }

    public readonly void AddToPlanetWideProductionStatistics(int[] sumProductRegister, int[] sumConsumeRegister)
    {
        ReadonlyArray<int> planetWideOptimizedProductIndex = _planetWideOptimizedProductIndex;
        int[] productRegister = ProductRegister;
        for (int i = 0; i < planetWideOptimizedProductIndex.Length; i++)
        {
            sumProductRegister[planetWideOptimizedProductIndex[i]] += productRegister[i];
        }

        ReadonlyArray<int> planetWideOptimizedConsumeIndex = _planetWideOptimizedConsumeIndex;
        int[] consumeRegister = ConsumeRegister;
        for (int i = 0; i < planetWideOptimizedConsumeIndex.Length; i++)
        {
            sumConsumeRegister[planetWideOptimizedConsumeIndex[i]] += consumeRegister[i];
        }
    }

    public readonly void Clear()
    {
        Array.Clear(ProductRegister, 0, ProductRegister.Length);
        Array.Clear(ConsumeRegister, 0, ConsumeRegister.Length);
    }
}