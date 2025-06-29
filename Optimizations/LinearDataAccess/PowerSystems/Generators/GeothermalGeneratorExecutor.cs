using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class GeothermalGeneratorExecutor
{
    private OptimizedGeothermalGenerator[] _optimizedGeothermalGenerators = null!;
    private Dictionary<int, int> _geothermalIdToOptimizedIndex = null!;
    private int _subId;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount => _optimizedGeothermalGenerators.Length;
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public Dictionary<int, int>.KeyCollection OptimizedPowerGeneratorIds => _geothermalIdToOptimizedIndex.Keys;

    public long EnergyCap(long[] currentGeneratorCapacities)
    {
        if (_optimizedGeothermalGenerators.Length == 0)
        {
            return 0;
        }

        long energySum = 0;
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 0; i < optimizedGeothermalGenerators.Length; i++)
        {
            energySum += optimizedGeothermalGenerators[i].EnergyCap_GTH();
        }

        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void GameTick()
    {
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 0; i < optimizedGeothermalGenerators.Length; i++)
        {
            optimizedGeothermalGenerators[i].GeneratePower();
        }
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_geothermalIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedGeothermalGenerator optimizedGeothermal = ref optimizedGeothermalGenerators[optimizedIndex];
            optimizedGeothermal.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        List<OptimizedGeothermalGenerator> optimizedGeothermalGenerators = [];
        Dictionary<int, int> geothermalIdToOptimizedIndex = [];
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

            if (!powerGenerator.geothermal)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all geothermal machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all geothermal machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            geothermalIdToOptimizedIndex.Add(powerGenerator.id, optimizedGeothermalGenerators.Count);
            optimizedGeothermalGenerators.Add(new OptimizedGeothermalGenerator(in powerGenerator));
        }

        _optimizedGeothermalGenerators = optimizedGeothermalGenerators.ToArray();
        _geothermalIdToOptimizedIndex = geothermalIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
    }
}
