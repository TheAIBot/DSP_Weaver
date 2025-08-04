using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Miners;

internal readonly struct BeltMinerOutput : IMinerOutput<BeltMinerOutput>
{
    private readonly OptimizedCargoPath outputBelt;
    private readonly int beltOffset;

    public BeltMinerOutput(OptimizedCargoPath outputBelt, int beltOffset)
    {
        this.outputBelt = outputBelt;
        this.beltOffset = beltOffset;
    }

    public readonly int InsertInto(int itemId, byte itemCount)
    {
        return outputBelt.TryInsertItem(beltOffset, itemId, itemCount, 0) ? itemCount : 0;
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
        if (!beltExecutor.TryOptimizedCargoPath(planet, outputBeltId, out OptimizedCargoPath? outputBelt))
        {
            minerOutput = default;
            return false;
        }

        BeltComponent beltComponent = planet.cargoTraffic.beltPool[outputBeltId];
        minerOutput = new BeltMinerOutput(outputBelt, beltComponent.pivotOnPath);
        return true;
    }
}
