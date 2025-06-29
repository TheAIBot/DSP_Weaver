namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal record struct GeneratorIDWithGenerators<T>(GeneratorID GeneratorID, T[] OptimizedFuelGenerators);
