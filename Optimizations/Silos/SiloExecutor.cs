using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Silos;

internal sealed class SiloExecutor
{
    public int[] _siloIndexes = null!;
    private short[] _optimizedBulletItemId = null!;
    private int[] _siloNetworkIds = null!;
    private Dictionary<int, int> _siloIdToOptimizedSiloIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;
    public const int SoleSiloNeedsIndex = 0;

    public int Count => _siloIndexes.Length;

    public int GetOptimizedSiloIndex(int siloId)
    {
        return _siloIdToOptimizedSiloIndex[siloId];
    }

    public void GameTick(PlanetFactory planet,
                         short[] siloPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] consumeRegister,
                         SubFactoryNeeds subFactoryNeeds)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] siloIndexes = _siloIndexes;
        int[] siloNetworkIds = _siloNetworkIds;
        SiloComponent[] silos = planet.factorySystem.siloPool;
        short[] optimizedBulletItemId = _optimizedBulletItemId;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Silo);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;

        DysonSphere dysonSphere = planet.factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = siloIndexes[siloIndexIndex];
            short optimizedBulletId = optimizedBulletItemId[siloIndexIndex];
            int needsOffset = groupNeeds.GetObjectNeedsIndex(siloIndexIndex);
            int networkIndex = siloNetworkIds[siloIndexIndex];
            float power4 = networkServes[networkIndex];
            ref SiloComponent silo = ref silos[siloIndex];
            InternalUpdate(ref silo, power4, dysonSphere, optimizedBulletId, consumeRegister, componentsNeeds, needsOffset);

            UpdatePower(siloPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, siloIndexIndex, networkIndex, in silo);
        }
    }

    public void UpdatePower(PlanetFactory planet,
                            short[] siloPowerConsumerTypeIndexes,
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

    private static void UpdatePower(short[] siloPowerConsumerTypeIndexes,
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
                                                                         short[] siloPowerConsumerTypeIndexes,
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

    private static void UpdatePowerConsumptionPerPrototype(short[] siloPowerConsumerTypeIndexes,
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

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        int[] siloIndexes = _siloIndexes;
        SiloComponent[] silos = planet.factorySystem.siloPool;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Silo);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        short[] needsPatterns = subFactoryNeeds.NeedsPatterns;

        for (int siloIndexIndex = 0; siloIndexIndex < siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = siloIndexes[siloIndexIndex];
            int needsOffset = groupNeeds.GetObjectNeedsIndex(siloIndexIndex);
            ComponentNeeds componentNeeds = componentsNeeds[needsOffset];
            ref SiloComponent silo = ref silos[siloIndex];

            for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
            {
                GroupNeeds.SetNeedsIfInRange(silo.needs, componentNeeds, needsPatterns, i);
            }
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        short[] optimizedBulletItemId = new short[_siloIndexes.Length];
        int[] siloNetworkIds = new int[_siloIndexes.Length];
        Dictionary<int, int> siloIdToOptimizedSiloIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.Silo);

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            optimizedBulletItemId[siloIndexIndex] = subFactoryProductionRegisterBuilder.AddConsume(silo.bulletId).OptimizedItemIndex;
            int networkIndex = planet.powerSystem.consumerPool[silo.pcId].networkId;
            siloNetworkIds[siloIndexIndex] = networkIndex;
            siloIdToOptimizedSiloIndex.Add(siloIndex, siloIndexIndex);

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;
            needsBuilder.AddNeeds(silo.needs, [silo.bulletId]);

            subFactoryPowerSystemBuilder.AddSilo(in silo, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[silo.entityId]);
        }

        _optimizedBulletItemId = optimizedBulletItemId;
        _siloNetworkIds = siloNetworkIds;
        _siloIdToOptimizedSiloIndex = siloIdToOptimizedSiloIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly SiloComponent silo)
    {
        return powerConsumerType.GetRequiredEnergy(silo.direction != 0, 1000 + Cargo.powerTable[silo.incLevel]);
    }

    private static uint InternalUpdate(ref SiloComponent silo,
                                       float power,
                                       DysonSphere sphere,
                                       short optimizedBulletId,
                                       int[] consumeRegister,
                                       ComponentNeeds[] componentsNeeds,
                                       int needsOffset)
    {
        componentsNeeds[needsOffset + SoleSiloNeedsIndex].Needs = (byte)(silo.bulletCount < 20 ? 1 : 0);
        if (silo.fired && silo.direction != -1)
        {
            silo.fired = false;
        }
        float num = (float)Cargo.accTableMilli[silo.incLevel];
        int num2 = (int)(power * 10000f * (1f + num) + 0.1f);
        if (silo.boost)
        {
            num2 *= 10;
        }
        lock (sphere.dysonSphere_mx)
        {
            silo.hasNode = sphere.GetAutoNodeCount() > 0;
            if (!silo.hasNode)
            {
                silo.autoIndex = 0;
                if (silo.direction == 1)
                {
                    silo.time = (int)(silo.time * (long)silo.coldSpend / silo.chargeSpend);
                    silo.direction = -1;
                }
                if (silo.direction == -1)
                {
                    silo.time -= num2;
                    if (silo.time <= 0)
                    {
                        silo.time = 0;
                        silo.direction = 0;
                    }
                }
                if (power >= 0.1f)
                {
                    return 1u;
                }
                return 0u;
            }
            if (power < 0.1f)
            {
                if (silo.direction == 1)
                {
                    silo.time = (int)(silo.time * (long)silo.coldSpend / silo.chargeSpend);
                    silo.direction = -1;
                }
                return 0u;
            }
            uint num3 = 0u;
            bool flag;
            num3 = (flag = silo.bulletCount > 0) ? 3u : 2u;
            if (silo.direction == 1)
            {
                if (!flag)
                {
                    silo.time = (int)(silo.time * (long)silo.coldSpend / silo.chargeSpend);
                    silo.direction = -1;
                }
            }
            else if (silo.direction == 0 && flag)
            {
                silo.direction = 1;
            }
            if (silo.direction == 1)
            {
                silo.time += num2;
                if (silo.time >= silo.chargeSpend)
                {
                    AstroData[] astrosData = sphere.starData.galaxy.astrosData;
                    silo.fired = true;
                    DysonNode autoDysonNode = sphere.GetAutoDysonNode(silo.autoIndex + silo.id);
                    DysonRocket rocket = default;
                    rocket.planetId = silo.planetId;
                    rocket.uPos = astrosData[silo.planetId].uPos + Maths.QRotateLF(astrosData[silo.planetId].uRot, silo.localPos + silo.localPos.normalized * 6.1f);
                    rocket.uRot = astrosData[silo.planetId].uRot * silo.localRot * Quaternion.Euler(-90f, 0f, 0f);
                    rocket.uVel = rocket.uRot * Vector3.forward;
                    rocket.uSpeed = 0f;
                    rocket.launch = silo.localPos.normalized;
                    sphere.AddDysonRocket(rocket, autoDysonNode);
                    silo.autoIndex++;
                    int num4 = silo.bulletInc / silo.bulletCount;
                    if (!silo.incUsed)
                    {
                        silo.incUsed = num4 > 0;
                    }
                    silo.bulletInc -= num4;
                    silo.bulletCount--;
                    if (silo.bulletCount == 0)
                    {
                        silo.bulletInc = 0;
                    }
                    consumeRegister[optimizedBulletId]++;
                    silo.time = silo.coldSpend;
                    silo.direction = -1;
                    sphere.gameData.spaceSector.AddHivePlanetHatred(sphere.starData.index, silo.planetId, 1);
                }
            }
            else if (silo.direction == -1)
            {
                silo.time -= num2;
                if (silo.time <= 0)
                {
                    silo.time = 0;
                    silo.direction = flag ? 1 : 0;
                }
            }
            else
            {
                silo.time = 0;
            }
            return num3;
        }
    }
}
