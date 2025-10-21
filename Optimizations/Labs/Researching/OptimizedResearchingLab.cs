using System.Runtime.InteropServices;
using Weaver.Optimizations.Labs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Researching;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedResearchingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly int nextLabIndex;
    public bool incUsed;
    public int hashBytes;
    public int extraHashBytes;

    public OptimizedResearchingLab(int? nextLabIndex,
                                   ref readonly LabComponent lab)
    {
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
    }

    public OptimizedResearchingLab(int nextLabIndex,
                                   ref readonly OptimizedResearchingLab lab)
    {
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
        hashBytes = lab.hashBytes;
        extraHashBytes = lab.extraHashBytes;
    }

    public void SetFunction(int entityId,
                            int techId,
                            SignData[] signPool,
                            ref LabPowerFields labPowerFields,
                            GroupNeeds groupNeeds,
                            short[] needs,
                            int labIndex)
    {
        hashBytes = 0;
        extraHashBytes = 0;
        labPowerFields.extraPowerRatio = 0;
        incUsed = false;
        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        for (int i = 0; i < LabComponent.matrixIds.Length; i++)
        {
            needs[needsOffset + i] = (short)LabComponent.matrixIds[i];
        }
        signPool[entityId].iconId0 = (uint)techId;
        signPool[entityId].iconType = techId != 0 ? 3u : 0u;
    }

    public static void UpdateNeedsResearch(GroupNeeds groupNeeds,
                                           short[] needs,
                                           int[] matrixServed,
                                           int labIndex)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int matrixServedOffset = groupNeeds.GroupNeedsSize * labIndex;
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            needs[needsOffset + i] = matrixServed[matrixServedOffset + i] < 36000 ? (short)(6001 + i) : (short)0;
        }
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
                                           ref LabPowerFields labPowerFields,
                                           GroupNeeds groupNeeds,
                                           int[] matrixServed,
                                           int[] matrixIncServed,
                                           int labIndex)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return LabState.Active;
        }

        int num = (int)(research_speed + 2f);
        int matrixServedOffset = groupNeeds.GroupNeedsSize * labIndex;
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            if (matrixPoints[i] <= 0)
            {
                continue;
            }

            int num2 = matrixServed[matrixServedOffset + i] / matrixPoints[i];
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
            int num7 = groupNeeds.GroupNeedsSize;
            int num8 = num7 != 0 ? 10 : 0;
            for (int i = 0; i < num7; i++)
            {
                if (matrixPoints[i] > 0)
                {
                    int num9 = matrixServed[matrixServedOffset + i] / 3600;
                    int num10 = split_inc_level(ref matrixServed[matrixServedOffset + i], ref matrixIncServed[matrixServedOffset + i], matrixPoints[i] * num6);
                    num8 = num8 < num10 ? num8 : num10;
                    int num11 = matrixServed[matrixServedOffset + i] / 3600;
                    if (matrixServed[matrixServedOffset + i] <= 0 || matrixIncServed[matrixServedOffset + i] < 0)
                    {
                        matrixIncServed[matrixServedOffset + i] = 0;
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
                                            NetworkIdAndState<LabState>[] networkIdAndStates,
                                            GroupNeeds groupNeeds,
                                            short[] needs,
                                            int[] matrixServed,
                                            int[] matrixIncServed)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        int matrixServedOffset = groupNeeds.GroupNeedsSize * labIndex;
        int nextLabMatrixServedOffset = groupNeeds.GroupNeedsSize * nextLabIndex;

        bool movedItems = false;
        int nextLabNeedsOffset = groupNeeds.GetObjectNeedsIndex(nextLabIndex);
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            if (needs[nextLabNeedsOffset + i] == 6001 + i && matrixServed[matrixServedOffset + i] >= 7200)
            {
                int num = (matrixServed[matrixServedOffset + i] - 7200) / 3600 * 3600;
                if (num > 36000)
                {
                    num = 36000;
                }
                int num2 = split_inc(ref matrixServed[matrixServedOffset + i], ref matrixIncServed[matrixServedOffset + i], num);
                matrixIncServed[nextLabMatrixServedOffset + i] += num2;
                matrixServed[nextLabMatrixServedOffset + i] += num;
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
                              int researchTechId,
                              GroupNeeds groupNeeds,
                              short[] needs,
                              int[] matrixServed,
                              int[] matrixIncServed,
                              int labIndex)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * labIndex;
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            GroupNeeds.SetIfInRange(lab.matrixServed, matrixServed, i, servedOffset + i);
            GroupNeeds.SetIfInRange(lab.needs, needs, i, needsOffset + i);
            GroupNeeds.SetIfInRange(lab.matrixIncServed, matrixIncServed, i, servedOffset + i);
        }

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
