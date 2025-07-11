﻿using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Researching;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedResearchingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly int[] needs;
    public readonly int[] matrixServed;
    public readonly int[] matrixIncServed;
    public readonly int nextLabIndex;
    public bool incUsed;
    public int hashBytes;
    public int extraHashBytes;

    public OptimizedResearchingLab(int? nextLabIndex,
                                   ref readonly LabComponent lab)
    {
        needs = lab.needs;
        matrixServed = lab.matrixServed;
        matrixIncServed = lab.matrixIncServed;
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
    }

    public OptimizedResearchingLab(int nextLabIndex,
                                   ref readonly OptimizedResearchingLab lab)
    {
        needs = lab.needs;
        matrixServed = lab.matrixServed;
        matrixIncServed = lab.matrixIncServed;
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
    }

    public void SetFunction(int entityId,
                            int techId,
                            SignData[] signPool,
                            ref LabPowerFields labPowerFields)
    {
        hashBytes = 0;
        extraHashBytes = 0;
        labPowerFields.extraPowerRatio = 0;
        incUsed = false;
        Array.Copy(LabComponent.matrixIds, needs, LabComponent.matrixIds.Length);
        signPool[entityId].iconId0 = (uint)techId;
        signPool[entityId].iconType = techId != 0 ? 3u : 0u;
    }

    public readonly void UpdateNeedsResearch()
    {
        needs[0] = matrixServed[0] < 36000 ? 6001 : 0;
        needs[1] = matrixServed[1] < 36000 ? 6002 : 0;
        needs[2] = matrixServed[2] < 36000 ? 6003 : 0;
        needs[3] = matrixServed[3] < 36000 ? 6004 : 0;
        needs[4] = matrixServed[4] < 36000 ? 6005 : 0;
        needs[5] = matrixServed[5] < 36000 ? 6006 : 0;
    }

    public LabState InternalUpdateResearch(float power,
                                           float research_speed,
                                           int techId,
                                           int[] matrixPoints,
                                           OptimizedItemId[] matrixIds,
                                           int[] consumeRegister,
                                           ref TechState ts,
                                           ref int techHashedThisFrame,
                                           ref long uMatrixPoint,
                                           ref long hashRegister,
                                           ref LabPowerFields labPowerFields)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return LabState.Active;
        }
        int num = (int)(research_speed + 2f);
        if (matrixPoints[0] > 0)
        {
            int num2 = matrixServed[0] / matrixPoints[0];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        if (matrixPoints[1] > 0)
        {
            int num2 = matrixServed[1] / matrixPoints[1];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        if (matrixPoints[2] > 0)
        {
            int num2 = matrixServed[2] / matrixPoints[2];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        if (matrixPoints[3] > 0)
        {
            int num2 = matrixServed[3] / matrixPoints[3];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        if (matrixPoints[4] > 0)
        {
            int num2 = matrixServed[4] / matrixPoints[4];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        if (matrixPoints[5] > 0)
        {
            int num2 = matrixServed[5] / matrixPoints[5];
            if (num2 < num)
            {
                num = num2;
                if (num == 0)
                {
                    labPowerFields.replicating = false;
                    return LabState.InactiveInputMissing;
                }
            }
        }
        labPowerFields.replicating = true;
        research_speed = research_speed < num ? research_speed : num;
        int num3 = (int)(power * 10000f * research_speed + 0.5f);
        hashBytes += num3;
        long num4 = hashBytes / 10000;
        hashBytes -= (int)num4 * 10000;
        long num5 = ts.hashNeeded - ts.hashUploaded;
        num4 = num4 < num5 ? num4 : num5;
        num4 = num4 < num ? num4 : num;
        int num6 = (int)num4;
        if (num6 > 0)
        {
            int num7 = matrixServed.Length;
            int num8 = num7 != 0 ? 10 : 0;
            for (int i = 0; i < num7; i++)
            {
                if (matrixPoints[i] > 0)
                {
                    int num9 = matrixServed[i] / 3600;
                    int num10 = split_inc_level(ref matrixServed[i], ref matrixIncServed[i], matrixPoints[i] * num6);
                    num8 = num8 < num10 ? num8 : num10;
                    int num11 = matrixServed[i] / 3600;
                    if (matrixServed[i] <= 0 || matrixIncServed[i] < 0)
                    {
                        matrixIncServed[i] = 0;
                    }
                    int num12 = num9 - num11;
                    if (num12 > 0 && !incUsed)
                    {
                        incUsed = num8 > 0;
                    }
                    consumeRegister[matrixIds[i].OptimizedItemIndex] += num12;
                }
            }
            if (num8 < 0)
            {
                num8 = 0;
            }
            int extraSpeed = (int)(10000.0 * Cargo.incTableMilli[num8] * 10.0 + 0.1);
            labPowerFields.extraPowerRatio = Cargo.powerTable[num8];
            extraHashBytes += (int)(power * extraSpeed * research_speed + 0.5f);
            long num13 = extraHashBytes / 100000;
            extraHashBytes -= (int)num13 * 100000;
            num13 = num13 < 0 ? 0 : num13;
            int num14 = (int)num13;
            ts.hashUploaded += num4 + num13;
            hashRegister += num4 + num13;
            uMatrixPoint += ts.uPointPerHash * num4;
            techHashedThisFrame += num6 + num14;
            if (ts.hashUploaded >= ts.hashNeeded)
            {
                TechProto techProto = LDB.techs.Select(techId);
                if (ts.curLevel >= ts.maxLevel)
                {
                    ts.curLevel = ts.maxLevel;
                    ts.hashUploaded = ts.hashNeeded;
                    ts.unlocked = true;
                    ts.unlockTick = GameMain.gameTick;
                }
                else
                {
                    ts.curLevel++;
                    ts.hashUploaded = 0L;
                    ts.hashNeeded = techProto.GetHashNeeded(ts.curLevel);
                }
            }
        }
        else
        {
            labPowerFields.extraPowerRatio = 0;
        }

        return LabState.Active;
    }

    public readonly void UpdateOutputToNext(int labIndex,
                                            OptimizedResearchingLab[] labPool,
                                            NetworkIdAndState<LabState>[] networkIdAndStates)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        bool movedItems = false;
        if (matrixServed != null && labPool[nextLabIndex].matrixServed != null)
        {
            if (labPool[nextLabIndex].needs[0] == 6001 && matrixServed[0] >= 7200)
            {
                int num = (matrixServed[0] - 7200) / 3600 * 3600;
                if (num > 36000)
                {
                    num = 36000;
                }
                int num2 = split_inc(ref matrixServed[0], ref matrixIncServed[0], num);
                labPool[nextLabIndex].matrixIncServed[0] += num2;
                labPool[nextLabIndex].matrixServed[0] += num;
                movedItems = true;
            }
            if (labPool[nextLabIndex].needs[1] == 6002 && matrixServed[1] >= 7200)
            {
                int num3 = (matrixServed[1] - 7200) / 3600 * 3600;
                if (num3 > 36000)
                {
                    num3 = 36000;
                }
                int num4 = split_inc(ref matrixServed[1], ref matrixIncServed[1], num3);
                labPool[nextLabIndex].matrixIncServed[1] += num4;
                labPool[nextLabIndex].matrixServed[1] += num3;
                movedItems = true;
            }
            if (labPool[nextLabIndex].needs[2] == 6003 && matrixServed[2] >= 7200)
            {
                int num5 = (matrixServed[2] - 7200) / 3600 * 3600;
                if (num5 > 36000)
                {
                    num5 = 36000;
                }
                int num6 = split_inc(ref matrixServed[2], ref matrixIncServed[2], num5);
                labPool[nextLabIndex].matrixIncServed[2] += num6;
                labPool[nextLabIndex].matrixServed[2] += num5;
                movedItems = true;
            }
            if (labPool[nextLabIndex].needs[3] == 6004 && matrixServed[3] >= 7200)
            {
                int num7 = (matrixServed[3] - 7200) / 3600 * 3600;
                if (num7 > 36000)
                {
                    num7 = 36000;
                }
                int num8 = split_inc(ref matrixServed[3], ref matrixIncServed[3], num7);
                labPool[nextLabIndex].matrixIncServed[3] += num8;
                labPool[nextLabIndex].matrixServed[3] += num7;
                movedItems = true;
            }
            if (labPool[nextLabIndex].needs[4] == 6005 && matrixServed[4] >= 7200)
            {
                int num9 = (matrixServed[4] - 7200) / 3600 * 3600;
                if (num9 > 36000)
                {
                    num9 = 36000;
                }
                int num10 = split_inc(ref matrixServed[4], ref matrixIncServed[4], num9);
                labPool[nextLabIndex].matrixIncServed[4] += num10;
                labPool[nextLabIndex].matrixServed[4] += num9;
                movedItems = true;
            }
            if (labPool[nextLabIndex].needs[5] == 6006 && matrixServed[5] >= 7200)
            {
                int num11 = (matrixServed[5] - 7200) / 3600 * 3600;
                if (num11 > 36000)
                {
                    num11 = 36000;
                }
                int num12 = split_inc(ref matrixServed[5], ref matrixIncServed[5], num11);
                labPool[nextLabIndex].matrixIncServed[5] += num12;
                labPool[nextLabIndex].matrixServed[5] += num11;
                movedItems = true;
            }
        }

        if (movedItems)
        {
            networkIdAndStates[labIndex].State = (int)LabState.Active;
            networkIdAndStates[nextLabIndex].State = (int)LabState.Active;
        }
    }

    public readonly void Save(ref LabComponent lab,
                              LabPowerFields labPowerFields,
                              int[] matrixPoints,
                              int researchTechId)
    {
        lab.needs = needs;
        lab.matrixServed = matrixServed;
        lab.matrixIncServed = matrixIncServed;
        lab.replicating = labPowerFields.replicating;
        lab.incUsed = incUsed;
        lab.hashBytes = hashBytes;
        lab.extraHashBytes = extraHashBytes;
        lab.extraPowerRatio = labPowerFields.extraPowerRatio;
        lab.matrixPoints = matrixPoints;
        lab.techId = researchTechId;
    }

    private static int split_inc(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        num = num2 > 0 ? num * p + num2 : num * p;
        m -= num;
        return num;
    }

    private static int split_inc_level(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        m -= num2 > 0 ? num * p + num2 : num * p;
        return num;
    }
}
