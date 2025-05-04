namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal struct MiningFlags
{
    public int MiningFlag;
    public int VeinMiningFlag;

    public MiningFlags()
    {
        MiningFlag = 0;
        VeinMiningFlag = 0;
    }

    public void AddMiningFlagUnsafe(EVeinType addVeinType)
    {
        MiningFlag |= 1 << (int)addVeinType;
    }

    public void AddVeinMiningFlagUnsafe(EVeinType addVeinType)
    {
        VeinMiningFlag |= 1 << (int)addVeinType;
    }
}
