using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Weaver.Optimizations.PowerSystems.Generators;

internal sealed class SolarGeneratorExecutor
{
    private OptimizedSolarGenerator[] _optimizedSolarGenerators = null!;
    private Dictionary<int, int> _solarIdToOptimizedIndex = null!;
    private int _subId;
    private long? _cachedEnergySum;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount => _optimizedSolarGenerators.Length;
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public Dictionary<int, int>.KeyCollection OptimizedPowerGeneratorIds => _solarIdToOptimizedIndex.Keys;

    public long EnergyCap(float luminosity,
                          UnityEngine.Vector3 normalized,
                          bool flag2,
                          long[] currentGeneratorCapacities)
    {
        if (_optimizedSolarGenerators.Length == 0)
        {
            return 0;
        }

        // Solar is apparently not updated on each tick so the previously calculated
        // result can be reused
        if (!flag2 && _cachedEnergySum.HasValue)
        {
            currentGeneratorCapacities[_subId] += _cachedEnergySum.Value;
            return _cachedEnergySum.Value;
        }

        long energySum = 0;
        OptimizedSolarGenerator[] optimizedSolarGenerators = _optimizedSolarGenerators;
        for (int i = 0; i < optimizedSolarGenerators.Length; i++)
        {
            energySum += optimizedSolarGenerators[i].EnergyCap_PV(normalized, luminosity);
        }

        _cachedEnergySum = energySum;
        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        OptimizedSolarGenerator[] optimizedSolarGenerators = _optimizedSolarGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_solarIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedSolarGenerator optimizedSolar = ref optimizedSolarGenerators[optimizedIndex];
            optimizedSolar.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        List<OptimizedSolarGenerator> optimizedSolarGenerators = [];
        Dictionary<int, int> solarIdToOptimizedIndex = [];
        int? subId = null;
        int? prototypeId = null;

        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            ref readonly PowerGeneratorComponent powerGenerator = ref planet.powerSystem.genPool[i];
            if (powerGenerator.id != i)
            {
                continue;
            }

            if (powerGenerator.networkId != networkId)
            {
                continue;
            }

            if (!powerGenerator.photovoltaic)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all solar machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all solar machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            solarIdToOptimizedIndex.Add(powerGenerator.id, optimizedSolarGenerators.Count);
            optimizedSolarGenerators.Add(new OptimizedSolarGenerator(in powerGenerator));
        }

        _optimizedSolarGenerators = optimizedSolarGenerators.ToArray();
        _solarIdToOptimizedIndex = solarIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
    }
}
