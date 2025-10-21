using System.Collections.Generic;

namespace Weaver.Optimizations.PowerSystems;

internal sealed class OptimizedPowerSystemVeinMinerBuilder
{
    private readonly SubFactoryPowerSystemBuilder _subFactoryPowerSystemBuilder;
    private readonly List<int> _veinMinerPowerConsumerTypeIndexes;

    public OptimizedPowerSystemVeinMinerBuilder(PowerSystem powerSystem, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, List<int> veinMinerPowerConsumerTypeIndexes)
    {
        _subFactoryPowerSystemBuilder = subFactoryPowerSystemBuilder;
        _veinMinerPowerConsumerTypeIndexes = veinMinerPowerConsumerTypeIndexes;
    }

    public void AddMiner(ref readonly MinerComponent miner, int networkIndex)
    {
        _subFactoryPowerSystemBuilder.AddEntity(_veinMinerPowerConsumerTypeIndexes, miner.pcId, networkIndex);
    }
}
