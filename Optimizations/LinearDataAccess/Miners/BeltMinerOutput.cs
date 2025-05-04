using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal readonly struct BeltMinerOutput : IMinerOutput<BeltMinerOutput>
{
    private readonly OptimizedCargoPath outputBelt;

    public BeltMinerOutput(OptimizedCargoPath outputBelt)
    {
        this.outputBelt = outputBelt;
    }

    public readonly int InsertInto(int itemId, byte itemCount)
    {
        return outputBelt.TryInsertItem(0, itemId, itemCount, 0) ? itemCount : 0;
    }

    public readonly void PrePowerUpdate<T>(ref T miner)
        where T : IMiner
    {
        float num4 = (float)miner.ProductCount / 50f;
        num4 = ((num4 > 1f) ? 1f : num4);
        float num5 = -2.45f * num4 + 2.47f;
        num5 = ((num5 > 1f) ? 1f : num5);
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
        if (outputBeltId <= 0)
        {
            minerOutput = default;
            return false;
        }

        CargoPath outputCargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[outputBeltId].segPathId];
        OptimizedCargoPath outputBelt = beltExecutor.GetOptimizedCargoPath(outputCargoPath);

        minerOutput = new BeltMinerOutput(outputBelt);
        return true;
    }
}
