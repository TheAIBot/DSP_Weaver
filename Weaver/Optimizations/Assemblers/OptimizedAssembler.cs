using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Assemblers;

internal struct AssemblerTimingData
{
    public int Time;
    public int ExtraTime;
    public int ExtraSpeed;
    public int SpeedOverride;

    public AssemblerTimingData(ref readonly AssemblerComponent assembler)
    {
        Time = assembler.time;
        ExtraTime = assembler.extraTime;
        SpeedOverride = assembler.speedOverride;
        ExtraSpeed = assembler.extraSpeed;
    }

    public bool UpdateTimings(float power, bool replicating, ref readonly AssemblerRecipe assemblerRecipeData)
    {
        if (replicating && Time < assemblerRecipeData.TimeSpend && ExtraTime < assemblerRecipeData.ExtraTimeSpend)
        {
            Time += (int)(power * SpeedOverride);
            ExtraTime += (int)(power * ExtraSpeed);
            return false;
        }

        return true;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedAssembler
{
    public readonly bool forceAccMode;
    public bool incUsed;
    public int cycleCount;
    public int extraCycleCount;

    public OptimizedAssembler(ref readonly AssemblerComponent assembler)
    {
        forceAccMode = assembler.forceAccMode;
        incUsed = assembler.incUsed;
        cycleCount = assembler.cycleCount;
        extraCycleCount = assembler.extraCycleCount;
    }

    public static void UpdateNeeds(ref readonly AssemblerRecipe assemblerRecipeData,
                                   ref readonly AssemblerTimingData assemblerTimingData,
                                   GroupNeeds groupNeeds,
                                   short[] served,
                                   ComponentNeeds[] componentsNeeds,
                                   int assemblerIndex)
    {
        int num2 = assemblerTimingData.SpeedOverride * 180 / assemblerRecipeData.TimeSpend + 1;
        if (num2 < 2)
        {
            num2 = 2;
        }

        int needsOffset = groupNeeds.GetObjectNeedsIndex(assemblerIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * assemblerIndex;
        ReadonlyArray<short> requireCounts = assemblerRecipeData.RequireCounts;
        byte needBits = 0;
        for (int i = 0; i < requireCounts.Length; i++)
        {
            needBits |= (byte)((served[servedOffset + i] < requireCounts[i] * num2 ? 1 : 0) << i);
        }

        componentsNeeds[needsOffset].Needs = needBits;
    }

    public AssemblerState Update(float power,
                                 int[] productRegister,
                                 int[] consumeRegister,
                                 ref readonly AssemblerRecipe assemblerRecipeData,
                                 ref bool replicating,
                                 ref int extraPowerRatio,
                                 ref AssemblerTimingData assemblerTimingData,
                                 int servedOffset,
                                 int producedOffset,
                                 short[] served,
                                 short[] incServed,
                                 short[] produced)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return AssemblerState.Active;
        }

        if (assemblerTimingData.ExtraTime >= assemblerRecipeData.ExtraTimeSpend)
        {
            for (int i = 0; i < assemblerRecipeData.ProductCounts.Length; i++)
            {
                produced[producedOffset + i] += (short)assemblerRecipeData.ProductCounts[i];
                productRegister[assemblerRecipeData.Products[i].OptimizedItemIndex] += assemblerRecipeData.ProductCounts[i];
            }
            extraCycleCount++;
            assemblerTimingData.ExtraTime -= assemblerRecipeData.ExtraTimeSpend;
        }
        if (assemblerTimingData.Time >= assemblerRecipeData.TimeSpend)
        {
            replicating = false;
            if (assemblerRecipeData.Products.Length == 1)
            {
                switch (assemblerRecipeData.RecipeType)
                {
                    case ERecipeType.Smelt:
                        if (produced[producedOffset] + assemblerRecipeData.ProductCounts[0] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    case ERecipeType.Assemble:
                        if (produced[producedOffset] > assemblerRecipeData.ProductCounts[0] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    default:
                        if (produced[producedOffset] > assemblerRecipeData.ProductCounts[0] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                }
                produced[producedOffset] += (short)assemblerRecipeData.ProductCounts[0];
                productRegister[assemblerRecipeData.Products[0].OptimizedItemIndex] += assemblerRecipeData.ProductCounts[0];
            }
            else
            {
                int num2 = assemblerRecipeData.Products.Length;
                if (assemblerRecipeData.RecipeType == ERecipeType.Refine)
                {
                    for (int j = 0; j < num2; j++)
                    {
                        if (produced[producedOffset + j] > assemblerRecipeData.ProductCounts[j] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Particle)
                {
                    for (int k = 0; k < num2; k++)
                    {
                        if (produced[producedOffset + k] > assemblerRecipeData.ProductCounts[k] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Chemical)
                {
                    for (int l = 0; l < num2; l++)
                    {
                        if (produced[producedOffset + l] > assemblerRecipeData.ProductCounts[l] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Smelt)
                {
                    for (int m = 0; m < num2; m++)
                    {
                        if (produced[producedOffset + m] + assemblerRecipeData.ProductCounts[m] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Assemble)
                {
                    for (int n = 0; n < num2; n++)
                    {
                        if (produced[producedOffset + n] > assemblerRecipeData.ProductCounts[n] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else
                {
                    for (int num3 = 0; num3 < num2; num3++)
                    {
                        if (produced[producedOffset + num3] > assemblerRecipeData.ProductCounts[num3] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                for (int num4 = 0; num4 < num2; num4++)
                {
                    produced[producedOffset + num4] += assemblerRecipeData.ProductCounts[num4];
                    productRegister[assemblerRecipeData.Products[num4].OptimizedItemIndex] += assemblerRecipeData.ProductCounts[num4];
                }
            }
            assemblerTimingData.ExtraSpeed = 0;
            assemblerTimingData.SpeedOverride = assemblerRecipeData.Speed;
            extraPowerRatio = 0;
            cycleCount++;
            assemblerTimingData.Time -= assemblerRecipeData.TimeSpend;
        }
        if (!replicating)
        {
            int num5 = assemblerRecipeData.RequireCounts.Length;
            for (int num6 = 0; num6 < num5; num6++)
            {
                if (incServed[servedOffset + num6] <= 0)
                {
                    incServed[servedOffset + num6] = 0;
                }
                if (served[servedOffset + num6] < assemblerRecipeData.RequireCounts[num6] || served[servedOffset + num6] == 0)
                {
                    assemblerTimingData.Time = 0;
                    return AssemblerState.InactiveInputMissing;
                }
            }
            int num7 = num5 > 0 ? 10 : 0;
            for (int num8 = 0; num8 < num5; num8++)
            {
                int num9 = split_inc_level(ref served[servedOffset + num8], ref incServed[servedOffset + num8], (short)assemblerRecipeData.RequireCounts[num8]);
                num7 = num7 < num9 ? num7 : num9;
                if (!incUsed)
                {
                    incUsed = num9 > 0;
                }
                if (served[servedOffset + num8] == 0)
                {
                    incServed[servedOffset + num8] = 0;
                }
                consumeRegister[assemblerRecipeData.Requires[num8].OptimizedItemIndex] += assemblerRecipeData.RequireCounts[num8];
            }
            if (num7 < 0)
            {
                num7 = 0;
            }
            if (assemblerRecipeData.Productive && !forceAccMode)
            {
                assemblerTimingData.ExtraSpeed = (int)(assemblerRecipeData.Speed * Cargo.incTableMilli[num7] * 10.0 + 0.1);
                assemblerTimingData.SpeedOverride = assemblerRecipeData.Speed;
                extraPowerRatio = Cargo.powerTable[num7];
            }
            else
            {
                assemblerTimingData.ExtraSpeed = 0;
                assemblerTimingData.SpeedOverride = (int)(assemblerRecipeData.Speed * (1.0 + Cargo.accTableMilli[num7]) + 0.1);
                extraPowerRatio = Cargo.powerTable[num7];
            }
            replicating = true;
        }
        assemblerTimingData.UpdateTimings(power, replicating, in assemblerRecipeData);
        if (!replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
        }
        return AssemblerState.Active;
    }

    public readonly void Save(ref AssemblerComponent assembler,
                              bool replicating,
                              int extraPowerRatio,
                              ref readonly AssemblerTimingData assemblerTimingData,
                              GroupNeeds groupNeeds,
                              ComponentNeeds[] componentsNeeds,
                              short[] needsPatterns,
                              int producedSize,
                              short[] served,
                              short[] incServed,
                              short[] produced,
                              int assemblerIndex)
    {
        int needsOffset = groupNeeds.GetObjectNeedsIndex(assemblerIndex);
        int servedOffset = groupNeeds.GroupNeedsSize * assemblerIndex;
        ComponentNeeds componentNeeds = componentsNeeds[needsOffset];
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            GroupNeeds.SetIfInRange(assembler.served, served, i, servedOffset + i);
            GroupNeeds.SetNeedsIfInRange(assembler.needs, componentNeeds, needsPatterns, i);
            GroupNeeds.SetIfInRange(assembler.incServed, incServed, i, servedOffset + i);
        }

        int producedOffset = assemblerIndex * producedSize;
        for (int i = 0; i < producedSize; i++)
        {
            GroupNeeds.SetIfInRange(assembler.produced, produced, i, producedOffset + i);
        }

        assembler.incUsed = incUsed;
        assembler.speedOverride = assemblerTimingData.SpeedOverride;
        assembler.time = assemblerTimingData.Time;
        assembler.extraTime = assemblerTimingData.ExtraTime;
        assembler.cycleCount = cycleCount;
        assembler.extraCycleCount = extraCycleCount;
        assembler.extraSpeed = assemblerTimingData.ExtraSpeed;
        assembler.replicating = replicating;
        assembler.extraPowerRatio = extraPowerRatio;
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