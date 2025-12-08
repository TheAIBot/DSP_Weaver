using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Producing;

internal struct ProducingLabTimingData
{
    public int Time;
    public int ExtraTime;
    public int SpeedOverride;
    public int ExtraSpeed;

    public ProducingLabTimingData(ref readonly LabComponent lab)
    {
        Time = lab.time;
        ExtraTime = lab.extraTime;
        SpeedOverride = lab.speedOverride;
        ExtraSpeed = lab.extraSpeed;
    }

    public bool UpdateTimings(float power, bool replicating, ref readonly ProducingLabRecipe producingLabRecipe)
    {
        if (replicating && Time < producingLabRecipe.TimeSpend && ExtraTime < producingLabRecipe.ExtraTimeSpend)
        {
            Time += (int)(power * SpeedOverride);
            ExtraTime += (int)(power * ExtraSpeed);
            return false;
        }

        return true;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedProducingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly bool forceAccMode;
    public readonly int nextLabIndex;
    public bool incUsed;

    public OptimizedProducingLab(int? nextLabIndex,
                                 ref readonly LabComponent lab)
    {
        forceAccMode = lab.forceAccMode;
        this.nextLabIndex = nextLabIndex.HasValue ? nextLabIndex.Value : NO_NEXT_LAB;
        incUsed = lab.incUsed;
    }

    public OptimizedProducingLab(int nextLabIndex,
                                 ref readonly OptimizedProducingLab lab)
    {
        forceAccMode = lab.forceAccMode;
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
    }

    public static void UpdateNeedsAssemble(ref readonly ProducingLabRecipe producingLabRecipe,
                                           ref readonly ProducingLabTimingData producingLabTimingData,
                                           GroupNeeds groupNeeds,
                                           short[] served,
                                           ComponentNeeds[] componentsNeeds,
                                           int labIndex)
    {
        int num2 = producingLabRecipe.TimeSpend > 5400000 ? 6 : 3 * ((producingLabTimingData.SpeedOverride + 5001) / 10000) + 3;

        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * labIndex;
        OptimizedItemId[] requires = producingLabRecipe.Requires;
        byte needBits = 0;
        for (int i = 0; i < requires.Length; i++)
        {
            needBits |= (byte)((served[servedOffset + i] < num2 ? 1 : 0) << i);
        }

        componentsNeeds[needsOffset].Needs = needBits;
    }

    public LabState InternalUpdateAssemble(float power,
                                           int[] productRegister,
                                           int[] consumeRegister,
                                           ref readonly ProducingLabRecipe producingLabRecipe,
                                           ref LabPowerFields labPowerFields,
                                           ref ProducingLabTimingData producingLabTimingData,
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
        if (producingLabTimingData.ExtraTime >= producingLabRecipe.ExtraTimeSpend)
        {
            for (int i = 0; i < producingLabRecipe.ProductCounts.Length; i++)
            {
                produced[producedOffset + i] += (short)producingLabRecipe.ProductCounts[i];
                productRegister[producingLabRecipe.Products[i].OptimizedItemIndex] += producingLabRecipe.ProductCounts[i];
            }
            producingLabTimingData.ExtraTime -= producingLabRecipe.ExtraTimeSpend;
        }
        if (producingLabTimingData.Time >= producingLabRecipe.TimeSpend)
        {
            labPowerFields.replicating = false;
            for (int j = 0; j < producingLabRecipe.Products.Length; j++)
            {
                if (produced[producedOffset + j] + producingLabRecipe.ProductCounts[j] > 10 * ((producingLabTimingData.SpeedOverride + 9999) / 10000))
                {
                    return LabState.InactiveOutputFull;
                }
            }
            for (int k = 0; k < producingLabRecipe.Products.Length; k++)
            {
                produced[producedOffset + k] += (short)producingLabRecipe.ProductCounts[k];
                productRegister[producingLabRecipe.Products[k].OptimizedItemIndex] += producingLabRecipe.ProductCounts[k];
            }
            producingLabTimingData.ExtraSpeed = 0;
            producingLabTimingData.SpeedOverride = producingLabRecipe.Speed;
            labPowerFields.extraPowerRatio = 0;
            producingLabTimingData.Time -= producingLabRecipe.TimeSpend;
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
                    producingLabTimingData.Time = 0;
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
                producingLabTimingData.ExtraSpeed = (int)(producingLabRecipe.Speed * Cargo.incTableMilli[num4] * 10.0 + 0.1);
                producingLabTimingData.SpeedOverride = producingLabRecipe.Speed;
                labPowerFields.extraPowerRatio = Cargo.powerTable[num4];
            }
            else
            {
                producingLabTimingData.ExtraSpeed = 0;
                producingLabTimingData.SpeedOverride = (int)(producingLabRecipe.Speed * (1.0 + Cargo.accTableMilli[num4]) + 0.1);
                labPowerFields.extraPowerRatio = Cargo.powerTable[num4];
            }
            labPowerFields.replicating = true;
        }
        producingLabTimingData.UpdateTimings(power, labPowerFields.replicating, in producingLabRecipe);
        if (!labPowerFields.replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
        }
        return LabState.Active;
    }

