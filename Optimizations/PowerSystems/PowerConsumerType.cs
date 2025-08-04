namespace Weaver.Optimizations.PowerSystems;

internal record struct PowerConsumerType(long WorkEnergyPerTick, long IdleEnergyPerTick)
{
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
}
