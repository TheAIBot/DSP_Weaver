namespace Weaver.Optimizations.PowerSystems;

internal record struct GeneratorIDWithGenerators<T>(GeneratorID GeneratorID, T[] OptimizedFuelGenerators);
