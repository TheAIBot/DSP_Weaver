using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.PowerSystems;

internal readonly struct PowerConsumerType : IEquatable<PowerConsumerType>, IMemorySize
{
    public readonly long WorkEnergyPerTick;
    public readonly long IdleEnergyPerTick;

    public PowerConsumerType(long workEnergyPerTick, 
                             long idleEnergyPerTick)
    {
        WorkEnergyPerTick = workEnergyPerTick;
        IdleEnergyPerTick = idleEnergyPerTick;
    }

    public readonly long GetRequiredEnergy(bool working, int permillage)
    {
        return working ? WorkEnergyPerTick * permillage / 1000 : IdleEnergyPerTick;
    }

    public readonly long GetRequiredEnergy(bool working)
    {
        return working ? WorkEnergyPerTick : IdleEnergyPerTick;
    }

    public readonly long GetRequiredEnergy(double ratio)
    {
        return (long)(WorkEnergyPerTick * ratio + IdleEnergyPerTick * (1.0 - ratio));
    }

    public unsafe int GetSize() => Marshal.SizeOf<PowerConsumerType>();

    public readonly bool Equals(PowerConsumerType other)
    {
        return WorkEnergyPerTick == other.WorkEnergyPerTick &&
               IdleEnergyPerTick == other.IdleEnergyPerTick;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is PowerConsumerType other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(WorkEnergyPerTick, IdleEnergyPerTick);
    }
}
