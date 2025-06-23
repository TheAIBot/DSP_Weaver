using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal readonly struct PrototypePowerConsumptionExecutor
{
    private readonly int[] _prototypeIds;
    private readonly int[] _prototypeIdCounts;
    public readonly int[] PrototypeIdIndexes;
    public readonly long[] PrototypeIdPowerConsumption;

    public PrototypePowerConsumptionExecutor(int[] prototypeIds,
                                             int[] prototypeIdCounts,
                                             int[] prototypeIdIndexes,
                                             long[] prototypeIdPowerConsumption)
    {
        _prototypeIds = prototypeIds;
        _prototypeIdCounts = prototypeIdCounts;
        PrototypeIdIndexes = prototypeIdIndexes;
        PrototypeIdPowerConsumption = prototypeIdPowerConsumption;
    }

    public PrototypePowerConsumptions GetPowerConsumption()
    {
        return new PrototypePowerConsumptions(_prototypeIds, _prototypeIdCounts, PrototypeIdPowerConsumption);
    }

    public void Clear()
    {
        Array.Clear(PrototypeIdPowerConsumption, 0, PrototypeIdPowerConsumption.Length);
    }
}

internal sealed class PrototypePowerConsumptionBuilder
{
    private readonly Dictionary<int, int> _prototypeIdToIndex = [];
    private readonly List<int> _prototypeIds = [];
    private readonly List<int> _prototypeCounts = [];
    private readonly List<int> _prototypeIdIndexes = [];

    public void AddPowerConsumer(ref readonly EntityData entity)
    {
        int entityPrototypeId = entity.protoId;
        if (!_prototypeIdToIndex.TryGetValue(entityPrototypeId, out int prototypeIndex))
        {
            prototypeIndex = _prototypeIds.Count;
            _prototypeIds.Add(entityPrototypeId);
            _prototypeIdToIndex.Add(entityPrototypeId, prototypeIndex);
            _prototypeCounts.Add(0);
        }

        _prototypeIdIndexes.Add(prototypeIndex);
        _prototypeCounts[prototypeIndex]++;
    }

    public PrototypePowerConsumptionExecutor Build()
    {
        return new PrototypePowerConsumptionExecutor(_prototypeIds.ToArray(),
                                                     _prototypeCounts.ToArray(),
                                                     _prototypeIdIndexes.ToArray(),
                                                     new long[_prototypeIds.Count]);
    }
}
