using System.Collections.Generic;

namespace Weaver.Optimizations.Belts;

internal sealed class PlanetWideBeltExecutor
{
    private readonly Dictionary<CargoPath, OptimizedCargoPath> _cargoPathToOptimizedCargoPath = [];

    public OptimizedCargoPath GetOptimizedCargoPath(CargoPath cargoPath)
    {
        return _cargoPathToOptimizedCargoPath[cargoPath];
    }

    public void AddBeltExecutor(BeltExecutor beltExecutor)
    {
        foreach (var cargoPathWithOptimizedCargoPath in beltExecutor.CargoPathToOptimizedCargoPath)
        {
            _cargoPathToOptimizedCargoPath.Add(cargoPathWithOptimizedCargoPath.Key, cargoPathWithOptimizedCargoPath.Value);
        }
    }
}