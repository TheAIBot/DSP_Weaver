using System;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal readonly struct StationMinerOutput : IMinerOutput<StationMinerOutput>
{
    private readonly StationComponent _outputStation;

    public StationMinerOutput(StationComponent outputStation)
    {
        _outputStation = outputStation;
    }

    public readonly int InsertInto(int itemId, byte itemCount)
    {
        return 0;
    }

    public readonly void PrePowerUpdate<T>(ref T miner)
        where T : IMiner
    {
        StationStore[] array = _outputStation.storage;
        int num = array[0].count;
        if (array[0].localOrder < -4000)
        {
            num += array[0].localOrder + 4000;
        }
        int max = array[0].max;
        max = ((max < 3000) ? 3000 : max);
        float num2 = (float)num / (float)max;
        num2 = ((num2 > 1f) ? 1f : num2);
        float num3 = -2.45f * num2 + 2.47f;
        num3 = ((num3 > 1f) ? 1f : num3);
        miner.SpeedDamper = num3;
    }

    public readonly bool TryGetMinerOutput(PlanetFactory planet, BeltExecutor beltExecutor, ref readonly MinerComponent miner, out StationMinerOutput minerOutput)
    {
        int outputStationId = planet.entityPool[miner.entityId].stationId;
        if (outputStationId <= 0)
        {
            minerOutput = default;
            return false;
        }

        if (miner.insertTarget > 0)
        {
            throw new InvalidOperationException("""
                Current code does not use insertTarget to move items into the station. Instead the station pulls from the miner.
                The miner knows the station id because the miners entity also contains the station id.
                If insertTarget suddenly start containing something for station miners then the logic has changed and the code
                need to be changed.
                """);
        }

        StationComponent outputStation = planet.transport.stationPool[outputStationId];
        minerOutput = new StationMinerOutput(outputStation);
        return true;
    }
}
