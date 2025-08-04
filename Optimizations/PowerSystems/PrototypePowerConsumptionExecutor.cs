using System;

namespace Weaver.Optimizations.PowerSystems;

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