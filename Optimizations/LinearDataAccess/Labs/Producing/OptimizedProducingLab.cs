using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Producing;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedProducingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly bool forceAccMode;
    public readonly int nextLabIndex;
    public bool incUsed;
    public int time;
    public int extraTime;
    public int extraSpeed;
    public int speedOverride;

    public OptimizedProducingLab(int? nextLabIndex,
                                 ref readonly LabComponent lab)
    {
        forceAccMode = lab.forceAccMode;
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        incUsed = lab.incUsed;
        time = lab.time;
        extraTime = lab.extraTime;
        extraSpeed = lab.extraSpeed;
        speedOverride = lab.speedOverride;
    }

    public OptimizedProducingLab(int nextLabIndex,
                                 ref readonly OptimizedProducingLab lab)
    {
        forceAccMode = lab.forceAccMode;
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
        time = lab.time;
        extraTime = lab.extraTime;
        extraSpeed = lab.extraSpeed;
        speedOverride = lab.speedOverride;
    }

    public readonly void UpdateNeedsAssemble(ref readonly ProducingLabRecipe producingLabRecipe,
                                             GroupNeeds groupNeeds,
                                             short[] served,
                                             short[] needs,
                                             int labIndex)
    {
        int num2 = producingLabRecipe.TimeSpend > 5400000 ? 6 : 3 * ((speedOverride + 5001) / 10000) + 3;

        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * labIndex;
        OptimizedItemId[] requires = producingLabRecipe.Requires;
        for (int i = 0; i < requires.Length; i++)
        {
            needs[needsOffset + i] = served[servedOffset + i] < num2 ? requires[i].ItemIndex : (short)0;
        }
    }

    public LabState InternalUpdateAssemble(float power,
                                           int[] productRegister,
                                           int[] consumeRegister,
                                           ref readonly ProducingLabRecipe producingLabRecipe,
                                           ref LabPowerFields labPowerFields,
                                           int servedOffset,
                                           int producedOffset,
                                           short[] served,
                                           short[] incServed,
                                           short[] produced)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return LabState.Active;
        }
        if (extraTime >= producingLabRecipe.ExtraTimeSpend)
        {
            for (int i = 0; i < producingLabRecipe.ProductCounts.Length; i++)
            {
                produced[producedOffset + i] += (short)producingLabRecipe.ProductCounts[i];
                productRegister[producingLabRecipe.Products[i].OptimizedItemIndex] += producingLabRecipe.ProductCounts[i];
            }
            extraTime -= producingLabRecipe.ExtraTimeSpend;
        }
        if (time >= producingLabRecipe.TimeSpend)
        {
            labPowerFields.replicating = false;
            for (int j = 0; j < producingLabRecipe.Products.Length; j++)
            {
                if (produced[producedOffset + j] + producingLabRecipe.ProductCounts[j] > 10 * ((speedOverride + 9999) / 10000))
                {
                    return LabState.InactiveOutputFull;
                }
            }
            for (int k = 0; k < producingLabRecipe.Products.Length; k++)
            {
                produced[producedOffset + k] += (short)producingLabRecipe.ProductCounts[k];
                productRegister[producingLabRecipe.Products[k].OptimizedItemIndex] += producingLabRecipe.ProductCounts[k];
            }
            extraSpeed = 0;
            speedOverride = producingLabRecipe.Speed;
            labPowerFields.extraPowerRatio = 0;
            time -= producingLabRecipe.TimeSpend;
        }
        if (!labPowerFields.replicating)
        {
            int num3 = producingLabRecipe.RequireCounts.Length;
            for (int l = 0; l < num3; l++)
            {
                if (incServed[servedOffset + l] <= 0)
                {
                    incServed[servedOffset + l] = 0;
                }
                if (served[servedOffset + l] < producingLabRecipe.RequireCounts[l] || served[servedOffset + l] == 0)
                {
                    time = 0;
                    return LabState.InactiveInputMissing;
                }
            }
            int num4 = num3 > 0 ? 10 : 0;
            for (int m = 0; m < num3; m++)
            {
                int num5 = split_inc_level(ref served[servedOffset + m], ref incServed[servedOffset + m], (short)producingLabRecipe.RequireCounts[m]);
                num4 = num4 < num5 ? num4 : num5;
                if (!incUsed)
                {
                    incUsed = num5 > 0;
                }
                if (served[servedOffset + m] == 0)
                {
                    incServed[servedOffset + m] = 0;
                }
                consumeRegister[producingLabRecipe.Requires[m].OptimizedItemIndex] += producingLabRecipe.RequireCounts[m];
            }
            if (num4 < 0)
            {
                num4 = 0;
            }
            if (producingLabRecipe.Productive && !forceAccMode)
            {
                extraSpeed = (int)(producingLabRecipe.Speed * Cargo.incTableMilli[num4] * 10.0 + 0.1);
                speedOverride = producingLabRecipe.Speed;
                labPowerFields.extraPowerRatio = Cargo.powerTable[num4];
            }
            else
            {
                extraSpeed = 0;
                speedOverride = (int)(producingLabRecipe.Speed * (1.0 + Cargo.accTableMilli[num4]) + 0.1);
                labPowerFields.extraPowerRatio = Cargo.powerTable[num4];
            }
            labPowerFields.replicating = true;
        }
        if (labPowerFields.replicating && time < producingLabRecipe.TimeSpend && extraTime < producingLabRecipe.ExtraTimeSpend)
        {
            time += (int)(power * speedOverride);
            extraTime += (int)(power * extraSpeed);
        }
        if (!labPowerFields.replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
        }
        return LabState.Active;
    }

    public readonly void UpdateOutputToNext(int labIndex,
                                            OptimizedProducingLab[] labPool,
                                            NetworkIdAndState<LabState>[] networkIdAndStates,
                                            ref readonly ProducingLabRecipe producingLabRecipe,
                                            GroupNeeds groupNeeds,
                                            short[] needs,
                                            int serveOffset,
                                            int producedSize,
                                            short[] served,
                                            short[] incServed,
                                            short[] produced)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        bool movedItems = false;
        int num14 = producingLabRecipe.TimeSpend > 5400000 ? 1 : 1 + speedOverride / 20000;
        int nextLabNeedsOffset = groupNeeds.GetObjectNeedsIndex(nextLabIndex);
        int nextLabServeOffset = groupNeeds.GroupNeedsSize * nextLabIndex;
        int recipeRequireCountLength = producingLabRecipe.RequireCounts.Length;
        for (int i = 0; i < recipeRequireCountLength; i++)
        {
            if (needs[nextLabNeedsOffset + i] == producingLabRecipe.Requires[i].ItemIndex && served[serveOffset + i] >= producingLabRecipe.RequireCounts[i] + num14)
            {
                int num15 = served[serveOffset + i] - producingLabRecipe.RequireCounts[i] - num14;
                if (num15 > 5)
                {
                    num15 = 5;
                }
                int num16 = num15 * incServed[serveOffset + i] / served[serveOffset + i];
                served[serveOffset + i] -= (short)num15;
                incServed[serveOffset + i] -= (short)num16;
                served[nextLabServeOffset + i] += (short)num15;
                incServed[nextLabServeOffset + i] += (short)num16;
                movedItems = true;
            }
        }

        int producedOffset = producedSize * labIndex;
        int nextLabProducedOffset = producedSize * nextLabIndex;
        int num17 = 10 * ((speedOverride + 9999) / 10000) - 2;
        if (produced[producedOffset] < num17 && produced[nextLabProducedOffset] > 0)
        {
            int num18 = num17 - produced[0] < produced[nextLabProducedOffset] ? num17 - produced[producedOffset] : produced[nextLabProducedOffset];
            produced[producedOffset] += (short)num18;
            produced[nextLabProducedOffset] -= (short)num18;
            movedItems = true;
        }

        if (movedItems)
        {
            networkIdAndStates[labIndex].State = (int)LabState.Active;
            networkIdAndStates[nextLabIndex].State = (int)LabState.Active;
        }
    }

    public readonly void Save(ref LabComponent lab,
                              LabPowerFields labPowerFields,
                              GroupNeeds groupNeeds,
                              short[] needs,
                              int producedSize,
                              short[] served,
                              short[] incServed,
                              short[] produced,
                              int labIndex)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * labIndex;
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            GroupNeeds.SetIfInRange(lab.served, served, i, servedOffset + i);
            GroupNeeds.SetIfInRange(lab.needs, needs, i, needsOffset + i);
            GroupNeeds.SetIfInRange(lab.incServed, incServed, i, servedOffset + i);
        }

        int producedOffset = labIndex * producedSize;
        for (int i = 0; i < producedSize; i++)
        {
            GroupNeeds.SetIfInRange(lab.produced, produced, i, producedOffset + i);
        }

        lab.replicating = labPowerFields.replicating;
        lab.incUsed = incUsed;
        lab.time = time;
        lab.extraTime = extraTime;
        lab.extraSpeed = extraSpeed;
        lab.extraPowerRatio = labPowerFields.extraPowerRatio;
        lab.speedOverride = speedOverride;
    }

    private static int split_inc_level(ref short n, ref short m, short p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        m -= (short)(num2 > 0 ? num * p + num2 : num * p);
        return num;
    }
}
