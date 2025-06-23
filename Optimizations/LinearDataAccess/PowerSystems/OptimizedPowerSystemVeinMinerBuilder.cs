using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal class OptimizedPowerSystemVeinMinerBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly SubFactoryPowerSystemBuilder _subFactoryPowerSystemBuilder;
    private readonly List<int> _veinMinerPowerConsumerTypeIndexes;

    public OptimizedPowerSystemVeinMinerBuilder(PowerSystem powerSystem, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, List<int> veinMinerPowerConsumerTypeIndexes)
    {
        _powerSystem = powerSystem;
        _subFactoryPowerSystemBuilder = subFactoryPowerSystemBuilder;
        _veinMinerPowerConsumerTypeIndexes = veinMinerPowerConsumerTypeIndexes;
    }

    public void AddMiner(ref readonly MinerComponent miner, int networkIndex)
    {
        _subFactoryPowerSystemBuilder.AddEntity(_veinMinerPowerConsumerTypeIndexes, miner.pcId, networkIndex);
    }
}
