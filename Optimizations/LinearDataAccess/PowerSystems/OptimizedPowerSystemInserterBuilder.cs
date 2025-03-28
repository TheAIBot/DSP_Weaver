using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystemInserterBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly OptimizedPowerSystemBuilder _optimizedPowerSystemBuilder;
    private readonly List<int> _inserterPowerConsumerTypeIndexes;

    public OptimizedPowerSystemInserterBuilder(PowerSystem powerSystem, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, List<int> inserterPowerConsumerTypeIndexes)
    {
        _powerSystem = powerSystem;
        _optimizedPowerSystemBuilder = optimizedPowerSystemBuilder;
        _inserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
    }

    public void AddInserter(ref readonly InserterComponent inserter, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _powerSystem.consumerPool[inserter.pcId];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        _inserterPowerConsumerTypeIndexes.Add(_optimizedPowerSystemBuilder.GetOrAddPowerConsumerType(powerConsumerType));

        _optimizedPowerSystemBuilder.AddPowerConsumerIndexToNetwork(inserter.pcId, networkIndex);
    }
}
