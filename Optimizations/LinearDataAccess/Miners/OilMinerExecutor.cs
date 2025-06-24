using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class OilMinerExecutor
{
    private int[] _networkIds = null!;
    private OptimizedOilMiner[] _optimizedMiners = null!;
    public Dictionary<int, int> _minerIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public void GameTick(PlanetFactory planet,
                         int[] oilMinerPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        float miningSpeedScale = history.miningSpeedScale;
        VeinData[] veinPool = planet.veinPool;
        int[] networkIds = _networkIds;
        OptimizedOilMiner[] optimizedMiners = _optimizedMiners;

        float num2;
        float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
        if (num2 < 5f / 12f)
        {
            num2 = 5f / 12f;
        }
        float num5 = history.miningCostRate * 0.40111667f / num2;
        if (num3 > 99.5f)
        {
            num5 = 0f;
        }

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            float power = networkServes[networkIndex];
            ref OptimizedOilMiner miner = ref optimizedMiners[minerIndex];
            miner.InternalUpdate(planet, veinPool, power, num5, miningSpeedScale, productRegister);

            UpdatePower(oilMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref miner);
        }
    }

    public void UpdatePower(int[] oilMinerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIds = _networkIds;
        OptimizedOilMiner[] optimizedMiners = _optimizedMiners;

        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            int networkIndex = networkIds[minerIndex];
            UpdatePower(oilMinerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, minerIndex, networkIndex, ref optimizedMiners[minerIndex]);
        }
    }

    private static void UpdatePower(int[] oilMinerPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int minerIndex,
                                    int networkIndex,
                                    ref OptimizedOilMiner miner)
    {
        float num4 = (float)miner.productCount / 50f;
        num4 = ((num4 > 1f) ? 1f : num4);
        float num5 = -2.45f * num4 + 2.47f;
        num5 = ((num5 > 1f) ? 1f : num5);
        miner.speedDamper = num5;

        int powerConsumerTypeIndex = oilMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, ref miner);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] oilMinerPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        OptimizedOilMiner[] optimizedMiners = _optimizedMiners;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            ref readonly OptimizedOilMiner miner = ref optimizedMiners[minerIndex];
            UpdatePowerConsumptionPerPrototype(oilMinerPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               minerIndex,
                                               in miner);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] oilMinerPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int minerIndex,
                                                           ref readonly OptimizedOilMiner miner)
    {
        int powerConsumerTypeIndex = oilMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[minerIndex]] += GetPowerConsumption(powerConsumerType, in miner);
    }

    public void Save(PlanetFactory planet)
    {
        MinerComponent[] miners = planet.factorySystem.minerPool;
        OptimizedOilMiner[] optimizedMiners = _optimizedMiners;
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
        List<OptimizedOilMiner> optimizedMiners = [];
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

            if (miner.type != EMinerType.Oil)
            {
                continue;
            }

            if (miner.veinCount == 0)
            {
                continue;
            }
            int productId = planet.veinPool[miner.veins[0]].productId;

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
            subFactoryPowerSystemBuilder.AddOilMiner(in miner, networkIndex);
            minerIdToOptimizedIndex.Add(minerIndex, optimizedMiners.Count);
            networkIds.Add(networkIndex);
            optimizedMiners.Add(new OptimizedOilMiner(outputBelt, outputBeltOffset, productId, in miner));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[miner.entityId]);
        }

        _networkIds = networkIds.ToArray();
        _optimizedMiners = optimizedMiners.ToArray();
        _minerIdToOptimizedIndex = minerIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly OptimizedOilMiner miner)
    {
        EWorkState workState = miner.DetermineState();
        return powerConsumerType.GetRequiredEnergy((workState > EWorkState.Idle) ? ((double)(miner.speedDamper * (float)miner.speed * (float)miner.speed) / 100000000.0) : 0.0);
    }
}
