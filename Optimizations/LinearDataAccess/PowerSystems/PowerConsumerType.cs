namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal record struct PowerConsumerType(long WorkEnergyPerTick, long IdleEnergyPerTick)
{
    public long GetRequiredEnergy(bool working, int permillage)
    {
        return working ? WorkEnergyPerTick * permillage / 1000 : IdleEnergyPerTick;
    }
}
