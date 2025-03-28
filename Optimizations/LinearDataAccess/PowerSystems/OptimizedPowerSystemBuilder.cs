using System.Collections.Generic;
using System.Linq;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystemBuilder
{
    private readonly PowerSystem _powerSystem;
    private readonly List<PowerConsumerType> _powerConsumerTypes = [];
    private readonly Dictionary<PowerConsumerType, int> _powerConsumerTypeToIndex = [];
    private readonly List<int> _assemblerPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterBiPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterPowerConsumerTypeIndexes = [];
    private readonly Dictionary<int, HashSet<int>> _networkIndexToOptimizedConsumerIndexes = [];

    public OptimizedPowerSystemBuilder(PowerSystem powerSystem)
    {
        _powerSystem = powerSystem;
    }

    public void AddAssembler(ref readonly AssemblerComponent assembler, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _powerSystem.consumerPool[assembler.pcId];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        _assemblerPowerConsumerTypeIndexes.Add(GetOrAddPowerConsumerType(powerConsumerType));

        AddPowerConsumerIndexToNetwork(assembler.pcId, networkIndex);
    }

    public OptimizedPowerSystemInserterBuilder CreateBiInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_powerSystem, this, _inserterBiPowerConsumerTypeIndexes);
    }

    public OptimizedPowerSystemInserterBuilder CreateInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_powerSystem, this, _inserterPowerConsumerTypeIndexes);
    }

    public OptimizedPowerSystem Build()
    {
        int[][] networkNonOptimizedPowerConsumerIndexes = new int[_powerSystem.netCursor][];
        for (int i = 0; i < networkNonOptimizedPowerConsumerIndexes.Length; i++)
        {
            if (!_networkIndexToOptimizedConsumerIndexes.TryGetValue(i, out HashSet<int> optimizedConsumerIndexes))
            {
                networkNonOptimizedPowerConsumerIndexes[i] = _powerSystem.netPool[i].consumers.ToArray();
                continue;
            }

            networkNonOptimizedPowerConsumerIndexes[i] = _powerSystem.netPool[i].consumers.Except(optimizedConsumerIndexes).ToArray();
        }

        WeaverFixes.Logger.LogMessage($"PowerConsumerTypes Count: {_powerConsumerTypes.Count}");
        return new OptimizedPowerSystem(_powerConsumerTypes.ToArray(),
                                        networkNonOptimizedPowerConsumerIndexes,
                                        _assemblerPowerConsumerTypeIndexes.ToArray(),
                                        _inserterBiPowerConsumerTypeIndexes.ToArray(),
                                        _inserterPowerConsumerTypeIndexes.ToArray());
    }

    public int GetOrAddPowerConsumerType(PowerConsumerType powerConsumerType)
    {
        if (!_powerConsumerTypeToIndex.TryGetValue(powerConsumerType, out int index))
        {
            _powerConsumerTypeToIndex.Add(powerConsumerType, _powerConsumerTypes.Count);
            index = _powerConsumerTypes.Count;
            _powerConsumerTypes.Add(powerConsumerType);
        }

        return index;
    }

    public void AddPowerConsumerIndexToNetwork(int powerConsumerIndex, int networkIndex)
    {
        if (!_networkIndexToOptimizedConsumerIndexes.TryGetValue(networkIndex, out HashSet<int> optimizedConsumerIndexes))
        {
            optimizedConsumerIndexes = [];
            _networkIndexToOptimizedConsumerIndexes.Add(networkIndex, optimizedConsumerIndexes);
        }

        optimizedConsumerIndexes.Add(powerConsumerIndex);
    }
}