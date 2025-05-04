using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal class OptimizedPowerSystemVeinMinerBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly OptimizedPowerSystemBuilder _optimizedPowerSystemBuilder;
    private readonly List<int> _veinMinerPowerConsumerTypeIndexes;

    public OptimizedPowerSystemVeinMinerBuilder(PowerSystem powerSystem, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, List<int> veinMinerPowerConsumerTypeIndexes)
    {
        _powerSystem = powerSystem;
        _optimizedPowerSystemBuilder = optimizedPowerSystemBuilder;
        _veinMinerPowerConsumerTypeIndexes = veinMinerPowerConsumerTypeIndexes;
    }

    public void AddMiner(ref readonly MinerComponent miner, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _powerSystem.consumerPool[miner.pcId];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        _veinMinerPowerConsumerTypeIndexes.Add(_optimizedPowerSystemBuilder.GetOrAddPowerConsumerType(powerConsumerType));

        _optimizedPowerSystemBuilder.AddPowerConsumerIndexToNetwork(miner.pcId, networkIndex);
    }
}
