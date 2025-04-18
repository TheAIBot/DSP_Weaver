﻿using System.Collections.Generic;
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
    private readonly List<int> _producingLabPowerConsumerTypeIndexes = [];
    private readonly List<int> _researchingLabPowerConsumerTypeIndexes = [];
    private readonly List<int> _spraycoaterPowerConsumerTypeIndexes = [];
    private readonly List<int> _fractionatorPowerConsumerTypeIndexes = [];
    private readonly Dictionary<int, HashSet<int>> _networkIndexToOptimizedConsumerIndexes = [];

    public OptimizedPowerSystemBuilder(PowerSystem powerSystem)
    {
        _powerSystem = powerSystem;
    }

    public void AddAssembler(ref readonly AssemblerComponent assembler, int networkIndex)
    {
        AddEntity(_assemblerPowerConsumerTypeIndexes, assembler.pcId, networkIndex);
    }

    public OptimizedPowerSystemInserterBuilder CreateBiInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_powerSystem, this, _inserterBiPowerConsumerTypeIndexes);
    }

    public OptimizedPowerSystemInserterBuilder CreateInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_powerSystem, this, _inserterPowerConsumerTypeIndexes);
    }

    public void AddProducingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_producingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddResearchingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_researchingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddSpraycoater(ref readonly SpraycoaterComponent spraycoater, int networkIndex)
    {
        AddEntity(_spraycoaterPowerConsumerTypeIndexes, spraycoater.pcId, networkIndex);
    }

    public void AddFractionator(ref readonly FractionatorComponent fractionator, int networkIndex)
    {
        AddEntity(_fractionatorPowerConsumerTypeIndexes, fractionator.pcId, networkIndex);
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

        return new OptimizedPowerSystem(_powerConsumerTypes.ToArray(),
                                        networkNonOptimizedPowerConsumerIndexes,
                                        _assemblerPowerConsumerTypeIndexes.ToArray(),
                                        _inserterBiPowerConsumerTypeIndexes.ToArray(),
                                        _inserterPowerConsumerTypeIndexes.ToArray(),
                                        _producingLabPowerConsumerTypeIndexes.ToArray(),
                                        _researchingLabPowerConsumerTypeIndexes.ToArray(),
                                        _spraycoaterPowerConsumerTypeIndexes.ToArray(),
                                        _fractionatorPowerConsumerTypeIndexes.ToArray());
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

    private void AddEntity(List<int> powerConsumerTypeIndexes, int powerConsumerIndex, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _powerSystem.consumerPool[powerConsumerIndex];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        powerConsumerTypeIndexes.Add(GetOrAddPowerConsumerType(powerConsumerType));

        AddPowerConsumerIndexToNetwork(powerConsumerIndex, networkIndex);
    }
}