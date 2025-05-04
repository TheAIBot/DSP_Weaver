namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal record struct PowerConsumerType(long WorkEnergyPerTick, long IdleEnergyPerTick)
{
    public long GetRequiredEnergy(bool working, int permillage)
    {
        return working ? WorkEnergyPerTick * permillage / 1000 : IdleEnergyPerTick;
    }

    public long GetRequiredEnergy(bool working)
    {
        return (working ? WorkEnergyPerTick : IdleEnergyPerTick);
    }

    public long GetRequiredEnergy(double ratio)
    {
        return (long)((double)WorkEnergyPerTick * ratio + (double)IdleEnergyPerTick * (1.0 - ratio));
    }
}
