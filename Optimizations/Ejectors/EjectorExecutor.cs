using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;
using static EjectorComponent;

namespace Weaver.Optimizations.Ejectors;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedEjector
{
    private readonly int id;
    private readonly int planetId;
    private readonly int chargeSpend;
    private readonly int coldSpend;
    private readonly int bulletId;
    private readonly float pivotY;
    private readonly float muzzleY;
    private readonly bool boost;
    private readonly bool autoOrbit;
    private readonly float localAlt;
    private readonly int incLevel;
    private readonly Vector3 localPosN;
    private readonly Quaternion localRot;
    private int direction;
    private int time;
    private int bulletCount;
    private int bulletInc;
    private int orbitId;
    private int findingOrbitId;
    private int runtimeOrbitId;
    private bool incUsed;
    private Vector3 localDir;
    private double targetDist;
    private bool needFindNextOrbit;


    public uint InternalUpdate(float power,
                               long tick,
                               DysonSwarm? swarm,
                               AstroData[] astroPoses,
                               short optimizedBulletId,
                               int[] consumeRegister,
                               short[] needs,
                               int needsOffset)
    {
        if (swarm == null)
        {
            throw new InvalidOperationException("I am very confused about why this ever worked to begin with. Swarm was null for ejector which is possible. The game ignores it but it will cause a crash.");
        }

        needs[needsOffset + EjectorExecutor.SoleEjectorNeedsIndex] = (short)(bulletCount < 20 ? bulletId : 0);
        // No point in updating anything else if the ejector can't shoot anyway
        if (bulletCount == 0)
        {
            return 0u;
        }

        if (!autoOrbit)
        {
            runtimeOrbitId = orbitId;
        }
        if (orbitId < 0 || orbitId >= swarm.orbitCursor || swarm.orbits[orbitId].id != orbitId || !swarm.orbits[orbitId].enabled)
        {
            orbitId = 0;
        }
        if (swarm.orbits[runtimeOrbitId].id != runtimeOrbitId || !swarm.orbits[runtimeOrbitId].enabled)
        {
            runtimeOrbitId = orbitId;
        }
        if (swarm.orbits[findingOrbitId].id != findingOrbitId || !swarm.orbits[findingOrbitId].enabled)
        {
            findingOrbitId = orbitId;
        }
        float num = (float)Cargo.accTableMilli[incLevel];
        int num2 = (int)(power * 10000f * (1f + num) + 0.1f);
        if (boost)
        {
            num2 *= 10;
        }
        if (runtimeOrbitId == 0 && !needFindNextOrbit)
        {
            if (autoOrbit)
            {
                needFindNextOrbit = true;
            }
            if (direction == 1)
            {
                time = (int)(time * (long)coldSpend / chargeSpend);
                direction = -1;
            }
            if (direction == -1)
            {
                time -= num2;
                if (time <= 0)
                {
                    time = 0;
                    direction = 0;
                }
            }
            if (power >= 0.1f)
            {
                localDir.x *= 0.9f;
                localDir.y *= 0.9f;
                localDir.z = localDir.z * 0.9f + 0.1f;
                return 1u;
            }
            return 0u;
        }
        if (power < 0.1f)
        {
            if (direction == 1)
            {
                time = (int)(time * (long)coldSpend / chargeSpend);
                direction = -1;
            }
            return 0u;
        }

        bool flag = true;
        int num4 = planetId / 100 * 100;
        float num5 = localAlt + pivotY + (muzzleY - pivotY) / Mathf.Max(0.1f, Mathf.Sqrt(1f - localDir.y * localDir.y));
        Vector3 vector = new Vector3(localPosN.x * num5, localPosN.y * num5, localPosN.z * num5);
        VectorLF3 vectorLF = astroPoses[planetId].uPos + Maths.QRotateLF(astroPoses[planetId].uRot, vector);
        Quaternion q = astroPoses[planetId].uRot * localRot;
        VectorLF3 uPos = astroPoses[num4].uPos;
        VectorLF3 b = uPos - vectorLF;
        if (needFindNextOrbit)
        {
            int num6 = 0;
            long num7 = tick % 30;
            long num8 = id % 30;
            if (num7 == num8 && orbitId != 0)
            {
                num6 = orbitId;
            }
            else if ((num7 + 15) % 30 == num8)
            {
                int num9 = findingOrbitId + 1;
                if (num9 >= swarm.orbitCursor)
                {
                    num9 = 1;
                }
                while (swarm.orbits[num9].id != num9 || !swarm.orbits[num9].enabled)
                {
                    num9++;
                    if (num9 >= swarm.orbitCursor)
                    {
                        num9 = 1;
                    }
                    if (num9 == runtimeOrbitId)
                    {
                        break;
                    }
                }
                num6 = num9;
                findingOrbitId = num9;
            }
            if (num6 != 0)
            {
                VectorLF3 vectorLF2 = uPos + VectorLF3.Cross(swarm.orbits[num6].up, b).normalized * swarm.orbits[num6].radius - vectorLF;
                targetDist = vectorLF2.magnitude;
                vectorLF2.x /= targetDist;
                vectorLF2.y /= targetDist;
                vectorLF2.z /= targetDist;
                Vector3 vector2 = Maths.QInvRotate(q, vectorLF2);
                if (vector2.y >= 0.08715574 && vector2.y <= 0.8660254f)
                {
                    bool flag2 = false;
                    for (int i = num4 + 1; i <= planetId + 2; i++)
                    {
                        if (i == planetId)
                        {
                            continue;
                        }
                        double num10 = astroPoses[i].uRadius;
                        if (!(num10 > 1.0))
                        {
                            continue;
                        }
                        VectorLF3 vectorLF3 = astroPoses[i].uPos - vectorLF;
                        double num11 = vectorLF3.x * vectorLF3.x + vectorLF3.y * vectorLF3.y + vectorLF3.z * vectorLF3.z;
                        double num12 = vectorLF3.x * vectorLF2.x + vectorLF3.y * vectorLF2.y + vectorLF3.z * vectorLF2.z;
                        if (num12 > 0.0)
                        {
                            double num13 = num11 - num12 * num12;
                            num10 += 120.0;
                            if (num13 < num10 * num10)
                            {
                                flag2 = true;
                                break;
                            }
                        }
                    }
                    if (!flag2)
                    {
                        runtimeOrbitId = num6;
                    }
                }
            }
        }
        VectorLF3 vectorLF4 = uPos + VectorLF3.Cross(swarm.orbits[runtimeOrbitId].up, b).normalized * swarm.orbits[runtimeOrbitId].radius;
        VectorLF3 vectorLF5 = vectorLF4 - vectorLF;
        targetDist = vectorLF5.magnitude;
        vectorLF5.x /= targetDist;
        vectorLF5.y /= targetDist;
        vectorLF5.z /= targetDist;
        Vector3 vector3 = Maths.QInvRotate(q, vectorLF5);
        if (vector3.y < 0.08715574 || vector3.y > 0.8660254f)
        {
            flag = false;
        }
        bool flag3 = bulletCount > 0;
        if (flag3 && flag)
        {
            for (int j = num4 + 1; j <= planetId + 2; j++)
            {
                if (j == planetId)
                {
                    continue;
                }
                double num14 = astroPoses[j].uRadius;
                if (!(num14 > 1.0))
                {
                    continue;
                }
                VectorLF3 vectorLF6 = astroPoses[j].uPos - vectorLF;
                double num15 = vectorLF6.x * vectorLF6.x + vectorLF6.y * vectorLF6.y + vectorLF6.z * vectorLF6.z;
                double num16 = vectorLF6.x * vectorLF5.x + vectorLF6.y * vectorLF5.y + vectorLF6.z * vectorLF5.z;
                if (num16 > 0.0)
                {
                    double num17 = num15 - num16 * num16;
                    num14 += 120.0;
                    if (num17 < num14 * num14)
                    {
                        flag = false;
                        break;
                    }
                }
            }
        }
        if (autoOrbit && (!flag || runtimeOrbitId == 0))
        {
            needFindNextOrbit = true;
            runtimeOrbitId = 0;
            if (direction == 1)
            {
                time = (int)(time * (long)coldSpend / chargeSpend);
                direction = -1;
            }
            if (direction == -1)
            {
                time -= num2;
                if (time <= 0)
                {
                    time = 0;
                    direction = 0;
                }
            }
            if (power >= 0.1f)
            {
                localDir.x *= 0.9f;
                localDir.y *= 0.9f;
                localDir.z = localDir.z * 0.9f + 0.1f;
                return 1u;
            }
            return 0u;
        }
        needFindNextOrbit = false;
        localDir.x = localDir.x * 0.9f + vector3.x * 0.1f;
        localDir.y = localDir.y * 0.9f + vector3.y * 0.1f;
        localDir.z = localDir.z * 0.9f + vector3.z * 0.1f;
        bool flag4 = flag && flag3;
        uint num3 = !flag3 ? 2u : flag ? 4u : 3u;
        if (direction == 1)
        {
            if (!flag4)
            {
                time = (int)(time * (long)coldSpend / chargeSpend);
                direction = -1;
            }
        }
        else if (direction == 0 && flag4)
        {
            direction = 1;
        }
        if (direction == 1)
        {
            time += num2;
            if (time >= chargeSpend)
            {
                SailBullet bullet = default;
                bullet.maxt = (float)(targetDist / 5000.0);
                bullet.lBegin = vector;
                bullet.uEndVel = VectorLF3.Cross(vectorLF4 - uPos, swarm.orbits[runtimeOrbitId].up).normalized * Math.Sqrt(swarm.dysonSphere.gravity / swarm.orbits[runtimeOrbitId].radius);
                bullet.uBegin = vectorLF;
                bullet.uEnd = vectorLF4;
                swarm.AddBullet(bullet, runtimeOrbitId);
                int num18 = bulletInc / bulletCount;
                if (!incUsed)
                {
                    incUsed = num18 > 0;
                }
                bulletInc -= num18;
                bulletCount--;
                if (bulletCount == 0)
                {
                    bulletInc = 0;
                }
                consumeRegister[optimizedBulletId]++;
                time = coldSpend;
                direction = -1;
            }
        }
        else if (direction == -1)
        {
            time -= num2;
            if (time <= 0)
            {
                time = 0;
                direction = flag4 ? 1 : 0;
            }
        }
        else
        {
            time = 0;
        }
        return num3;
    }

