using System.Collections.Generic;
using System.Linq;

namespace Weaver.Optimizations.PowerSystems;

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

    public PrototypePowerConsumptionExecutor Build(int[] reorder)
    {
        return new PrototypePowerConsumptionExecutor(_prototypeIds.ToArray(),
                                                     _prototypeCounts.ToArray(),
                                                     reorder.Select(x => _prototypeIdIndexes[x]).ToArray(),
                                                     new long[_prototypeIds.Count]);
    }
}