    public readonly void UpdateOutputToNext(int labIndex,
                                            OptimizedProducingLab[] labPool,
                                            LabState[] labStates,
                                            bool[] needToUpdateNeeds,
                                            ref readonly ProducingLabRecipe producingLabRecipe,
                                            ref readonly ProducingLabTimingData producingLabTimingData,
                                            GroupNeeds groupNeeds,
                                            ComponentNeeds[] componentsNeeds,
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
        int num14 = producingLabRecipe.TimeSpend > 5400000 ? 1 : 1 + producingLabTimingData.SpeedOverride / 20000;
        int nextLabNeedsOffset = groupNeeds.GetObjectNeedsIndex(nextLabIndex);
        int nextLabServeOffset = groupNeeds.GroupNeedsSize * nextLabIndex;
        int recipeRequireCountLength = producingLabRecipe.RequireCounts.Length;
        ComponentNeeds nextLabNeeds = componentsNeeds[nextLabNeedsOffset];
        for (int i = 0; i < recipeRequireCountLength; i++)
        {
            int num115 = producingLabRecipe.RequireCounts[i] + num14;
            if (nextLabNeeds.GetNeeds(i) && served[serveOffset + i] >= num115)
            {
                int num15 = served[serveOffset + i] - num115;
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
        int num17 = 10 * ((producingLabTimingData.SpeedOverride + 9999) / 10000) - 2;
        if (produced[producedOffset] < num17 && produced[nextLabProducedOffset] > 0)
        {
            int num18 = num17 - produced[producedOffset] < produced[nextLabProducedOffset] ? num17 - produced[producedOffset] : produced[nextLabProducedOffset];
            produced[producedOffset] += (short)num18;
            produced[nextLabProducedOffset] -= (short)num18;
            movedItems = true;
        }

        if (movedItems)
        {
            labStates[labIndex] = LabState.Active;
            labStates[nextLabIndex] = LabState.Active;
            needToUpdateNeeds[labIndex] = true;
            needToUpdateNeeds[nextLabIndex] = true;
        }
    }

    public readonly void Save(ref LabComponent lab,
                              LabPowerFields labPowerFields,
                              ProducingLabTimingData producingLabTimingData,
                              GroupNeeds groupNeeds,
                              ComponentNeeds[] componentsNeeds,
                              short[] needsPatterns,
                              int producedSize,
                              short[] served,
                              short[] incServed,
                              short[] produced,
                              int labIndex)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(labIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * labIndex;
        ComponentNeeds componentNeeds = componentsNeeds[needsOffset];
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            GroupNeeds.SetIfInRange(lab.served, served, i, servedOffset + i);
            GroupNeeds.SetNeedsIfInRange(lab.needs, componentNeeds, needsPatterns, i);
            GroupNeeds.SetIfInRange(lab.incServed, incServed, i, servedOffset + i);
        }

        int producedOffset = labIndex * producedSize;
        for (int i = 0; i < producedSize; i++)
        {
            GroupNeeds.SetIfInRange(lab.produced, produced, i, producedOffset + i);
        }

        lab.incUsed = incUsed;
        lab.replicating = labPowerFields.replicating;
        lab.extraPowerRatio = labPowerFields.extraPowerRatio;
        lab.time = producingLabTimingData.Time;
        lab.extraTime = producingLabTimingData.ExtraTime;
        lab.extraSpeed = producingLabTimingData.ExtraSpeed;
        lab.speedOverride = producingLabTimingData.SpeedOverride;
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