    public int TakeOneBulletUnsafe(out byte inc)
    {
        inc = (byte)((bulletInc >= 0) ? ((uint)(bulletInc / bulletCount)) : 0u);
        bulletCount--;
        bulletInc -= inc;
        return bulletId;
    }
}

internal sealed class EjectorExecutor
{
    public int[] _ejectorIndexes = null!;
    public OptimizedEjector[] _optimizedEjectors = null!;
    private short[] _optimizedBulletItemId = null!;
    private int[] _ejectorNetworkIds = null!;
    private Dictionary<int, int> _ejectorIdToOptimizedEjectorIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;
    public const int SoleEjectorNeedsIndex = 0;

    public int Count => _ejectorIndexes.Length;

    public int GetOptimizedEjectorIndex(int ejectorId)
    {
        return _ejectorIdToOptimizedEjectorIndex[ejectorId];
    }

    public void GameTick(PlanetFactory planet,
                         long time,
                         int[] ejectorPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] consumeRegister,
                         SubFactoryNeeds subFactoryNeeds)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;
        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;
        short[] optimizedBulletItemId = _optimizedBulletItemId;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Ejector);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;

        DysonSwarm? swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        //for (int i = 0; i < _optimizedEjectors.Length; i++)
        //{
        //    ref OptimizedEjector ejector = ref _optimizedEjectors[i];
        //}
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            short optimizedBulletId = optimizedBulletItemId[ejectorIndexIndex];
            int needsOffset = groupNeeds.GetObjectNeedsIndex(ejectorIndexIndex);
            int networkIndex = ejectorNetworkIds[ejectorIndexIndex];
            float power3 = networkServes[networkIndex];
            ref EjectorComponent ejector = ref ejectors[ejectorIndex];
            InternalUpdate(ref planet.factorySystem.ejectorPool[ejectorIndex], power3, time, swarm, astroPoses, optimizedBulletId, consumeRegister, componentsNeeds, needsOffset);

            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndexIndex, networkIndex, ref ejector);
        }
    }

    public void UpdatePower(PlanetFactory planet,
                            int[] ejectorPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] ejectorNetworkIds = _ejectorNetworkIds;
        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            int networkIndex = ejectorNetworkIds[ejectorIndexIndex];
            ref readonly EjectorComponent ejector = ref ejectors[ejectorIndex];
            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndexIndex, networkIndex, in ejector);
        }
    }

    private static void UpdatePower(int[] ejectorPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int ejectorIndexIndex,
                                    int networkIndex,
                                    ref readonly EjectorComponent ejector)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, in ejector);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(PlanetFactory planet,
                                                                         int[] ejectorPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            ref readonly EjectorComponent ejector = ref ejectors[ejectorIndex];
            UpdatePowerConsumptionPerPrototype(ejectorPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               ejectorIndexIndex,
                                               in ejector);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] ejectorPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int ejectorIndexIndex,
                                                           ref readonly EjectorComponent ejector)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndexIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[ejectorIndexIndex]] += GetPowerConsumption(powerConsumerType, in ejector);
    }

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        int[] ejectorIndexes = _ejectorIndexes;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Ejector);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        short[] needsPatterns = subFactoryNeeds.NeedsPatterns;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = ejectorIndexes[ejectorIndexIndex];
            int needsOffset = groupNeeds.GetObjectNeedsIndex(ejectorIndexIndex);
            ComponentNeeds componentNeeds = componentsNeeds[needsOffset];
            ref EjectorComponent ejector = ref ejectors[ejectorIndex];

            for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
            {
                GroupNeeds.SetNeedsIfInRange(ejector.needs, componentNeeds, needsPatterns, i);
            }
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder)
    {
        _ejectorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();

        short[] optimizedBulletItemId = new short[_ejectorIndexes.Length];
        int[] ejectorNetworkIds = new int[_ejectorIndexes.Length];
        Dictionary<int, int> ejectorIdToOptimizedEjectorIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.Ejector);

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorIndex];

            optimizedBulletItemId[ejectorIndexIndex] = subFactoryProductionRegisterBuilder.AddConsume(ejector.bulletId).OptimizedItemIndex;
            int networkIndex = planet.powerSystem.consumerPool[ejector.pcId].networkId;
            ejectorNetworkIds[ejectorIndexIndex] = networkIndex;
            ejectorIdToOptimizedEjectorIndex.Add(ejectorIndex, ejectorIndexIndex);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
            needsBuilder.AddNeeds(ejector.needs, [ejector.bulletId]);

            subFactoryPowerSystemBuilder.AddEjector(in ejector, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[ejector.entityId]);
        }

        _optimizedBulletItemId = optimizedBulletItemId;
        _ejectorNetworkIds = ejectorNetworkIds;
        _ejectorIdToOptimizedEjectorIndex = ejectorIdToOptimizedEjectorIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly EjectorComponent ejector)
    {
        return powerConsumerType.GetRequiredEnergy(ejector.direction != 0, 1000 + Cargo.powerTable[ejector.incLevel]);
    }

    private static uint InternalUpdate(ref EjectorComponent ejector,
                                       float power,
                                       long tick,
                                       DysonSwarm? swarm,
                                       AstroData[] astroPoses,
                                       short optimizedBulletId,
                                       int[] consumeRegister,
                                       ComponentNeeds[] componentsNeeds,
                                       int needsOffset)
    {
        if (swarm == null)
        {
            throw new InvalidOperationException("I am very confused about why this ever worked to begin with. Swarm was null for ejector which is possible. The game ignores it but it will cause a crash.");
        }

        componentsNeeds[needsOffset + SoleEjectorNeedsIndex].Needs = (byte)(ejector.bulletCount < 20 ? 1 : 0);
        ejector.targetState = ETargetState.None;
        //// No point in updating anything else if the ejector can't shoot anyway
        //if (ejector.bulletCount == 0)
        //{
        //    return 0u;
        //}

        if (!ejector.autoOrbit)
        {
            ejector.runtimeOrbitId = ejector.orbitId;
        }
        if (ejector.orbitId < 0 || ejector.orbitId >= swarm.orbitCursor || swarm.orbits[ejector.orbitId].id != ejector.orbitId || !swarm.orbits[ejector.orbitId].enabled)
        {
            ejector.orbitId = 0;
        }
        if (swarm.orbits[ejector.runtimeOrbitId].id != ejector.runtimeOrbitId || !swarm.orbits[ejector.runtimeOrbitId].enabled)
        {
            ejector.runtimeOrbitId = ejector.orbitId;
        }
        if (swarm.orbits[ejector.findingOrbitId].id != ejector.findingOrbitId || !swarm.orbits[ejector.findingOrbitId].enabled)
        {
            ejector.findingOrbitId = ejector.orbitId;
        }
        float num = (float)Cargo.accTableMilli[ejector.incLevel];
        int num2 = (int)(power * 10000f * (1f + num) + 0.1f);
        if (ejector.boost)
        {
            num2 *= 10;
        }
        if (ejector.runtimeOrbitId == 0 && !ejector.needFindNextOrbit)
        {
            if (ejector.autoOrbit)
            {
                ejector.needFindNextOrbit = true;
            }
            if (ejector.direction == 1)
            {
                ejector.time = (int)(ejector.time * (long)ejector.coldSpend / ejector.chargeSpend);
                ejector.direction = -1;
            }
            if (ejector.direction == -1)
            {
                ejector.time -= num2;
                if (ejector.time <= 0)
                {
                    ejector.time = 0;
                    ejector.direction = 0;
                }
            }
            if (power >= 0.1f)
            {
                ejector.localDir.x *= 0.9f;
                ejector.localDir.y *= 0.9f;
                ejector.localDir.z = ejector.localDir.z * 0.9f + 0.1f;
                return 1u;
            }
            return 0u;
        }
        if (power < 0.1f)
        {
            if (ejector.direction == 1)
            {
                ejector.time = (int)(ejector.time * (long)ejector.coldSpend / ejector.chargeSpend);
                ejector.direction = -1;
            }
            return 0u;
        }
        ;
        ejector.targetState = ETargetState.OK;
        bool flag = true;
        int num4 = ejector.planetId / 100 * 100;
        float num5 = ejector.localAlt + ejector.pivotY + (ejector.muzzleY - ejector.pivotY) / Mathf.Max(0.1f, Mathf.Sqrt(1f - ejector.localDir.y * ejector.localDir.y));
        Vector3 vector = new Vector3(ejector.localPosN.x * num5, ejector.localPosN.y * num5, ejector.localPosN.z * num5);
        VectorLF3 vectorLF = astroPoses[ejector.planetId].uPos + Maths.QRotateLF(astroPoses[ejector.planetId].uRot, vector);
        Quaternion q = astroPoses[ejector.planetId].uRot * ejector.localRot;
        VectorLF3 uPos = astroPoses[num4].uPos;
        VectorLF3 b = uPos - vectorLF;
        if (ejector.needFindNextOrbit)
        {
            int num6 = 0;
            long num7 = tick % 30;
            long num8 = ejector.id % 30;
            if (num7 == num8 && ejector.orbitId != 0)
            {
                num6 = ejector.orbitId;
            }
            else if ((num7 + 15) % 30 == num8)
            {
                int num9 = ejector.findingOrbitId + 1;
                if (num9 >= swarm.orbitCursor)
                {
                    num9 = 1;
                }
                while (swarm.orbits[num9].id != num9 || !swarm.orbits[num9].enabled)
                {
                    num9++;
                    if (num9 >= swarm.orbitCursor)
                    {
                        num9 = 1;
                    }
                    if (num9 == ejector.runtimeOrbitId)
                    {
                        break;
                    }
                }
                num6 = num9;
                ejector.findingOrbitId = num9;
            }
            if (num6 != 0)
            {
                VectorLF3 vectorLF2 = uPos + VectorLF3.Cross(swarm.orbits[num6].up, b).normalized * swarm.orbits[num6].radius - vectorLF;
                ejector.targetDist = vectorLF2.magnitude;
                vectorLF2.x /= ejector.targetDist;
                vectorLF2.y /= ejector.targetDist;
                vectorLF2.z /= ejector.targetDist;
                Vector3 vector2 = Maths.QInvRotate(q, vectorLF2);
                if (vector2.y >= 0.08715574 && vector2.y <= 0.8660254f)
                {
                    bool flag2 = false;
                    for (int i = num4 + 1; i <= ejector.planetId + 2; i++)
                    {
                        if (i == ejector.planetId)
                        {
                            continue;
                        }
                        double num10 = astroPoses[i].uRadius;
                        if (!(num10 > 1.0))
                        {
                            continue;
                        }
                        VectorLF3 vectorLF3 = astroPoses[i].uPos - vectorLF;
                        double num11 = vectorLF3.x * vectorLF3.x + vectorLF3.y * vectorLF3.y + vectorLF3.z * vectorLF3.z;
                        double num12 = vectorLF3.x * vectorLF2.x + vectorLF3.y * vectorLF2.y + vectorLF3.z * vectorLF2.z;
                        if (num12 > 0.0)
                        {
                            double num13 = num11 - num12 * num12;
                            num10 += 120.0;
                            if (num13 < num10 * num10)
                            {
                                flag2 = true;
                                break;
                            }
                        }
                    }
                    if (!flag2)
                    {
                        ejector.runtimeOrbitId = num6;
                    }
                }
            }
        }
        VectorLF3 vectorLF4 = uPos + VectorLF3.Cross(swarm.orbits[ejector.runtimeOrbitId].up, b).normalized * swarm.orbits[ejector.runtimeOrbitId].radius;
        VectorLF3 vectorLF5 = vectorLF4 - vectorLF;
        ejector.targetDist = vectorLF5.magnitude;
        vectorLF5.x /= ejector.targetDist;
        vectorLF5.y /= ejector.targetDist;
        vectorLF5.z /= ejector.targetDist;
        Vector3 vector3 = Maths.QInvRotate(q, vectorLF5);
        if (vector3.y < 0.08715574 || vector3.y > 0.8660254f)
        {
            ejector.targetState = ETargetState.AngleLimit;
            flag = false;
        }
        bool flag3 = ejector.bulletCount > 0;
        if (flag3 && flag)
        {
            for (int j = num4 + 1; j <= ejector.planetId + 2; j++)
            {
                if (j == ejector.planetId)
                {
                    continue;
                }
                double num14 = astroPoses[j].uRadius;
                if (!(num14 > 1.0))
                {
                    continue;
                }
                VectorLF3 vectorLF6 = astroPoses[j].uPos - vectorLF;
                double num15 = vectorLF6.x * vectorLF6.x + vectorLF6.y * vectorLF6.y + vectorLF6.z * vectorLF6.z;
                double num16 = vectorLF6.x * vectorLF5.x + vectorLF6.y * vectorLF5.y + vectorLF6.z * vectorLF5.z;
                if (num16 > 0.0)
                {
                    double num17 = num15 - num16 * num16;
                    num14 += 120.0;
                    if (num17 < num14 * num14)
                    {
                        flag = false;
                        ejector.targetState = ETargetState.Blocked;
                        break;
                    }
                }
            }
        }
        if (ejector.autoOrbit && (!flag || ejector.runtimeOrbitId == 0))
        {
            ejector.needFindNextOrbit = true;
            ejector.runtimeOrbitId = 0;
            if (ejector.direction == 1)
            {
                ejector.time = (int)(ejector.time * (long)ejector.coldSpend / ejector.chargeSpend);
                ejector.direction = -1;
            }
            if (ejector.direction == -1)
            {
                ejector.time -= num2;
                if (ejector.time <= 0)
                {
                    ejector.time = 0;
                    ejector.direction = 0;
                }
            }
            if (power >= 0.1f)
            {
                ejector.localDir.x *= 0.9f;
                ejector.localDir.y *= 0.9f;
                ejector.localDir.z = ejector.localDir.z * 0.9f + 0.1f;
                return 1u;
            }
            return 0u;
        }
        ejector.needFindNextOrbit = false;
        ejector.localDir.x = ejector.localDir.x * 0.9f + vector3.x * 0.1f;
        ejector.localDir.y = ejector.localDir.y * 0.9f + vector3.y * 0.1f;
        ejector.localDir.z = ejector.localDir.z * 0.9f + vector3.z * 0.1f;
        bool flag4 = flag && flag3;
        uint num3 = !flag3 ? 2u : flag ? 4u : 3u;
        if (ejector.direction == 1)
        {
            if (!flag4)
            {
                ejector.time = (int)(ejector.time * (long)ejector.coldSpend / ejector.chargeSpend);
                ejector.direction = -1;
            }
        }
        else if (ejector.direction == 0 && flag4)
        {
            ejector.direction = 1;
        }
        if (ejector.direction == 1)
        {
            ejector.time += num2;
            if (ejector.time >= ejector.chargeSpend)
            {
                ejector.fired = true;
                SailBullet bullet = default;
                bullet.maxt = (float)(ejector.targetDist / 5000.0);
                bullet.lBegin = vector;
                bullet.uEndVel = VectorLF3.Cross(vectorLF4 - uPos, swarm.orbits[ejector.runtimeOrbitId].up).normalized * Math.Sqrt(swarm.dysonSphere.gravity / swarm.orbits[ejector.runtimeOrbitId].radius);
                bullet.uBegin = vectorLF;
                bullet.uEnd = vectorLF4;
                swarm.AddBullet(bullet, ejector.runtimeOrbitId);
                int num18 = ejector.bulletInc / ejector.bulletCount;
                if (!ejector.incUsed)
                {
                    ejector.incUsed = num18 > 0;
                }
                ejector.bulletInc -= num18;
                ejector.bulletCount--;
                if (ejector.bulletCount == 0)
                {
                    ejector.bulletInc = 0;
                }
                consumeRegister[optimizedBulletId]++;
                ejector.time = ejector.coldSpend;
                ejector.direction = -1;
            }
        }
        else if (ejector.direction == -1)
        {
            ejector.time -= num2;
            if (ejector.time <= 0)
            {
                ejector.time = 0;
                ejector.direction = flag4 ? 1 : 0;
            }
        }
        else
        {
            ejector.time = 0;
        }
        return num3;
    }
}
