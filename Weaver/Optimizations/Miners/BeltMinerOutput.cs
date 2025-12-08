using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Miners;

internal readonly struct BeltMinerOutput : IMinerOutput<BeltMinerOutput>
{
    private readonly BeltIndex outputBeltIndex;
    private readonly int beltOffset;

    public BeltMinerOutput(BeltIndex outputBeltIndex, int beltOffset)
    {
        this.outputBeltIndex = outputBeltIndex;
        this.beltOffset = beltOffset;
    }

    public readonly int InsertInto(int itemId, byte itemCount, OptimizedCargoPath[] optimizedCargoPaths)
    {
        return outputBeltIndex.GetBelt(optimizedCargoPaths).TryInsertItem(beltOffset, itemId, itemCount, 0) ? itemCount : 0;
    }

    public readonly void PrePowerUpdate<T>(ref T miner)
        where T : IMiner
    {
        float num4 = miner.ProductCount / 50f;
        num4 = num4 > 1f ? 1f : num4;
        float num5 = -2.45f * num4 + 2.47f;
        num5 = num5 > 1f ? 1f : num5;
        miner.SpeedDamper = num5;
    }

    public readonly bool TryGetMinerOutput(PlanetFactory planet, BeltExecutor beltExecutor, ref readonly MinerComponent miner, out BeltMinerOutput minerOutput)
    {
        if (miner.insertTarget <= 0)
        {
            minerOutput = default;
            return false;
        }

        int outputBeltId = planet.entityPool[miner.insertTarget].beltId;
        if (!beltExecutor.TryGetOptimizedCargoPathIndex(planet, outputBeltId, out BeltIndex outputBeltIndex))
        {
            minerOutput = default;
            return false;
        }

        BeltComponent beltComponent = planet.cargoTraffic.beltPool[outputBeltId];
        minerOutput = new BeltMinerOutput(outputBeltIndex, beltComponent.pivotOnPath);
        return true;
    }
}
