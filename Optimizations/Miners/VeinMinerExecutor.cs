using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Miners;

internal sealed class VeinMinerExecutor<TMinerOutput>
    where TMinerOutput : struct, IMinerOutput<TMinerOutput>
{
    private int[] _networkIds = null!;
    public OptimizedVeinMiner<TMinerOutput>[] _optimizedMiners = null!;
    public Dictionary<int, int> _minerIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int Count => _optimizedMiners.Length;

    public int GetOptimizedMinerIndexFromMinerId(int minerId)
    {
        return _minerIdToOptimizedIndex[minerId];
    }

    public void GameTick(PlanetFactory planet,
                         int[] veinMinerPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] productRegister,
                         ref MiningFlags miningFlags,
                         OptimizedCargoPath[] optimizedCargoPaths)
    {
        GameHistoryData history = GameMain.history;
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
            miner.InternalUpdate(planet, veinPool, power, num4, miningSpeedScale, productRegister, ref miningFlags, optimizedCargoPaths);

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

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] veinMinerPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        OptimizedVeinMiner<TMinerOutput>[] optimizedMiners = _optimizedMiners;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int minerIndex = 0; minerIndex < optimizedMiners.Length; minerIndex++)
        {
            ref readonly OptimizedVeinMiner<TMinerOutput> miner = ref optimizedMiners[minerIndex];
            UpdatePowerConsumptionPerPrototype(veinMinerPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               minerIndex,
                                               in miner);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] veinMinerPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int minerIndex,
                                                           ref readonly OptimizedVeinMiner<TMinerOutput> miner)
    {
        int powerConsumerTypeIndex = veinMinerPowerConsumerIndexes[minerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[minerIndex]] += GetPowerConsumption(powerConsumerType, in miner);
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
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIds = [];
        List<OptimizedVeinMiner<TMinerOutput>> optimizedMiners = [];
        Dictionary<int, int> minerIdToOptimizedIndex = [];
        TMinerOutput minerOutputBuilder = new TMinerOutput();
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

            if (miner.type != EMinerType.Vein)
            {
                continue;
            }

            if (!minerOutputBuilder.TryGetMinerOutput(planet, beltExecutor, in miner, out TMinerOutput minerOutput))
            {
                continue;
            }

            var veinProducts = new OptimizedItemId[miner.veinCount];
            for (int i = 0; i < veinProducts.Length; i++)
            {
                veinProducts[i] = subFactoryProductionRegisterBuilder.AddProduct(planet.veinPool[miner.veins[i]].productId);
            }

            OptimizedItemId veinProductId = default;
            if (miner.productId > 0)
            {
                veinProductId = subFactoryProductionRegisterBuilder.AddProduct(miner.productId);
            }

            int networkIndex = planet.powerSystem.consumerPool[miner.pcId].networkId;
            optimizedPowerSystemVeinMinerBuilder.AddMiner(in miner, networkIndex);
            minerIdToOptimizedIndex.Add(minerIndex, optimizedMiners.Count);
            networkIds.Add(networkIndex);
            optimizedMiners.Add(new OptimizedVeinMiner<TMinerOutput>(minerOutput, veinProducts, veinProductId, in miner));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[miner.entityId]);
        }

        _networkIds = networkIds.ToArray();
        _optimizedMiners = optimizedMiners.ToArray();
        _minerIdToOptimizedIndex = minerIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly OptimizedVeinMiner<TMinerOutput> miner)
    {
        EWorkState workState = miner.DetermineState();
        return powerConsumerType.GetRequiredEnergy(workState > EWorkState.Idle ? (double)(miner.SpeedDamper * miner.speed * miner.speed) / 100000000.0 : 0.0);
    }
}
