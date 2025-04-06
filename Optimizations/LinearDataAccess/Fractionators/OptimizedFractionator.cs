﻿using System.Runtime.InteropServices;
using UnityEngine;
#nullable enable

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedFractionator
{
    public readonly CargoPath belt0;
    public readonly CargoPath belt1;
    public readonly CargoPath belt2;
    public readonly int configurationIndex;
    public int fluidId;
    public int productId;
    public float produceProb;
    public bool isWorking;
    public int fluidInputCount;
    public float fluidInputCargoCount;
    public int fluidInputInc;
    public int productOutputCount;
    public int fluidOutputCount;
    public int fluidOutputInc;
    public int progress;
    public bool fractionSuccess;
    public bool incUsed;
    public int fluidOutputTotal;
    public int productOutputTotal;
    public uint seed;

    public OptimizedFractionator(CargoPath belt0,
                                 CargoPath belt1,
                                 CargoPath belt2,
                                 int configurationIndex,
                                 ref readonly FractionatorComponent fractionator)
    {
        this.belt0 = belt0;
        this.belt1 = belt1;
        this.belt2 = belt2;
        this.configurationIndex = configurationIndex;
        fluidId = fractionator.fluidId;
        productId = fractionator.productId;
        produceProb = fractionator.produceProb;
        isWorking = fractionator.isWorking;
        fluidInputCount = fractionator.fluidInputCount;
        fluidInputCargoCount = fractionator.fluidInputCargoCount;
        fluidInputInc = fractionator.fluidInputInc;
        productOutputCount = fractionator.productOutputCount;
        fluidOutputCount = fractionator.fluidOutputCount;
        fluidOutputInc = fractionator.fluidOutputInc;
        progress = fractionator.progress;
        fractionSuccess = fractionator.fractionSuccess;
        incUsed = fractionator.incUsed;
        fluidOutputTotal = fractionator.fluidOutputTotal;
        productOutputTotal = fractionator.productOutputTotal;
        seed = fractionator.seed;
    }

    public int incLevel
    {
        get
        {
            if (fluidInputCount <= 0 || fluidInputInc <= 0)
            {
                return 0;
            }
            int num = fluidInputInc / fluidInputCount;
            if (num >= 10)
            {
                return 10;
            }
            return num;
        }
    }

    public void SetRecipe(int needId)
    {
        incUsed = false;
        RecipeProto[] fractionatorRecipes = RecipeProto.fractionatorRecipes;
        for (int i = 0; i < fractionatorRecipes.Length; i++)
        {
            if (needId == fractionatorRecipes[i].Items[0])
            {
                fluidId = needId;
                productId = fractionatorRecipes[i].Results[0];
                produceProb = fractionatorRecipes[i].ResultCounts[0] / (float)fractionatorRecipes[i].ItemCounts[0];
                break;
            }
        }
    }

    public uint InternalUpdate(CargoTraffic cargoTraffic,
                               float power,
                               ref readonly FractionatorConfiguration configuration,
                               int[] productRegister,
                               int[] consumeRegister)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        double num = 1.0;
        if (fluidInputCount == 0)
        {
            fluidInputCargoCount = 0f;
        }
        else
        {
            num = fluidInputCargoCount > 0.0001 ? fluidInputCount / fluidInputCargoCount : 4f;
        }
        if (fluidInputCount > 0 && productOutputCount < configuration.ProductOutputMax && fluidOutputCount < configuration.FluidOutputMax)
        {
            int num2 = (int)((double)power * 166.66666666666666 * (double)(fluidInputCargoCount < 30f ? fluidInputCargoCount : 30f) * num + 0.75);
            progress += num2;
            if (progress > 100000)
            {
                progress = 100000;
            }
            while (progress >= 10000)
            {
                int num3 = fluidInputInc > 0 && fluidInputCount > 0 ? fluidInputInc / fluidInputCount : 0;
                if (!incUsed)
                {
                    incUsed = num3 > 0;
                }
                seed = (uint)((int)((ulong)((seed % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                fractionSuccess = seed / 2147483646.0 < produceProb * (1.0 + Cargo.accTableMilli[num3 < 10 ? num3 : 10]);
                if (fractionSuccess)
                {
                    productOutputCount++;
                    productOutputTotal++;
                    lock (productRegister)
                    {
                        productRegister[productId]++;
                    }
                    lock (consumeRegister)
                    {
                        consumeRegister[fluidId]++;
                    }
                }
                else
                {
                    fluidOutputCount++;
                    fluidOutputTotal++;
                    fluidOutputInc += num3;
                }
                fluidInputCount--;
                fluidInputInc -= num3;
                fluidInputCargoCount -= (float)(1.0 / num);
                if (fluidInputCargoCount < 0f)
                {
                    fluidInputCargoCount = 0f;
                }
                progress -= 10000;
            }
        }
        else
        {
            fractionSuccess = false;
        }
        byte stack;
        byte inc;
        if (belt1 != null)
        {
            if (configuration.IsOutput1)
            {
                if (fluidOutputCount > 0)
                {
                    int num4 = fluidOutputInc / fluidOutputCount;
                    if (belt1.TryUpdateItemAtHeadAndFillBlank(fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                    {
                        fluidOutputCount--;
                        fluidOutputInc -= num4;
                        if (fluidOutputCount > 0)
                        {
                            num4 = fluidOutputInc / fluidOutputCount;
                            if (belt1.TryUpdateItemAtHeadAndFillBlank(fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                            {
                                fluidOutputCount--;
                                fluidOutputInc -= num4;
                            }
                        }
                    }
                }
            }
            else if (!configuration.IsOutput1 && fluidInputCargoCount < configuration.FluidInputMax)
            {
                if (fluidId > 0)
                {
                    if (TryPickItemAtRear(cargoTraffic, belt1, fluidId, null, out stack, out inc) > 0)
                    {
                        fluidInputCount += stack;
                        fluidInputInc += inc;
                        fluidInputCargoCount += 1f;
                    }
                }
                else
                {
                    int num5 = TryPickItemAtRear(cargoTraffic, belt1, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                    if (num5 > 0)
                    {
                        fluidInputCount += stack;
                        fluidInputInc += inc;
                        fluidInputCargoCount += 1f;
                        SetRecipe(num5);
                    }
                }
            }
        }
        if (belt2 != null)
        {
            if (configuration.IsOutput2)
            {
                if (fluidOutputCount > 0)
                {
                    int num6 = fluidOutputInc / fluidOutputCount;
                    if (belt2.TryUpdateItemAtHeadAndFillBlank(fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                    {
                        fluidOutputCount--;
                        fluidOutputInc -= num6;
                        if (fluidOutputCount > 0)
                        {
                            num6 = fluidOutputInc / fluidOutputCount;
                            if (belt2.TryUpdateItemAtHeadAndFillBlank(fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                            {
                                fluidOutputCount--;
                                fluidOutputInc -= num6;
                            }
                        }
                    }
                }
            }
            else if (!configuration.IsOutput2 && fluidInputCargoCount < configuration.FluidInputMax)
            {
                if (fluidId > 0)
                {
                    if (TryPickItemAtRear(cargoTraffic, belt2, fluidId, null, out stack, out inc) > 0)
                    {
                        fluidInputCount += stack;
                        fluidInputInc += inc;
                        fluidInputCargoCount += 1f;
                    }
                }
                else
                {
                    int num7 = TryPickItemAtRear(cargoTraffic, belt2, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                    if (num7 > 0)
                    {
                        fluidInputCount += stack;
                        fluidInputInc += inc;
                        fluidInputCargoCount += 1f;
                        SetRecipe(num7);
                    }
                }
            }
        }
        if (belt0 != null && configuration.IsOutput0 && productOutputCount > 0 && belt0.TryInsertItemAtHeadAndFillBlank(productId, 1, 0))
        {
            productOutputCount--;
        }
        if (fluidInputCount == 0 && fluidOutputCount == 0 && productOutputCount == 0)
        {
            fluidId = 0;
        }
        isWorking = fluidInputCount > 0 && productOutputCount < configuration.ProductOutputMax && fluidOutputCount < configuration.FluidOutputMax;
        if (!isWorking)
        {
            return 0u;
        }
        return 1u;
    }

    public void Save(ref FractionatorComponent fractionator, SignData[] signPool)
    {
        fractionator.fluidId = fluidId;
        fractionator.productId = productId;
        fractionator.produceProb = produceProb;
        fractionator.isWorking = isWorking;
        fractionator.fluidInputCount = fluidInputCount;
        fractionator.fluidInputCargoCount = fluidInputCargoCount;
        fractionator.fluidInputInc = fluidInputInc;
        fractionator.productOutputCount = productOutputCount;
        fractionator.fluidOutputCount = fluidOutputCount;
        fractionator.fluidOutputInc = fluidOutputInc;
        fractionator.progress = progress;
        fractionator.fractionSuccess = fractionSuccess;
        fractionator.incUsed = incUsed;
        fractionator.fluidOutputTotal = fluidOutputTotal;
        fractionator.productOutputTotal = productOutputTotal;
        fractionator.seed = seed;

        SaveRecipeSignData(ref fractionator, signPool);
    }

    private static void SaveRecipeSignData(ref FractionatorComponent fractionator, SignData[] signPool)
    {
        RecipeProto[] fractionatorRecipes = RecipeProto.fractionatorRecipes;
        for (int i = 0; i < fractionatorRecipes.Length; i++)
        {
            if (fractionator.fluidId == fractionatorRecipes[i].Items[0])
            {
                signPool[fractionator.entityId].iconId0 = (uint)fractionatorRecipes[i].Results[0];
                signPool[fractionator.entityId].iconType = fractionatorRecipes[i].Results[0] != 0 ? 1u : 0u;
                break;
            }
        }
    }

    private static int TryPickItemAtRear(CargoTraffic cargoTraffic, CargoPath cargoPath, int filter, int[] needs, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
        int cargoIdAtRear = cargoPath.GetCargoIdAtRear();
        if (cargoIdAtRear == -1)
        {
            return 0;
        }
        int item = cargoTraffic.container.cargoPool[cargoIdAtRear].item;
        stack = cargoTraffic.container.cargoPool[cargoIdAtRear].stack;
        inc = cargoTraffic.container.cargoPool[cargoIdAtRear].inc;
        if (filter != 0)
        {
            if (item == filter)
            {
                cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                return item;
            }
        }
        else
        {
            if (needs == null)
            {
                cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                return item;
            }
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i] == item)
                {
                    cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                    return item;
                }
            }
        }
        stack = 1;
        inc = 0;
        return 0;
    }
}