using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

internal struct AssemblerTimingData
{
    public int time;
    public int extraTime;
    public int speedOverride;
    public int extraSpeed;

    public AssemblerTimingData(ref readonly AssemblerComponent assembler)
    {
        time = assembler.time;
        extraTime = assembler.extraTime;
        speedOverride = assembler.speedOverride;
        extraSpeed = assembler.extraSpeed;
    }

    public bool UpdateTimings(float power, bool replicating, ref readonly AssemblerRecipe assemblerRecipeData)
    {
        if (replicating && time < assemblerRecipeData.TimeSpend && extraTime < assemblerRecipeData.ExtraTimeSpend)
        {
            time += (int)(power * speedOverride);
            extraTime += (int)(power * extraSpeed);
            return false;
        }

        return true;
    }
}

internal readonly struct AssemblerNeeds
{
    public readonly int[] served;
    public readonly int[] needs;

    public AssemblerNeeds(ref readonly AssemblerComponent assembler)
    {
        served = assembler.served;
        needs = assembler.needs;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedAssembler
{
    public readonly bool forceAccMode;
    public readonly int speed;
    public readonly int[] incServed;
    public readonly int[] produced;
    public bool incUsed;
    public int cycleCount;
    public int extraCycleCount;

    public OptimizedAssembler(ref readonly AssemblerComponent assembler)
    {
        forceAccMode = assembler.forceAccMode;
        speed = assembler.speed;
        incServed = assembler.incServed;
        produced = assembler.produced;
        incUsed = assembler.incUsed;
        cycleCount = assembler.cycleCount;
        extraCycleCount = assembler.extraCycleCount;
    }

    public static void UpdateNeeds(ref readonly AssemblerRecipe assemblerRecipeData,
                                   ref readonly AssemblerTimingData assemblerTimingData,
                                   ref readonly AssemblerNeeds assemblerNeeds)
    {
        int num = assemblerRecipeData.Requires.Length;
        int num2 = assemblerTimingData.speedOverride * 180 / assemblerRecipeData.TimeSpend + 1;
        if (num2 < 2)
        {
            num2 = 2;
        }

        int[] served = assemblerNeeds.served;
        int[] needs = assemblerNeeds.needs;
        needs[0] = 0 < num && served[0] < assemblerRecipeData.RequireCounts[0] * num2 ? assemblerRecipeData.Requires[0] : 0;
        needs[1] = 1 < num && served[1] < assemblerRecipeData.RequireCounts[1] * num2 ? assemblerRecipeData.Requires[1] : 0;
        needs[2] = 2 < num && served[2] < assemblerRecipeData.RequireCounts[2] * num2 ? assemblerRecipeData.Requires[2] : 0;
        needs[3] = 3 < num && served[3] < assemblerRecipeData.RequireCounts[3] * num2 ? assemblerRecipeData.Requires[3] : 0;
        needs[4] = 4 < num && served[4] < assemblerRecipeData.RequireCounts[4] * num2 ? assemblerRecipeData.Requires[4] : 0;
        needs[5] = 5 < num && served[5] < assemblerRecipeData.RequireCounts[5] * num2 ? assemblerRecipeData.Requires[5] : 0;
    }

    public AssemblerState Update(float power,
                                 int[] productRegister,
                                 int[] consumeRegister,
                                 ref readonly AssemblerRecipe assemblerRecipeData,
                                 ref bool replicating,
                                 ref int extraPowerRatio,
                                 ref AssemblerTimingData assemblerTimingData,
                                 ref readonly AssemblerNeeds assemblerNeeds)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return AssemblerState.Active;
        }

        if (assemblerTimingData.extraTime >= assemblerRecipeData.ExtraTimeSpend)
        {
            int num = assemblerRecipeData.Products.Length;
            if (num == 1)
            {
                produced[0] += assemblerRecipeData.ProductCounts[0];
                lock (productRegister)
                {
                    productRegister[assemblerRecipeData.Products[0]] += assemblerRecipeData.ProductCounts[0];
                }
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    produced[i] += assemblerRecipeData.ProductCounts[i];
                    lock (productRegister)
                    {
                        productRegister[assemblerRecipeData.Products[i]] += assemblerRecipeData.ProductCounts[i];
                    }
                }
            }
            extraCycleCount++;
            assemblerTimingData.extraTime -= assemblerRecipeData.ExtraTimeSpend;
        }
        if (assemblerTimingData.time >= assemblerRecipeData.TimeSpend)
        {
            replicating = false;
            if (assemblerRecipeData.Products.Length == 1)
            {
                switch (assemblerRecipeData.RecipeType)
                {
                    case ERecipeType.Smelt:
                        if (produced[0] + assemblerRecipeData.ProductCounts[0] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    case ERecipeType.Assemble:
                        if (produced[0] > assemblerRecipeData.ProductCounts[0] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    default:
                        if (produced[0] > assemblerRecipeData.ProductCounts[0] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                }
                produced[0] += assemblerRecipeData.ProductCounts[0];
                lock (productRegister)
                {
                    productRegister[assemblerRecipeData.Products[0]] += assemblerRecipeData.ProductCounts[0];
                }
            }
            else
            {
                int num2 = assemblerRecipeData.Products.Length;
                if (assemblerRecipeData.RecipeType == ERecipeType.Refine)
                {
                    for (int j = 0; j < num2; j++)
                    {
                        if (produced[j] > assemblerRecipeData.ProductCounts[j] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Particle)
                {
                    for (int k = 0; k < num2; k++)
                    {
                        if (produced[k] > assemblerRecipeData.ProductCounts[k] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Chemical)
                {
                    for (int l = 0; l < num2; l++)
                    {
                        if (produced[l] > assemblerRecipeData.ProductCounts[l] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Smelt)
                {
                    for (int m = 0; m < num2; m++)
                    {
                        if (produced[m] + assemblerRecipeData.ProductCounts[m] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assemblerRecipeData.RecipeType == ERecipeType.Assemble)
                {
                    for (int n = 0; n < num2; n++)
                    {
                        if (produced[n] > assemblerRecipeData.ProductCounts[n] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else
                {
                    for (int num3 = 0; num3 < num2; num3++)
                    {
                        if (produced[num3] > assemblerRecipeData.ProductCounts[num3] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                for (int num4 = 0; num4 < num2; num4++)
                {
                    produced[num4] += assemblerRecipeData.ProductCounts[num4];
                    lock (productRegister)
                    {
                        productRegister[assemblerRecipeData.Products[num4]] += assemblerRecipeData.ProductCounts[num4];
                    }
                }
            }
            assemblerTimingData.extraSpeed = 0;
            assemblerTimingData.speedOverride = speed;
            extraPowerRatio = 0;
            cycleCount++;
            assemblerTimingData.time -= assemblerRecipeData.TimeSpend;
        }
        if (!replicating)
        {
            int num5 = assemblerRecipeData.RequireCounts.Length;
            for (int num6 = 0; num6 < num5; num6++)
            {
                if (incServed[num6] <= 0)
                {
                    incServed[num6] = 0;
                }
                if (assemblerNeeds.served[num6] < assemblerRecipeData.RequireCounts[num6] || assemblerNeeds.served[num6] == 0)
                {
                    assemblerTimingData.time = 0;
                    return AssemblerState.InactiveInputMissing;
                }
            }
            int num7 = num5 > 0 ? 10 : 0;
            for (int num8 = 0; num8 < num5; num8++)
            {
                int num9 = split_inc_level(ref assemblerNeeds.served[num8], ref incServed[num8], assemblerRecipeData.RequireCounts[num8]);
                num7 = num7 < num9 ? num7 : num9;
                if (!incUsed)
                {
                    incUsed = num9 > 0;
                }
                if (assemblerNeeds.served[num8] == 0)
                {
                    incServed[num8] = 0;
                }
                lock (consumeRegister)
                {
                    consumeRegister[assemblerRecipeData.Requires[num8]] += assemblerRecipeData.RequireCounts[num8];
                }
            }
            if (num7 < 0)
            {
                num7 = 0;
            }
            if (assemblerRecipeData.Productive && !forceAccMode)
            {
                assemblerTimingData.extraSpeed = (int)(speed * Cargo.incTableMilli[num7] * 10.0 + 0.1);
                assemblerTimingData.speedOverride = speed;
                extraPowerRatio = Cargo.powerTable[num7];
            }
            else
            {
                assemblerTimingData.extraSpeed = 0;
                assemblerTimingData.speedOverride = (int)(speed * (1.0 + Cargo.accTableMilli[num7]) + 0.1);
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
                              ref readonly AssemblerRecipe assemblerRecipeData,
                              bool replicating,
                              int extraPowerRatio,
                              ref readonly AssemblerTimingData assemblerTimingData,
                              ref readonly AssemblerNeeds assemblerNeeds)
    {
        assembler.requires = assemblerRecipeData.Requires;
        assembler.requireCounts = assemblerRecipeData.RequireCounts;
        assembler.products = assemblerRecipeData.Products;
        assembler.productCounts = assemblerRecipeData.ProductCounts;
        assembler.served = assemblerNeeds.served;
        assembler.incServed = incServed;
        assembler.needs = assemblerNeeds.needs;
        assembler.produced = produced;
        assembler.incUsed = incUsed;
        assembler.speedOverride = assemblerTimingData.speedOverride;
        assembler.time = assemblerTimingData.time;
        assembler.extraTime = assemblerTimingData.extraTime;
        assembler.cycleCount = cycleCount;
        assembler.extraCycleCount = extraCycleCount;
        assembler.extraSpeed = assemblerTimingData.extraSpeed;
        assembler.replicating = replicating;
        assembler.extraPowerRatio = extraPowerRatio;
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