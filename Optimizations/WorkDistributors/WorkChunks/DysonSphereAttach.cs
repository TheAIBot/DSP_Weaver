using System;
using System.Threading;
using UnityEngine;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class DysonSphereAttach : IWorkChunk
{
    private readonly DysonSphere _dysonSphere;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;

    public DysonSphereAttach(DysonSphere dysonSphere, int workIndex, int maxWorkCount)
    {
        _dysonSphere = dysonSphere;
        _workIndex = workIndex;
        _maxWorkCount = maxWorkCount;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DysonSwarm? dysonSwarm = _dysonSphere.swarm;
        if (dysonSwarm != null && dysonSwarm.bulletCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DysonSwarm, workerIndex);
            ParallelDysonSwarmGameTick();
            DeepProfiler.EndSample(DPEntry.DysonSwarm, workerIndex);
        }

        if (_dysonSphere.rocketCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DysonRocket, workerIndex);
            ParallelDysonRocketGameTick(time);
            DeepProfiler.EndSample(DPEntry.DysonRocket, workerIndex);
        }
    }

    private void ParallelDysonSwarmGameTick()
    {
        GameData data = GameMain.data;
        VectorLF3 relativePos = data.relativePos;
        Quaternion relativeRot = data.relativeRot;
        DysonSwarm? dysonSwarm = null;
        if (data.dysonSpheres != null)
        {
            StarData? starData = null;
            switch (DysonSphere.renderPlace)
            {
                case ERenderPlace.Universe:
                    starData = data.localStar;
                    break;
                case ERenderPlace.Starmap:
                    starData = UIRoot.instance.uiGame.starmap.viewStarSystem;
                    break;
                case ERenderPlace.Dysonmap:
                    starData = UIRoot.instance.uiGame.dysonEditor.selection.viewStar;
                    break;
            }
            if (starData != null)
            {
                dysonSwarm = data.dysonSpheres[starData.index]?.swarm;
            }
        }

        DysonSwarm swarm = _dysonSphere.swarm;
        SailBullet[] bulletPool = swarm.bulletPool;
        int orbitCursor = swarm.orbitCursor;
        SailOrbit[] orbits = swarm.orbits;
        StarData starData2 = swarm.starData;
        ref int randSeed = ref swarm.randSeed;

        (int startIndex, int workLength) = UnOptimizedPlanetWorkChunk.GetWorkChunkIndices(swarm.bulletCursor, _maxWorkCount, _workIndex);
        for (int i = startIndex; i < startIndex + workLength; i++)
        {
            ref SailBullet sailBullet = ref bulletPool[i];
            if (sailBullet.id != i)
            {
                continue;
            }

            sailBullet.t += 1f / 60f;
            if (sailBullet.t >= sailBullet.maxt)
            {
                if (sailBullet.state > 0)
                {
                    if (sailBullet.state < orbitCursor && orbits[sailBullet.state].id == sailBullet.state)
                    {
                        DysonSail ss = default;
                        VectorLF3 vectorLF = sailBullet.uEnd - starData2.uPosition;
                        ss.px = (float)vectorLF.x;
                        ss.py = (float)vectorLF.y;
                        ss.pz = (float)vectorLF.z;
                        vectorLF = sailBullet.uEndVel;
                        vectorLF += SphericNormalThreadSafe(ref randSeed, 0.5);
                        ss.vx = (float)vectorLF.x;
                        ss.vy = (float)vectorLF.y;
                        ss.vz = (float)vectorLF.z;
                        ss.gs = 1f;
                        swarm.AddSolarSailList(ref ss, sailBullet.state);
                    }
                }
                else if (sailBullet.t > sailBullet.maxt + 0.7f)
                {
                    swarm.RemoveBullet(i);
                }
                sailBullet.state = 0;
            }

            if (dysonSwarm == swarm)
            {
                switch (DysonSphere.renderPlace)
                {
                    case ERenderPlace.Universe:
                        sailBullet.rBegin = Maths.QInvRotateLF(relativeRot, sailBullet.uBegin - relativePos);
                        sailBullet.rEnd = Maths.QInvRotateLF(relativeRot, sailBullet.uEnd - relativePos);
                        break;
                    case ERenderPlace.Starmap:
                        sailBullet.rBegin = (sailBullet.uBegin - UIStarmap.viewTargetStatic) * 0.00025;
                        sailBullet.rEnd = (sailBullet.uEnd - UIStarmap.viewTargetStatic) * 0.00025;
                        break;
                    case ERenderPlace.Dysonmap:
                        sailBullet.rBegin = (sailBullet.uBegin - starData2.uPosition) * 0.00025;
                        sailBullet.rEnd = (sailBullet.uEnd - starData2.uPosition) * 0.00025;
                        break;
                    default:
                        Assert.CannotBeReached();
                        break;
                }
            }
        }
    }

    private void ParallelDysonRocketGameTick(long time)
    {
        GameLogic logic = GameMain.logic;
        DysonSphere dysonSphere = _dysonSphere;
        DysonRocket[] rocketPool = dysonSphere.rocketPool;
        DysonSphereLayer[] array2 = dysonSphere.layersIdBased;
        StarData starData = dysonSphere.starData;
        AstroData[] array3 = starData.galaxy.astrosData;
        float num3 = 7.5f;
        float num6 = Mathf.Max(1f, (float)Math.Pow((double)dysonSphere.defOrbitRadius / 40000.0 * 4.0, 0.4));
        float num4 = 18f * num6;
        float num5 = 2800f * num6;
        VectorLF3 vectorLF = starData.uPosition;

        (int startIndex, int workLength) = UnOptimizedPlanetWorkChunk.GetWorkChunkIndices(dysonSphere.rocketCursor, _maxWorkCount, _workIndex);
        for (int i = startIndex; i < startIndex + workLength; i++)
        {
            ref DysonRocket reference2 = ref rocketPool[i];
            if (reference2.id != i)
            {
                continue;
            }
            if (reference2.node == null)
            {
                dysonSphere.RemoveDysonRocket(i);
                continue;
            }
            bool flag4 = false;
            DysonSphereLayer dysonSphereLayer = array2[reference2.node.layerId];
            ref AstroData reference3 = ref array3[reference2.planetId];
            VectorLF3 v = reference2.uPos - reference3.uPos;
            double num10 = VectorLength(v) - (double)reference3.uRadius;
            if (reference2.t <= 0f)
            {
                if (num10 < 200.0)
                {
                    float num11 = (float)num10 / 200f;
                    if (num11 < 0f)
                    {
                        num11 = 0f;
                    }
                    float num12 = num11 * num11 * 600f + 15f;
                    reference2.uSpeed = reference2.uSpeed * 0.9f + num12 * 0.1f;
                    reference2.t = (num11 - 1f) * 1.2f;
                    if (reference2.t < -1f)
                    {
                        reference2.t = -1f;
                    }
                }
                else
                {
                    dysonSphereLayer.NodeEnterUPos(reference2.node, out var result);
                    VectorLF3 vectorLF2 = result - reference2.uPos;
                    double num13 = VectorLength(vectorLF2);
                    if (num13 < 50.0)
                    {
                        reference2.t = 0.0001f;
                    }
                    else
                    {
                        reference2.t = 0f;
                    }
                    double num14 = num13 / ((double)reference2.uSpeed + 0.1) * 0.382;
                    double num15 = num13 / (double)num5;
                    float num16 = (float)((double)reference2.uSpeed * num14) + 150f;
                    if (num16 > num5)
                    {
                        num16 = num5;
                    }
                    if (reference2.uSpeed < num16 - num3)
                    {
                        reference2.uSpeed += num3;
                    }
                    else if (reference2.uSpeed > num16 + num4)
                    {
                        reference2.uSpeed -= num4;
                    }
                    else
                    {
                        reference2.uSpeed = num16;
                    }
                    int num17 = -1;
                    double num18 = 0.0;
                    double num19 = 1E+40;
                    int num20 = reference2.planetId / 100 * 100;
                    for (int j = num20; j < num20 + 10; j++)
                    {
                        float uRadius = array3[j].uRadius;
                        if (!(uRadius < 1f) && j != reference2.planetId)
                        {
                            float num21 = ((j == num20) ? (dysonSphereLayer.orbitRadius + 8000f) : (uRadius + 6500f));
                            VectorLF3 vectorLF3 = reference2.uPos - array3[j].uPos;
                            double num22 = vectorLF3.x * vectorLF3.x + vectorLF3.y * vectorLF3.y + vectorLF3.z * vectorLF3.z;
                            double num23 = 0.0 - ((double)reference2.uVel.x * vectorLF3.x + (double)reference2.uVel.y * vectorLF3.y + (double)reference2.uVel.z * vectorLF3.z);
                            if ((num23 > 0.0 || num22 < (double)(uRadius * uRadius * 7f)) && num22 < num19 && num22 < (double)(num21 * num21))
                            {
                                num18 = ((num23 < 0.0) ? 0.0 : num23);
                                num17 = j;
                                num19 = num22;
                            }
                        }
                    }
                    VectorLF3 vectorLF4 = VectorLF3.zero;
                    float num24 = 0f;
                    if (num17 > 0)
                    {
                        float num25 = array3[num17].uRadius;
                        bool flag5 = num17 % 100 == 0;
                        if (flag5)
                        {
                            num25 = dysonSphereLayer.orbitRadius - 400f;
                        }
                        double num26 = 1.25;
                        VectorLF3 vectorLF5 = reference2.uPos + (VectorLF3)reference2.uVel * num18 - array3[num17].uPos;
                        double num27 = vectorLF5.magnitude / (double)num25;
                        if (num27 < num26)
                        {
                            double num28 = Math.Sqrt(num19) - (double)num25 * 0.82;
                            if (num28 < 1.0)
                            {
                                num28 = 1.0;
                            }
                            double num29 = (num27 - 1.0) / (num26 - 1.0);
                            if (num29 < 0.0)
                            {
                                num29 = 0.0;
                            }
                            num29 = 1.0 - num29 * num29;
                            double num30 = (double)(reference2.uSpeed - 6f) / num28 * 2.5 - 0.01;
                            if (num30 > 1.5)
                            {
                                num30 = 1.5;
                            }
                            else if (num30 < 0.0)
                            {
                                num30 = 0.0;
                            }
                            num30 = num30 * num30 * num29;
                            num24 = (float)(flag5 ? 0.0 : (num30 * 0.5));
                            vectorLF4 = vectorLF5.normalized * num30 * 2.0;
                        }
                    }
                    int num31 = ((num17 > 0 || num10 < 2000.0 || num13 < 2000.0) ? 1 : 6);
                    if (num31 == 1 || (time + i) % num31 == 0L)
                    {
                        float num32 = 1f / (float)num15 - 0.05f;
                        num32 += num24;
                        float t = Mathf.Lerp(0.005f, 0.08f, num32) * (float)num31;
                        reference2.uVel = Vector3.Slerp(reference2.uVel, vectorLF2.normalized + vectorLF4, t).normalized;
                        Quaternion b;
                        if (num13 < 350.0)
                        {
                            float t2 = ((float)num13 - 50f) / 300f;
                            b = Quaternion.Slerp(dysonSphereLayer.NodeURot(reference2.node), Quaternion.LookRotation(reference2.uVel), t2);
                        }
                        else
                        {
                            b = Quaternion.LookRotation(reference2.uVel);
                        }
                        reference2.uRot = Quaternion.Slerp(reference2.uRot, b, 0.2f);
                    }
                }
            }
            else
            {
                dysonSphereLayer.NodeSlotUPos(reference2.node, out var result2);
                VectorLF3 vectorLF6 = result2 - reference2.uPos;
                double num33 = VectorLength(vectorLF6);
                if (num33 < 2.0)
                {
                    flag4 = true;
                }
                float num34 = (float)(num33 * 0.75 + 15.0);
                if (num34 > num5)
                {
                    num34 = num5;
                }
                if (reference2.uSpeed < num34 - num3)
                {
                    reference2.uSpeed += num3;
                }
                else if (reference2.uSpeed > num34 + num4)
                {
                    reference2.uSpeed -= num4;
                }
                else
                {
                    reference2.uSpeed = num34;
                }
                if ((time + i) % 2 == 0L)
                {
                    reference2.uVel = Vector3.Slerp(reference2.uVel, vectorLF6.normalized, 0.15f);
                    reference2.uRot = Quaternion.Slerp(reference2.uRot, dysonSphereLayer.NodeURot(reference2.node), 0.2f);
                }
                reference2.t = (350f - (float)num33) / 330f;
                if (reference2.t > 1f)
                {
                    reference2.t = 1f;
                }
                else if (reference2.t < 0.0001f)
                {
                    reference2.t = 0.0001f;
                }
            }
            VectorLF3 vectorLF7 = new VectorLF3(0f, 0f, 0f);
            bool flag6 = false;
            double num35 = 2f - (float)num10 / 200f;
            if (num35 > 1.0)
            {
                num35 = 1.0;
            }
            else if (num35 < 0.0)
            {
                num35 = 0.0;
            }
            if (num35 > 0.0)
            {
                Maths.QInvRotateLF_refout(ref reference3.uRot, ref v, out var result3);
                VectorLF3 vectorLF8 = Maths.QRotateLF(reference3.uRotNext, result3) + reference3.uPosNext;
                Maths.QInvMultiply_ref(ref reference3.uRot, ref reference2.uRot, out var result4);
                Maths.QMultiply_ref(ref reference3.uRotNext, ref result4, out var result5);
                num35 = (3.0 - num35 - num35) * num35 * num35;
                vectorLF7 = (vectorLF8 - reference2.uPos) * num35;
                reference2.uRot = ((num35 == 1.0) ? result5 : Quaternion.Slerp(reference2.uRot, result5, (float)num35));
                flag6 = true;
            }
            if (!flag6)
            {
                VectorLF3 v2 = reference2.uPos - vectorLF;
                double num36 = Math.Abs(VectorLength(v2) - (double)dysonSphereLayer.orbitRadius);
                double num37 = 1.5 - (double)((float)num36 / 1800f);
                if (num37 > 1.0)
                {
                    num37 = 1.0;
                }
                else if (num37 < 0.0)
                {
                    num37 = 0.0;
                }
                if (num37 > 0.0)
                {
                    Maths.QInvRotateLF_refout(ref dysonSphereLayer.currentRotation, ref v2, out var result6);
                    VectorLF3 vectorLF9 = Maths.QRotateLF(dysonSphereLayer.nextRotation, result6) + vectorLF;
                    Maths.QInvMultiply_ref(ref dysonSphereLayer.currentRotation, ref reference2.uRot, out var result7);
                    Maths.QMultiply_ref(ref dysonSphereLayer.nextRotation, ref result7, out var result8);
                    num37 = (3.0 - num37 - num37) * num37 * num37;
                    vectorLF7 = (vectorLF9 - reference2.uPos) * num37;
                    reference2.uRot = ((num37 == 1.0) ? result8 : Quaternion.Slerp(reference2.uRot, result8, (float)num37));
                }
            }
            double num38 = reference2.uSpeed * logic.deltaTime_lf;
            reference2.uPos = reference2.uPos + reference2.uVel * new VectorLF3(num38, num38, num38) + vectorLF7;
            if (num10 < 250.0)
            {
                v = reference3.uPos - reference2.uPos;
                num10 = VectorLength(v) - reference3.uRadius;
                if (num10 < 180.0)
                {
                    reference2.uPos = reference3.uPos + Maths.QRotateLF(reference3.uRot, (VectorLF3)reference2.launch * (reference3.uRadius + num10));
                    reference2.uRot = reference3.uRot * Quaternion.LookRotation(reference2.launch);
                }
            }
            if (flag4)
            {
                dysonSphere.ConstructSp(reference2.node);
                dysonSphere.RemoveDysonRocket(i);
            }
        }
    }

    private static double VectorLength(VectorLF3 vectorLF)
    {
        return Math.Sqrt(vectorLF.x * vectorLF.x + vectorLF.y * vectorLF.y + vectorLF.z * vectorLF.z);
    }

    private static VectorLF3 SphericNormalThreadSafe(ref int seed, double scale)
    {
        int incrementedValue = Interlocked.Increment(ref seed);
        if (incrementedValue == 65535)
        {
            Interlocked.Exchange(ref seed, 0);
        }
        incrementedValue &= 65535;
        return new VectorLF3(RandomTable.sphericNormal[incrementedValue].x * scale, RandomTable.sphericNormal[incrementedValue].y * scale, RandomTable.sphericNormal[incrementedValue].z * scale);
    }
}