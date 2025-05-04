using System;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal readonly struct StationMinerOutput : IMinerOutput<StationMinerOutput>
{
    private readonly StationComponent outputStation;
    private readonly int[] needs;

    public StationMinerOutput(StationComponent outputStation, int[] needs)
    {
        this.outputStation = outputStation;
        this.needs = needs;
    }

    public readonly int InsertInto(int itemId, byte itemCount)
    {
        if (itemId == 1210 && outputStation.warperCount < outputStation.warperMaxCount)
        {
            outputStation.warperCount += itemCount;
            return itemCount;
        }
        StationStore[] storage = outputStation.storage;
        for (int j = 0; j < needs.Length && j < storage.Length; j++)
        {
            if (needs[j] == itemId && storage[j].itemId == itemId)
            {
                storage[j].count += itemCount;
                return itemCount;
            }
        }

        return 0;
    }

    public readonly void PrePowerUpdate<T>(ref T miner)
        where T : IMiner
    {
        StationStore[] array = outputStation.storage;
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
        if (miner.insertTarget <= 0)
        {
            minerOutput = default;
            return false;
        }


        int outputStationId = planet.entityPool[miner.insertTarget].stationId;
        if (outputStationId <= 0)
        {
            minerOutput = default;
            return false;
        }

        StationComponent outputStation = planet.transport.stationPool[outputStationId];
        int[] stationNeeds = outputStation.needs;// planet.entityNeeds[outputStation.entityId];
        if (stationNeeds == null)
        {
            throw new InvalidOperationException("Miner station needs is null.");
        }

        minerOutput = new StationMinerOutput(outputStation, stationNeeds);
        return true;
    }
}
