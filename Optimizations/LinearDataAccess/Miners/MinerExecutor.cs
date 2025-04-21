using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class MinerExecutor
{
    private int[] _minerIndexes;
    private int[] _minerNetworkIds;

    public void GameTick(PlanetFactory planet)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        VeinData[] veinPool = planet.veinPool;
        FactorySystem factorySystem = planet.factorySystem;

        float num2;
        float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
        if (num2 < 5f / 12f)
        {
            num2 = 5f / 12f;
        }
        float num4 = history.miningCostRate;
        float miningSpeedScale = history.miningSpeedScale;
        float num5 = history.miningCostRate * 0.40111667f / num2;
        if (num3 > 99.5f)
        {
            num4 = 0f;
            num5 = 0f;
        }

        int[] minerNetworkIds = _minerNetworkIds;
        int num6 = MinerComponent.InsufficientWarningThresAmount(num3, num4);
        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];

            float num7 = networkServes[minerNetworkIds[minerIndexIndex]];
            factorySystem.minerPool[minerIndex].InternalUpdate(planet, veinPool, num7, (factorySystem.minerPool[minerIndex].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        FactorySystem factory = planet.factorySystem;
        EntityData[] entityPool = planet.entityPool;
        StationComponent[] stationPool = planet.transport.stationPool;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];

            int stationId = entityPool[factory.minerPool[minerIndex].entityId].stationId;
            if (stationId > 0)
            {
                StationStore[] array = stationPool[stationId].storage;
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
                factory.minerPool[minerIndex].speedDamper = num3;
            }
            else
            {
                float num4 = (float)factory.minerPool[minerIndex].productCount / 50f;
                num4 = ((num4 > 1f) ? 1f : num4);
                float num5 = -2.45f * num4 + 2.47f;
                num5 = ((num5 > 1f) ? 1f : num5);
                factory.minerPool[minerIndex].speedDamper = num5;
            }
            factory.minerPool[minerIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _minerIndexes = subFactoryGraph.GetAllNodes()
                                       .Where(x => x.EntityTypeIndex.EntityType == EntityType.Miner)
                                       .Select(x => x.EntityTypeIndex.Index)
                                       .OrderBy(x => x)
                                       .ToArray();

        int[] minerNetworkIds = new int[_minerIndexes.Length];

        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[minerIndex];

            minerNetworkIds[minerIndexIndex] = planet.powerSystem.consumerPool[miner.pcId].networkId;
        }

        _minerNetworkIds = minerNetworkIds;
    }
}
