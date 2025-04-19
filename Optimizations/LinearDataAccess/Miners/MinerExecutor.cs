using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class PilerExecutor
{
    private int[] _pilerIndexes;

    public void GameTick(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        AnimData[] entityAnimPool = planet.entityAnimPool;

        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].InternalUpdate(cargoTraffic, entityAnimPool, out float _);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _pilerIndexes = subFactoryGraph.GetAllNodes()
                                       .Where(x => x.EntityTypeIndex.EntityType == EntityType.Piler)
                                       .Select(x => x.EntityTypeIndex.Index)
                                       .OrderBy(x => x)
                                       .ToArray();
    }
}

internal sealed class MonitorExecutor
{
    private int[] _monitorIndexes;

    public void GameTick(PlanetFactory planet)
    {
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SpeakerComponent[] speakerPool = planet.digitalSystem.speakerPool;
        EntityData[] entityPool = planet.entityPool;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;
        CargoTraffic cargoTraffic = planet.cargoTraffic;

        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].InternalUpdate(cargoTraffic, sandboxToolsEnabled, entityPool, speakerPool, entityAnimPool);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _monitorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Monitor)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();
    }
}

internal sealed class SiloExecutor
{
    private int[] _siloIndexes;
    private int[] _siloNetworkIds;

    public void GameTick(PlanetFactory planet)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        FactorySystem factorySystem = planet.factorySystem;
        AstroData[] astroPoses = factorySystem.planet.galaxy.astrosData;

        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];

            float power4 = networkServes[_siloNetworkIds[siloIndexIndex]];
            factorySystem.siloPool[siloIndex].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            planet.factorySystem.siloPool[siloIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        int[] siloNetworkIds = new int[_siloIndexes.Length];

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            siloNetworkIds[siloIndexIndex] = planet.powerSystem.consumerPool[silo.pcId].networkId;

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;
        }

        _siloNetworkIds = siloNetworkIds;
    }
}

internal sealed class EjectorExecutor
{
    private int[] _ejectorIndexes;
    private int[] _ejectorNetworkIds;

    public void GameTick(PlanetFactory planet, long time)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;

        DysonSwarm swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];

            float power3 = networkServes[ejectorNetworkIds[ejectorIndexIndex]];
            planet.factorySystem.ejectorPool[ejectorIndex].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            if (planet.factorySystem.ejectorPool[ejectorIndex].id == ejectorIndex)
            {
                planet.factorySystem.ejectorPool[ejectorIndex].SetPCState(consumerPool);
            }
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _ejectorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();

        int[] ejectorNetworkIds = new int[_ejectorIndexes.Length];

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorIndex];

            ejectorNetworkIds[ejectorIndexIndex] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }
}

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
