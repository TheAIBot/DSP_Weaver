using System.Linq;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Silos;

internal sealed class SiloExecutor
{
    private int[] _siloIndexes = null!;
    private short[] _optimizedBulletItemId = null!;
    private int[] _siloNetworkIds = null!;

    public void GameTick(PlanetFactory planet, int[] consumeRegister)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        FactorySystem factorySystem = planet.factorySystem;
        short[] optimizedBulletItemId = _optimizedBulletItemId;

        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            short optimizedBulletId = optimizedBulletItemId[siloIndexIndex];

            float power4 = networkServes[_siloNetworkIds[siloIndexIndex]];
            InternalUpdate(ref factorySystem.siloPool[siloIndex], power4, dysonSphere, optimizedBulletId, consumeRegister);
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

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        short[] optimizedBulletItemId = new short[_siloIndexes.Length];
        int[] siloNetworkIds = new int[_siloIndexes.Length];

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            optimizedBulletItemId[siloIndexIndex] = subFactoryProductionRegisterBuilder.AddConsume(silo.bulletId).OptimizedItemIndex;

            siloNetworkIds[siloIndexIndex] = planet.powerSystem.consumerPool[silo.pcId].networkId;

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;
        }

        _optimizedBulletItemId = optimizedBulletItemId;
        _siloNetworkIds = siloNetworkIds;
    }

    private static uint InternalUpdate(ref SiloComponent silo, float power, DysonSphere sphere, short optimizedBulletId, int[] consumeRegister)
    {
        silo.needs[0] = ((silo.bulletCount < 20) ? silo.bulletId : 0);
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
                    silo.time = (int)((long)silo.time * (long)silo.coldSpend / silo.chargeSpend);
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
                    silo.time = (int)((long)silo.time * (long)silo.coldSpend / silo.chargeSpend);
                    silo.direction = -1;
                }
                return 0u;
            }
            uint num3 = 0u;
            bool flag;
            num3 = ((flag = silo.bulletCount > 0) ? 3u : 2u);
            if (silo.direction == 1)
            {
                if (!flag)
                {
                    silo.time = (int)((long)silo.time * (long)silo.coldSpend / silo.chargeSpend);
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
                    DysonRocket rocket = default(DysonRocket);
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
                    silo.direction = (flag ? 1 : 0);
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
