using System;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.PowerSystems;

internal readonly struct PrototypePowerConsumptionExecutor
{
    private readonly ReadonlyArray<int> _prototypeIds;
    private readonly ReadonlyArray<int> _prototypeIdCounts;
    public readonly ReadonlyArray<int> PrototypeIdIndexes;
    public readonly long[] PrototypeIdPowerConsumption;

    public PrototypePowerConsumptionExecutor(ReadonlyArray<int> prototypeIds,
                                             ReadonlyArray<int> prototypeIdCounts,
                                             ReadonlyArray<int> prototypeIdIndexes,
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