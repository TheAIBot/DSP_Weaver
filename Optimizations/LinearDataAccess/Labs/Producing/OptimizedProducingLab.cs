﻿using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Producing;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedProducingLab
{
    public const int NO_NEXT_LAB = -1;
    public readonly int producingLabRecipeIndex;
    public readonly bool forceAccMode;
    public readonly int[] served;
    public readonly int[] incServed;
    public readonly int[] needs;
    public readonly int[] produced;
    public readonly int nextLabIndex;
    public bool incUsed;
    public int time;
    public int extraTime;
    public int extraSpeed;
    public int speedOverride;

    public OptimizedProducingLab(int producingLabRecipeIndex,
                                 int? nextLabIndex,
                                 ref readonly LabComponent lab)
    {
        this.producingLabRecipeIndex = producingLabRecipeIndex;
        forceAccMode = lab.forceAccMode;
        served = lab.served;
        incServed = lab.incServed;
        needs = lab.needs;
        produced = lab.produced;
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
        producingLabRecipeIndex = lab.producingLabRecipeIndex;
        forceAccMode = lab.forceAccMode;
        served = lab.served;
        incServed = lab.incServed;
        needs = lab.needs;
        produced = lab.produced;
        this.nextLabIndex = nextLabIndex;
        incUsed = lab.incUsed;
        time = lab.time;
        extraTime = lab.extraTime;
        extraSpeed = lab.extraSpeed;
        speedOverride = lab.speedOverride;
    }

    public readonly void UpdateNeedsAssemble(ref readonly ProducingLabRecipe producingLabRecipe)
    {
        int num = served.Length;
        int num2 = producingLabRecipe.TimeSpend > 5400000 ? 6 : 3 * ((speedOverride + 5001) / 10000) + 3;
        needs[0] = 0 < num && served[0] < num2 ? producingLabRecipe.Requires[0].ItemIndex : 0;
        needs[1] = 1 < num && served[1] < num2 ? producingLabRecipe.Requires[1].ItemIndex : 0;
        needs[2] = 2 < num && served[2] < num2 ? producingLabRecipe.Requires[2].ItemIndex : 0;
        needs[3] = 3 < num && served[3] < num2 ? producingLabRecipe.Requires[3].ItemIndex : 0;
        needs[4] = 4 < num && served[4] < num2 ? producingLabRecipe.Requires[4].ItemIndex : 0;
        needs[5] = 5 < num && served[5] < num2 ? producingLabRecipe.Requires[5].ItemIndex : 0;
    }

    public LabState InternalUpdateAssemble(float power,
                                           int[] productRegister,
                                           int[] consumeRegister,
                                           ref readonly ProducingLabRecipe producingLabRecipe,
                                           ref LabPowerFields labPowerFields)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return LabState.Active;
        }
        if (extraTime >= producingLabRecipe.ExtraTimeSpend)
        {
            int num = producingLabRecipe.Products.Length;
            if (num == 1)
            {
                produced[0] += producingLabRecipe.ProductCounts[0];
                productRegister[producingLabRecipe.Products[0].OptimizedItemIndex] += producingLabRecipe.ProductCounts[0];
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    produced[i] += producingLabRecipe.ProductCounts[i];
                    productRegister[producingLabRecipe.Products[i].OptimizedItemIndex] += producingLabRecipe.ProductCounts[i];
                }
            }
            extraTime -= producingLabRecipe.ExtraTimeSpend;
        }
        if (time >= producingLabRecipe.TimeSpend)
        {
            labPowerFields.replicating = false;
            int num2 = producingLabRecipe.Products.Length;
            if (num2 == 1)
            {
                if (produced[0] + producingLabRecipe.ProductCounts[0] > 10 * ((speedOverride + 9999) / 10000))
                {
                    return LabState.InactiveOutputFull;
                }
                produced[0] += producingLabRecipe.ProductCounts[0];
                productRegister[producingLabRecipe.Products[0].OptimizedItemIndex] += producingLabRecipe.ProductCounts[0];
            }
            else
            {
                for (int j = 0; j < num2; j++)
                {
                    if (produced[j] + producingLabRecipe.ProductCounts[j] > 10 * ((speedOverride + 9999) / 10000))
                    {
                        return LabState.InactiveOutputFull;
                    }
                }
                for (int k = 0; k < num2; k++)
                {
                    produced[k] += producingLabRecipe.ProductCounts[k];
                    productRegister[producingLabRecipe.Products[k].OptimizedItemIndex] += producingLabRecipe.ProductCounts[k];
                }
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
                if (incServed[l] <= 0)
                {
                    incServed[l] = 0;
                }
                if (served[l] < producingLabRecipe.RequireCounts[l] || served[l] == 0)
                {
                    time = 0;
                    return LabState.InactiveInputMissing;
                }
            }
            int num4 = num3 > 0 ? 10 : 0;
            for (int m = 0; m < num3; m++)
            {
                int num5 = split_inc_level(ref served[m], ref incServed[m], producingLabRecipe.RequireCounts[m]);
                num4 = num4 < num5 ? num4 : num5;
                if (!incUsed)
                {
                    incUsed = num5 > 0;
                }
                if (served[m] == 0)
                {
                    incServed[m] = 0;
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
                                            ref readonly ProducingLabRecipe producingLabRecipe)
    {
        if (nextLabIndex == NO_NEXT_LAB)
        {
            return;
        }

        bool movedItems = false;
        if (served != null && labPool[nextLabIndex].served != null)
        {
            int num13 = served.Length;
            int num14 = producingLabRecipe.TimeSpend > 5400000 ? 1 : 1 + speedOverride / 20000;
            for (int i = 0; i < num13; i++)
            {
                if (labPool[nextLabIndex].needs[i] == producingLabRecipe.Requires[i].ItemIndex && served[i] >= producingLabRecipe.RequireCounts[i] + num14)
                {
                    int num15 = served[i] - producingLabRecipe.RequireCounts[i] - num14;
                    if (num15 > 5)
                    {
                        num15 = 5;
                    }
                    int num16 = num15 * incServed[i] / served[i];
                    served[i] -= num15;
                    incServed[i] -= num16;
                    labPool[nextLabIndex].served[i] += num15;
                    labPool[nextLabIndex].incServed[i] += num16;
                    movedItems = true;
                }
            }
        }

        int num17 = 10 * ((speedOverride + 9999) / 10000) - 2;
        if (produced[0] < num17 && labPool[nextLabIndex].produced[0] > 0)
        {
            int num18 = num17 - produced[0] < labPool[nextLabIndex].produced[0] ? num17 - produced[0] : labPool[nextLabIndex].produced[0];
            produced[0] += num18;
            labPool[nextLabIndex].produced[0] -= num18;
            movedItems = true;
        }

        if (movedItems)
        {
            networkIdAndStates[labIndex].State = (int)LabState.Active;
            networkIdAndStates[nextLabIndex].State = (int)LabState.Active;
        }
    }

    public readonly void Save(ref LabComponent lab,
                              LabPowerFields labPowerFields)
    {
        lab.served = served;
        lab.incServed = incServed;
        lab.needs = needs;
        lab.produced = produced;
        lab.replicating = labPowerFields.replicating;
        lab.incUsed = incUsed;
        lab.time = time;
        lab.extraTime = extraTime;
        lab.extraSpeed = extraSpeed;
        lab.extraPowerRatio = labPowerFields.extraPowerRatio;
        lab.speedOverride = speedOverride;
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
