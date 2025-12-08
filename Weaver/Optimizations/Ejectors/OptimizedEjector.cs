using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Weaver.Optimizations.NeedsSystem;

namespace Weaver.Optimizations.Ejectors;

internal struct EjectorBulletData
{
    public readonly short BulletId;
    public byte BulletCount;
    public byte BulletInc;

    public EjectorBulletData(int bulletId, int bulletCount, int bulletInc)
    {
        if (bulletId < 0 || bulletId > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bulletId), $"{nameof(bulletId)} was not within the bounds of a short. Value: {bulletId}");
        }
        if (bulletCount < 0 || bulletCount > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bulletCount), $"{nameof(bulletCount)} was not within the bounds of a byte. Value: {bulletCount}");
        }
        if (bulletInc < 0 || bulletInc > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bulletInc), $"{nameof(bulletInc)} was not within the bounds of a byte. Value: {bulletInc}");
        }

        BulletId = (short)bulletId;
        BulletCount = (byte)bulletCount;
        BulletInc = (byte)bulletInc;
    }

    public short TakeOneBulletUnsafe(out byte inc)
    {
        inc = (byte)((BulletInc >= 0) ? ((uint)(BulletInc / BulletCount)) : 0u);
        BulletCount--;
        BulletInc -= inc;
        return BulletId;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedEjector
{
    private readonly int id;
    private readonly int planetId;
    private readonly int chargeSpend;
    private readonly int coldSpend;
    private readonly float pivotY;
    private readonly float muzzleY;
    private readonly bool boost;
    private readonly bool autoOrbit;
    private readonly float localAlt;
    private readonly Vector3 localPosN;
    private readonly Quaternion localRot;
    private int time;
    private int orbitId;
    private int findingOrbitId;
    private int runtimeOrbitId;
    private bool incUsed;
    private Vector3 localDir;
    private double targetDist;
    private bool needFindNextOrbit;

    public OptimizedEjector(ref readonly EjectorComponent ejector)
    {
        id = ejector.id;
        planetId = ejector.planetId;
        chargeSpend = ejector.chargeSpend;
        coldSpend = ejector.coldSpend;
        pivotY = ejector.pivotY;
        muzzleY = ejector.muzzleY;
        boost = ejector.boost;
        autoOrbit = ejector.autoOrbit;
        localAlt = ejector.localAlt;
        localPosN = ejector.localPosN;
        localRot = ejector.localRot;
        time = ejector.time;
        orbitId = ejector.orbitId;
        findingOrbitId = ejector.findingOrbitId;
        runtimeOrbitId = ejector.runtimeOrbitId;
        incUsed = ejector.incUsed;
        localDir = ejector.localDir;
        targetDist = ejector.targetDist;
        needFindNextOrbit = ejector.needFindNextOrbit;
    }

    public uint InternalUpdate(float power,
                                long tick,
                                DysonSwarm? swarm,
                                AstroData[] astroPoses,
                                short optimizedBulletId,
                                int[] consumeRegister,
                                ComponentNeeds[] componentsNeeds,
                                int needsOffset,
                                ref EjectorBulletData bulletData,
                                ref int direction,
                                int incLevel)
    {
        if (swarm == null)
        {
            throw new InvalidOperationException("I am very confused about why this ever worked to begin with. Swarm was null for ejector which is possible. The game ignores it but it will cause a crash.");
        }

        componentsNeeds[needsOffset + EjectorExecutor.SoleEjectorNeedsIndex].Needs = (byte)(bulletData.BulletCount < 20 ? 1 : 0);
        // No point in updating anything else if the ejector can't shoot anyway
        if (bulletData.BulletCount == 0)
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
        bool flag3 = bulletData.BulletCount > 0;
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
                int num18 = bulletData.BulletInc / bulletData.BulletCount;
                if (!incUsed)
                {
                    incUsed = num18 > 0;
                }
                bulletData.BulletInc -= (byte)num18;
                bulletData.BulletCount--;
                if (bulletData.BulletCount == 0)
                {
                    bulletData.BulletInc = 0;
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

    public readonly void Save(ref EjectorComponent ejector,
                              GroupNeeds groupNeeds,
                              ComponentNeeds[] componentsNeeds,
                              short[] needsPatterns,
                              int ejectorIndex,
                              EjectorBulletData ejectorBulletData,
                              int direction)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(ejectorIndex);
        ComponentNeeds componentNeeds = componentsNeeds[needsOffset];
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            GroupNeeds.SetNeedsIfInRange(ejector.needs, componentNeeds, needsPatterns, i);
        }

        ejector.time = time;
        ejector.orbitId = orbitId;
        ejector.findingOrbitId = findingOrbitId;
        ejector.runtimeOrbitId = runtimeOrbitId;
        ejector.incUsed = incUsed;
        ejector.localDir = localDir;
        ejector.targetDist = targetDist;
        ejector.needFindNextOrbit = needFindNextOrbit;
        ejector.direction = direction;
        ejector.bulletCount = ejectorBulletData.BulletCount;
        ejector.bulletInc = ejectorBulletData.BulletInc;
    }
}
