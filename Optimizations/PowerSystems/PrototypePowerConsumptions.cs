using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.PowerSystems;

internal record struct PrototypePowerConsumptions(ReadonlyArray<int> PrototypeIds, ReadonlyArray<int> PrototypeIdCounts, long[] PrototypeIdPowerConsumption);
