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

    public PrototypePowerConsumptionExecutor Build(UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        return new PrototypePowerConsumptionExecutor(universeStaticDataBuilder.DeduplicateArrayUnmanaged(_prototypeIds),
                                                     universeStaticDataBuilder.DeduplicateArrayUnmanaged(_prototypeCounts),
                                                     universeStaticDataBuilder.DeduplicateArrayUnmanaged(_prototypeIdIndexes),
                                                     new long[_prototypeIds.Count]);
    }

    public PrototypePowerConsumptionExecutor Build(UniverseStaticDataBuilder universeStaticDataBuilder, int[] reorder)
    {
        return new PrototypePowerConsumptionExecutor(universeStaticDataBuilder.DeduplicateArrayUnmanaged(_prototypeIds),
                                                     universeStaticDataBuilder.DeduplicateArrayUnmanaged(_prototypeCounts),
                                                     universeStaticDataBuilder.DeduplicateArrayUnmanaged(reorder.Select(x => _prototypeIdIndexes[x]).ToArray()),
                                                     new long[_prototypeIds.Count]);
    }
}
