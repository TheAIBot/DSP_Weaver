using System.Collections.Generic;

namespace Weaver.Optimizations.Belts;

internal sealed class PlanetWideBeltExecutor
{
    private readonly Dictionary<CargoPath, OptimizedIndexedCargoPath> _cargoPathToOptimizedCargoPath = [];

    public OptimizedIndexedCargoPath GetOptimizedCargoPath(CargoPath cargoPath)
    {
        return _cargoPathToOptimizedCargoPath[cargoPath];
    }

    public void AddBeltExecutor(BeltExecutor beltExecutor)
    {
        foreach (KeyValuePair<CargoPath, BeltIndex> cargoPathWithOptimizedCargoPath in beltExecutor.CargoPathToOptimizedCargoPathIndex)
        {
            _cargoPathToOptimizedCargoPath.Add(cargoPathWithOptimizedCargoPath.Key, new OptimizedIndexedCargoPath(beltExecutor.OptimizedCargoPaths, cargoPathWithOptimizedCargoPath.Value));
        }
    }
}