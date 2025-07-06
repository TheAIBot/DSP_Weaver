using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;

internal sealed class WindGeneratorExecutor
{
    private OptimizedWindGeneratorGroup[] _optimizedWindGeneratorGroups = null!;
    private List<int> _optimizedWindIndexes = null!;
    private int _subId;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount { get; private set; }
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public IEnumerable<int> OptimizedPowerGeneratorIds => _optimizedWindIndexes;

    public long EnergyCap(float windStrength, long[] currentGeneratorCapacities)
    {
        if (_optimizedWindGeneratorGroups.Length == 0)
        {
            return 0;
        }

        long energySum = 0;
        OptimizedWindGeneratorGroup[] optimizedWindGeneratorGroups = _optimizedWindGeneratorGroups;
        for (int i = 0; i < optimizedWindGeneratorGroups.Length; i++)
        {
            energySum += optimizedWindGeneratorGroups[i].EnergyCap_Wind(windStrength);
        }

        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void Save(PlanetFactory planet)
    {
        // There is nothing to save for wind power
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        Dictionary<long, int> optimizedWindGeneratorGroupToCount = [];
        List<int> optimizedWindIndexes = [];
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

            if (!powerGenerator.wind)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all wind machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all wind machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            optimizedWindIndexes.Add(powerGenerator.id);
            if (!optimizedWindGeneratorGroupToCount.TryGetValue(powerGenerator.genEnergyPerTick, out int groupCount))
            {
                optimizedWindGeneratorGroupToCount.Add(powerGenerator.genEnergyPerTick, 0);
            }
            optimizedWindGeneratorGroupToCount[powerGenerator.genEnergyPerTick]++;
        }

        _optimizedWindGeneratorGroups = optimizedWindGeneratorGroupToCount.Select(x => new OptimizedWindGeneratorGroup(x.Key, x.Value)).ToArray();
        _optimizedWindIndexes = optimizedWindIndexes;
        _subId = subId ?? -1;
        GeneratorCount = optimizedWindGeneratorGroupToCount.Values.Sum();
        PrototypeId = prototypeId;
    }
}
