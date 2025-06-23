using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystemInserterBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly SubFactoryPowerSystemBuilder _subFactoryPowerSystemBuilder;
    private readonly List<int> _inserterPowerConsumerTypeIndexes;

    public OptimizedPowerSystemInserterBuilder(PowerSystem powerSystem,
                                               SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                               List<int> inserterPowerConsumerTypeIndexes)
    {
        _powerSystem = powerSystem;
        _subFactoryPowerSystemBuilder = subFactoryPowerSystemBuilder;
        _inserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
    }

    public void AddInserter(ref readonly InserterComponent inserter, int networkIndex)
    {
        _subFactoryPowerSystemBuilder.AddEntity(_inserterPowerConsumerTypeIndexes, inserter.pcId, networkIndex);
    }
}
