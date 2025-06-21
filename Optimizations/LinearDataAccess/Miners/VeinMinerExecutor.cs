using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class VeinMinerExecutor<TMinerOutput>
    where TMinerOutput : struct, IMinerOutput<TMinerOutput>
{
    private int[] _networkIds = null!;
    public OptimizedVeinMiner<TMinerOutput>[] _optimizedMiners = null!;
    public Dictionary<int, int> _minerIdToOptimizedIndex = null!;

    public int GetOptimizedMinerIndexFromMinerId(int minerId)
    {
        return _minerIdToOptimizedIndex[minerId];
    }

    public void GameTick(PlanetFactory planet,
                         int[] veinMinerPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         ref MiningFlags miningFlags)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        VeinData[] veinPool = planet.veinPool;
        int[] networkIds = _networkIds;
        OptimizedVeinMiner<TMinerOutput>[] optimizedMiners = _optimizedMiners;

        float num3 = planet.gameData.gameDesc.resourceMultiplier;
        float num4 = history.miningCostRate;
        float miningSpeedScale = history.miningSpeedScale;
        if (num3 > 99.5f)
        {
            num4 = 0f;
        }

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            float power = networkServes[networkIndex];
            ref OptimizedVeinMiner<TMinerOutput> miner = ref optimizedMiners[minerIndex];
            miner.InternalUpdate(planet, veinPool, power, num4, miningSpeedScale, productRegister, ref miningFlags);

            UpdatePower(veinMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref miner);
        }
    }

    public void UpdatePower(int[] veinMinerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;
        OptimizedVeinMiner<TMinerOutput>[] optimizedMiners = _optimizedMiners;

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            UpdatePower(veinMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref optimizedMiners[minerIndex]);
        }
    }

    private static void UpdatePower(int[] veinMinerPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int minerIndex,
                                    int networkIndex,
                                    ref OptimizedVeinMiner<TMinerOutput> miner)
    {
        miner.output.PrePowerUpdate(ref miner);

        int powerConsumerTypeIndex = veinMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, ref miner);
    }

    public void Save(PlanetFactory planet)
    {
        MinerComponent[] miners = planet.factorySystem.minerPool;
        OptimizedVeinMiner<TMinerOutput>[] optimizedMiners = _optimizedMiners;
        for (int i = 1; i < planet.factorySystem.minerCursor; i++)
        {
            if (!_minerIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedMiners[optimizedIndex].Save(ref miners[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemVeinMinerBuilder optimizedPowerSystemVeinMinerBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIds = [];
        List<OptimizedVeinMiner<TMinerOutput>> optimizedMiners = [];
        Dictionary<int, int> minerIdToOptimizedIndex = [];
        TMinerOutput minerOutputBuilder = new TMinerOutput();

        foreach (int minerIndex in subFactoryGraph.GetAllNodes()
                                                  .Where(x => x.EntityTypeIndex.EntityType == EntityType.Miner)
                                                  .Select(x => x.EntityTypeIndex.Index)
                                                  .OrderBy(x => x))
        {
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[minerIndex];
            if (miner.id != minerIndex)
            {
                continue;
            }

            if (miner.type != EMinerType.Vein)
            {
                continue;
            }

            if (!minerOutputBuilder.TryGetMinerOutput(planet, beltExecutor, in miner, out TMinerOutput minerOutput))
            {
                continue;
            }

            int networkIndex = planet.powerSystem.consumerPool[miner.pcId].networkId;
            optimizedPowerSystemVeinMinerBuilder.AddMiner(in miner, networkIndex);
            minerIdToOptimizedIndex.Add(minerIndex, optimizedMiners.Count);
            networkIds.Add(networkIndex);
            optimizedMiners.Add(new OptimizedVeinMiner<TMinerOutput>(minerOutput, in miner));
        }

        _networkIds = networkIds.ToArray();
        _optimizedMiners = optimizedMiners.ToArray();
        _minerIdToOptimizedIndex = minerIdToOptimizedIndex;
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly OptimizedVeinMiner<TMinerOutput> miner)
    {
        EWorkState workState = miner.DetermineState();
        return powerConsumerType.GetRequiredEnergy((workState > EWorkState.Idle) ? ((double)(miner.SpeedDamper * (float)miner.speed * (float)miner.speed) / 100000000.0) : 0.0);
    }
}
