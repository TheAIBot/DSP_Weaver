using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Silos;

internal sealed class SiloExecutor
{
    private int[] _siloIndexes = null!;
    private int[] _siloNetworkIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public void GameTick(PlanetFactory planet,
                         int[] siloPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        int[] siloIndexes = _siloIndexes;
        int[] siloNetworkIds = _siloNetworkIds;
        SiloComponent[] silos = planet.factorySystem.siloPool;

        DysonSphere dysonSphere = planet.factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = siloIndexes[siloIndexIndex];
            int networkIndex = siloNetworkIds[siloIndexIndex];
            float power4 = networkServes[networkIndex];
            ref SiloComponent silo = ref silos[siloIndex];
            silo.InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);

            UpdatePower(siloPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, siloIndexIndex, networkIndex, in silo);
        }
    }

    public void UpdatePower(PlanetFactory planet,
                            int[] siloPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] siloIndexes = _siloIndexes;
        int[] siloNetworkIds = _siloNetworkIds;
        SiloComponent[] silos = planet.factorySystem.siloPool;

        for (int siloIndexIndex = 0; siloIndexIndex < siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = siloIndexes[siloIndexIndex];
            int networkIndex = siloNetworkIds[siloIndexIndex];
            ref readonly SiloComponent silo = ref silos[siloIndex];
            UpdatePower(siloPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, siloIndexIndex, networkIndex, in silo);
        }
    }

    private static void UpdatePower(int[] siloPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int siloIndexIndex,
                                    int networkIndex,
                                    ref readonly SiloComponent silo)
    {
        int powerConsumerTypeIndex = siloPowerConsumerTypeIndexes[siloIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, in silo);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(PlanetFactory planet,
                                                                         int[] siloPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] siloIndexes = _siloIndexes;
        SiloComponent[] silos = planet.factorySystem.siloPool;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int siloIndexIndex = 0; siloIndexIndex < siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = siloIndexes[siloIndexIndex];
            ref readonly SiloComponent silo = ref silos[siloIndex];
            UpdatePowerConsumptionPerPrototype(siloPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               siloIndexIndex,
                                               in silo);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] siloPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int siloIndexIndex,
                                                           ref readonly SiloComponent silo)
    {
        int powerConsumerTypeIndex = siloPowerConsumerTypeIndexes[siloIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[siloIndexIndex]] += GetPowerConsumption(powerConsumerType, in silo);
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        int[] siloNetworkIds = new int[_siloIndexes.Length];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            int networkIndex = planet.powerSystem.consumerPool[silo.pcId].networkId;
            siloNetworkIds[siloIndexIndex] = networkIndex;

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;

            subFactoryPowerSystemBuilder.AddSilo(in silo, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[silo.entityId]);
        }

        _siloNetworkIds = siloNetworkIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly SiloComponent silo)
    {
        return powerConsumerType.GetRequiredEnergy(silo.direction != 0, 1000 + Cargo.powerTable[silo.incLevel]);
    }
}
