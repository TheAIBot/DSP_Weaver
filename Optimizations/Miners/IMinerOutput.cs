using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Miners;

internal interface IMinerOutput<T>
    where T : IMinerOutput<T>
{
    int InsertInto(int itemId, byte itemCount);

    void PrePowerUpdate<TMiner>(ref TMiner miner) where TMiner : IMiner;

    bool TryGetMinerOutput(PlanetFactory planet, BeltExecutor beltExecutor, ref readonly MinerComponent miner, out T minerOutput);
}
