using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedAssembler
{
    public readonly int assemblerRecipeIndex;
    public readonly bool forceAccMode;
    public readonly int speed;
    public readonly int[] served;
    public readonly int[] incServed;
    public readonly int[] needs;
    public readonly int[] produced;
    public bool incUsed;
    public int speedOverride; // Can move out but need to move logic to creation
    public int time;
    public int extraTime;
    public int cycleCount;
    public int extraCycleCount;
    public int extraSpeed;

    public OptimizedAssembler(int assemblerRecipeIndex,
                              ref readonly AssemblerComponent assembler)
    {
        this.assemblerRecipeIndex = assemblerRecipeIndex;
        forceAccMode = assembler.forceAccMode;
        speed = assembler.speed;
        served = assembler.served;
        incServed = assembler.incServed;
        needs = assembler.needs;
        produced = assembler.produced;
        incUsed = assembler.incUsed;
        speedOverride = assembler.speedOverride;
        time = assembler.time;
        extraTime = assembler.extraTime;
        cycleCount = assembler.cycleCount;
        extraCycleCount = assembler.extraCycleCount;
        extraSpeed = assembler.extraSpeed;
    }

    public void UpdateNeeds(ref readonly AssemblerRecipe assemblerRecipeData)
    {
        int num = assemblerRecipeData.Requires.Length;
        int num2 = speedOverride * 180 / assemblerRecipeData.TimeSpend + 1;
        if (num2 < 2)
        {
            num2 = 2;
        }
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
                                 ref int extraPowerRatio)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return AssemblerState.Active;
        }

        if (extraTime >= assemblerRecipeData.ExtraTimeSpend)
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
            extraTime -= assemblerRecipeData.ExtraTimeSpend;
        }
        if (time >= assemblerRecipeData.TimeSpend)
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
            extraSpeed = 0;
            speedOverride = speed;
            extraPowerRatio = 0;
            cycleCount++;
            time -= assemblerRecipeData.TimeSpend;
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
                if (served[num6] < assemblerRecipeData.RequireCounts[num6] || served[num6] == 0)
                {
                    time = 0;
                    return AssemblerState.InactiveInputMissing;
                }
            }
            int num7 = num5 > 0 ? 10 : 0;
            for (int num8 = 0; num8 < num5; num8++)
            {
                int num9 = split_inc_level(ref served[num8], ref incServed[num8], assemblerRecipeData.RequireCounts[num8]);
                num7 = num7 < num9 ? num7 : num9;
                if (!incUsed)
                {
                    incUsed = num9 > 0;
                }
                if (served[num8] == 0)
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
                extraSpeed = (int)(speed * Cargo.incTableMilli[num7] * 10.0 + 0.1);
                speedOverride = speed;
                extraPowerRatio = Cargo.powerTable[num7];
            }
            else
            {
                extraSpeed = 0;
                speedOverride = (int)(speed * (1.0 + Cargo.accTableMilli[num7]) + 0.1);
                extraPowerRatio = Cargo.powerTable[num7];
            }
            replicating = true;
        }
        if (replicating && time < assemblerRecipeData.TimeSpend && extraTime < assemblerRecipeData.ExtraTimeSpend)
        {
            time += (int)(power * speedOverride);
            extraTime += (int)(power * extraSpeed);
        }
        if (!replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
            //return 0u;
        }
        return AssemblerState.Active;
    }

    private int split_inc_level(ref int n, ref int m, int p)
    {
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        m -= num2 > 0 ? num * p + num2 : num * p;
        return num;
    }
}