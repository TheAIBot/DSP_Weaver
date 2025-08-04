using System.Collections.Generic;
using Weaver.Optimizations.PowerSystems.Generators;

namespace Weaver.Optimizations.PowerSystems;

internal sealed class OptimizedPowerSystemInserterBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly SubFactoryPowerSystemBuilder _subFactoryPowerSystemBuilder;
    private readonly List<int> _inserterPowerConsumerTypeIndexes;
    private readonly Dictionary<int, OptimizedFuelGeneratorLocation> _fuelGeneratorIdToOptimizedLocation;

    public OptimizedPowerSystemInserterBuilder(PowerSystem powerSystem,
                                               SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                               List<int> inserterPowerConsumerTypeIndexes,
                                               Dictionary<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdToOptimizedLocation)
    {
        _powerSystem = powerSystem;
        _subFactoryPowerSystemBuilder = subFactoryPowerSystemBuilder;
        _inserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
        _fuelGeneratorIdToOptimizedLocation = fuelGeneratorIdToOptimizedLocation;
    }

    public void AddInserter(ref readonly InserterComponent inserter, int networkIndex)
    {
        _subFactoryPowerSystemBuilder.AddEntity(_inserterPowerConsumerTypeIndexes, inserter.pcId, networkIndex);
    }

    public OptimizedFuelGeneratorLocation GetOptimizedFuelGeneratorLocation(int fuelGeneratorId)
    {
        return _fuelGeneratorIdToOptimizedLocation[fuelGeneratorId];
    }
}