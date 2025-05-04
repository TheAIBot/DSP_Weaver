using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class WaterMinerExecutor
{
    private int[] _networkIds;
    private OptimizedWaterMiner[] _optimizedMiners;
    public Dictionary<int, int> _minerIdToOptimizedIndex;

    public void GameTick(PlanetFactory planet)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        float miningSpeedScale = history.miningSpeedScale;
        int[] networkIds = _networkIds;
        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;

        for (int i = 0; i < optimizedMiners.Length; i++)
        {
            float power = networkServes[networkIds[i]];
            optimizedMiners[i].InternalUpdate(power, miningSpeedScale, productRegister);
        }
    }

    public void UpdatePower(int[] waterMinerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;
        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;

        for (int j = 0; j < optimizedMiners.Length; j++)
        {
            ref OptimizedWaterMiner miner = ref optimizedMiners[j];
            float num4 = (float)miner.productCount / 50f;
            num4 = ((num4 > 1f) ? 1f : num4);
            float num5 = -2.45f * num4 + 2.47f;
            num5 = ((num5 > 1f) ? 1f : num5);
            miner.speedDamper = num5;

            int networkIndex = networkIds[j];
            int powerConsumerTypeIndex = waterMinerPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, ref miner);
        }
    }

    public void Save(PlanetFactory planet)
    {
        MinerComponent[] miners = planet.factorySystem.minerPool;
        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;
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
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIds = [];
        List<OptimizedWaterMiner> optimizedMiners = [];
        Dictionary<int, int> minerIdToOptimizedIndex = [];

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

            if (miner.type != EMinerType.Water)
            {
                continue;
            }

            if (planet.planet.waterItemId <= 0)
            {
                continue;
            }

            if (miner.insertTarget <= 0)
            {
                continue;
            }

            int inputBeltId = planet.entityPool[miner.insertTarget].beltId;
            if (inputBeltId <= 0)
            {
                continue;
            }

            CargoPath inputCargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[inputBeltId].segPathId];
            OptimizedCargoPath inputBelt = beltExecutor.GetOptimizedCargoPath(inputCargoPath);

            int networkIndex = planet.powerSystem.consumerPool[miner.pcId].networkId;
            optimizedPowerSystemBuilder.AddWaterMiner(in miner, networkIndex);
            minerIdToOptimizedIndex.Add(minerIndex, optimizedMiners.Count);
            networkIds.Add(networkIndex);
            optimizedMiners.Add(new OptimizedWaterMiner(inputBelt, planet.planet.waterItemId, in miner));
        }

        _networkIds = networkIds.ToArray();
        _optimizedMiners = optimizedMiners.ToArray();
        _minerIdToOptimizedIndex = minerIdToOptimizedIndex;

        if (_optimizedMiners.Length > 0)
        {
            WeaverFixes.Logger.LogMessage($"Water Miners: {_optimizedMiners.Length}");
        }
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly OptimizedWaterMiner miner)
    {
        EWorkState workState = miner.DetermineState();
        return powerConsumerType.GetRequiredEnergy((workState > EWorkState.Idle) ? ((double)(miner.speedDamper * (float)miner.speed * (float)miner.speed) / 100000000.0) : 0.0);
    }
}