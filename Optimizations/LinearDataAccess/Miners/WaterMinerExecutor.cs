using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class WaterMinerExecutor
{
    private int[] _networkIds = null!;
    private OptimizedWaterMiner[] _optimizedMiners = null!;
    public Dictionary<int, int> _minerIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public void GameTick(PlanetFactory planet,
                         int[] waterMinerPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        float miningSpeedScale = history.miningSpeedScale;
        int[] networkIds = _networkIds;
        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            float power = networkServes[networkIndex];
            ref OptimizedWaterMiner miner = ref optimizedMiners[minerIndex];
            miner.InternalUpdate(power, miningSpeedScale, productRegister);

            UpdatePower(waterMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref miner);
        }
    }

    public void UpdatePower(int[] waterMinerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;
        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            UpdatePower(waterMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref optimizedMiners[minerIndex]);
        }
    }

    private static void UpdatePower(int[] waterMinerPowerConsumerIndexes,
                                PowerConsumerType[] powerConsumerTypes,
                                long[] thisSubFactoryNetworkPowerConsumption,
                                int minerIndex,
                                int networkIndex,
                                ref OptimizedWaterMiner miner)
    {
        float num4 = (float)miner.productCount / 50f;
        num4 = ((num4 > 1f) ? 1f : num4);
        float num5 = -2.45f * num4 + 2.47f;
        num5 = ((num5 > 1f) ? 1f : num5);
        miner.speedDamper = num5;

        int powerConsumerTypeIndex = waterMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, ref miner);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] waterMinerPowerConsumerIndexes,
                                                                     PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        OptimizedWaterMiner[] optimizedMiners = _optimizedMiners;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            ref readonly OptimizedWaterMiner miner = ref optimizedMiners[minerIndex];
            UpdatePowerConsumptionPerPrototype(waterMinerPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               minerIndex,
                                               in miner);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] waterMinerPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int minerIndex,
                                                           ref readonly OptimizedWaterMiner miner)
    {
        int powerConsumerTypeIndex = waterMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[minerIndex]] += GetPowerConsumption(powerConsumerType, in miner);
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
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIds = [];
        List<OptimizedWaterMiner> optimizedMiners = [];
        Dictionary<int, int> minerIdToOptimizedIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

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

            int outputBeltId = planet.entityPool[miner.insertTarget].beltId;
            if (outputBeltId <= 0)
            {
                continue;
            }

            int outputBeltOffset = planet.cargoTraffic.beltPool[outputBeltId].pivotOnPath;
            CargoPath outputCargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[outputBeltId].segPathId];
            OptimizedCargoPath outputBelt = beltExecutor.GetOptimizedCargoPath(outputCargoPath);

            int networkIndex = planet.powerSystem.consumerPool[miner.pcId].networkId;
            subFactoryPowerSystemBuilder.AddWaterMiner(in miner, networkIndex);
            minerIdToOptimizedIndex.Add(minerIndex, optimizedMiners.Count);
            networkIds.Add(networkIndex);
            optimizedMiners.Add(new OptimizedWaterMiner(outputBelt, outputBeltOffset, planet.planet.waterItemId, in miner));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[miner.entityId]);
        }

        _networkIds = networkIds.ToArray();
        _optimizedMiners = optimizedMiners.ToArray();
        _minerIdToOptimizedIndex = minerIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly OptimizedWaterMiner miner)
    {
        EWorkState workState = miner.DetermineState();
        return powerConsumerType.GetRequiredEnergy((workState > EWorkState.Idle) ? ((double)(miner.speedDamper * (float)miner.speed * (float)miner.speed) / 100000000.0) : 0.0);
    }
}