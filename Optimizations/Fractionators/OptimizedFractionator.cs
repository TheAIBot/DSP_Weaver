﻿using System.Runtime.InteropServices;
using UnityEngine;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Fractionators;

internal record struct FractionatorRecipeProduct(int GameFluidId, OptimizedItemId Fluid, OptimizedItemId Product, float ProduceProbability);

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct OptimizedFractionator
{
    public readonly int belt0Index;
    public readonly int belt1Index;
    public readonly int belt2Index;
    public readonly int configurationIndex;
    public OptimizedItemId fluidId;
    public OptimizedItemId productId;
    public float produceProb;
    public int productOutputCount;
    public int fluidOutputCount;
    public int fluidOutputInc;
    public int progress;
    public bool fractionSuccess;
    public bool incUsed;
    public int fluidOutputTotal;
    public int productOutputTotal;
    public uint seed;

    public OptimizedFractionator(int belt0Index,
                                 int belt1Index,
                                 int belt2Index,
                                 int configurationIndex,
                                 OptimizedItemId fluidId,
                                 OptimizedItemId productId,
                                 ref readonly FractionatorComponent fractionator)
    {
        this.belt0Index = belt0Index;
        this.belt1Index = belt1Index;
        this.belt2Index = belt2Index;
        this.configurationIndex = configurationIndex;
        this.fluidId = fluidId;
        this.productId = productId;
        produceProb = fractionator.produceProb;
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

    public void SetRecipe(int needId, FractionatorRecipeProduct[] fractionatorRecipeProducts)
    {
        incUsed = false;
        for (int i = 0; i < fractionatorRecipeProducts.Length; i++)
        {
            if (needId == fractionatorRecipeProducts[i].GameFluidId)
            {
                fluidId = fractionatorRecipeProducts[i].Fluid;
                productId = fractionatorRecipeProducts[i].Product;
                produceProb = fractionatorRecipeProducts[i].ProduceProbability;
                break;
            }
        }
    }

    public uint InternalUpdate(float power,
                               ref readonly FractionatorConfiguration configuration,
                               ref FractionatorPowerFields fractionatorPowerFields,
                               FractionatorRecipeProduct[] fractionatorRecipeProducts,
                               int[] productRegister,
                               int[] consumeRegister,
                               OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        double num = 1.0;
        if (fractionatorPowerFields.fluidInputCount == 0)
        {
            fractionatorPowerFields.fluidInputCargoCount = 0f;
        }
        else
        {
            num = fractionatorPowerFields.fluidInputCargoCount > 0.0001 ? fractionatorPowerFields.fluidInputCount / fractionatorPowerFields.fluidInputCargoCount : 4f;
        }
        if (fractionatorPowerFields.fluidInputCount > 0 && productOutputCount < configuration.ProductOutputMax && fluidOutputCount < configuration.FluidOutputMax)
        {
            int num2 = (int)((double)power * 166.66666666666666 * (double)(fractionatorPowerFields.fluidInputCargoCount < 30f ? fractionatorPowerFields.fluidInputCargoCount : 30f) * num + 0.75);
            progress += num2;
            if (progress > 100000)
            {
                progress = 100000;
            }
            while (progress >= 10000)
            {
                int num3 = fractionatorPowerFields.fluidInputInc > 0 && fractionatorPowerFields.fluidInputCount > 0 ? fractionatorPowerFields.fluidInputInc / fractionatorPowerFields.fluidInputCount : 0;
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
                    productRegister[productId.OptimizedItemIndex]++;
                    consumeRegister[fluidId.OptimizedItemIndex]++;
                }
                else
                {
                    fluidOutputCount++;
                    fluidOutputTotal++;
                    fluidOutputInc += num3;
                }
                fractionatorPowerFields.fluidInputCount--;
                fractionatorPowerFields.fluidInputInc -= num3;
                fractionatorPowerFields.fluidInputCargoCount -= (float)(1.0 / num);
                if (fractionatorPowerFields.fluidInputCargoCount < 0f)
                {
                    fractionatorPowerFields.fluidInputCargoCount = 0f;
                }
                progress -= 10000;
            }
        }
        else
        {
            fractionSuccess = false;
        }
        if (belt1Index != OptimizedCargoPath.NO_BELT_INDEX)
        {
            ref OptimizedCargoPath belt1 = ref optimizedCargoPaths[belt1Index];
            if (configuration.IsOutput1)
            {
                if (fluidOutputCount > 0)
                {
                    int num4 = fluidOutputInc / fluidOutputCount;
                    if (belt1.TryUpdateItemAtHeadAndFillBlank(fluidId.ItemIndex, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                    {
                        fluidOutputCount--;
                        fluidOutputInc -= num4;
                        if (fluidOutputCount > 0)
                        {
                            num4 = fluidOutputInc / fluidOutputCount;
                            if (belt1.TryUpdateItemAtHeadAndFillBlank(fluidId.ItemIndex, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                            {
                                fluidOutputCount--;
                                fluidOutputInc -= num4;
                            }
                        }
                    }
                }
            }
            else if (!configuration.IsOutput1 && fractionatorPowerFields.fluidInputCargoCount < configuration.FluidInputMax)
            {
                if (fluidId.ItemIndex > 0)
                {
                    if (CargoPathMethods.TryPickItemAtRear(ref belt1, fluidId.ItemIndex, null, out OptimizedCargo optimizedCargo))
                    {
                        fractionatorPowerFields.fluidInputCount += optimizedCargo.Stack;
                        fractionatorPowerFields.fluidInputInc += optimizedCargo.Inc;
                        fractionatorPowerFields.fluidInputCargoCount += 1f;
                    }
                }
                else
                {
                    if (CargoPathMethods.TryPickItemAtRear(ref belt1, 0, RecipeProto.fractionatorNeeds, out OptimizedCargo optimizedCargo))
                    {
                        fractionatorPowerFields.fluidInputCount += optimizedCargo.Stack;
                        fractionatorPowerFields.fluidInputInc += optimizedCargo.Inc;
                        fractionatorPowerFields.fluidInputCargoCount += 1f;
                        SetRecipe(optimizedCargo.Item, fractionatorRecipeProducts);
                    }
                }
            }
        }
        if (belt2Index != OptimizedCargoPath.NO_BELT_INDEX)
        {
            ref OptimizedCargoPath belt2 = ref optimizedCargoPaths[belt2Index];
            if (configuration.IsOutput2)
            {
                if (fluidOutputCount > 0)
                {
                    int num6 = fluidOutputInc / fluidOutputCount;
                    if (belt2.TryUpdateItemAtHeadAndFillBlank(fluidId.ItemIndex, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                    {
                        fluidOutputCount--;
                        fluidOutputInc -= num6;
                        if (fluidOutputCount > 0)
                        {
                            num6 = fluidOutputInc / fluidOutputCount;
                            if (belt2.TryUpdateItemAtHeadAndFillBlank(fluidId.ItemIndex, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                            {
                                fluidOutputCount--;
                                fluidOutputInc -= num6;
                            }
                        }
                    }
                }
            }
            else if (!configuration.IsOutput2 && fractionatorPowerFields.fluidInputCargoCount < configuration.FluidInputMax)
            {
                if (fluidId.ItemIndex > 0)
                {
                    if (CargoPathMethods.TryPickItemAtRear(ref belt2, fluidId.ItemIndex, null, out OptimizedCargo optimizedCargo))
                    {
                        fractionatorPowerFields.fluidInputCount += optimizedCargo.Stack;
                        fractionatorPowerFields.fluidInputInc += optimizedCargo.Inc;
                        fractionatorPowerFields.fluidInputCargoCount += 1f;
                    }
                }
                else
                {
                    if (CargoPathMethods.TryPickItemAtRear(ref belt2, 0, RecipeProto.fractionatorNeeds, out OptimizedCargo optimizedCargo))
                    {
                        fractionatorPowerFields.fluidInputCount += optimizedCargo.Stack;
                        fractionatorPowerFields.fluidInputInc += optimizedCargo.Inc;
                        fractionatorPowerFields.fluidInputCargoCount += 1f;
                        SetRecipe(optimizedCargo.Item, fractionatorRecipeProducts);
                    }
                }
            }
        }
        if (belt0Index != OptimizedCargoPath.NO_BELT_INDEX)
        {
            ref OptimizedCargoPath belt0 = ref optimizedCargoPaths[belt0Index];
            if (configuration.IsOutput0 && productOutputCount > 0 && belt0.TryInsertItemAtHeadAndFillBlank(productId.ItemIndex, 1, 0))
            {
                productOutputCount--;
            }
        }
        if (fractionatorPowerFields.fluidInputCount == 0 && fluidOutputCount == 0 && productOutputCount == 0)
        {
            fluidId = default;
        }
        fractionatorPowerFields.isWorking = fractionatorPowerFields.fluidInputCount > 0 && productOutputCount < configuration.ProductOutputMax && fluidOutputCount < configuration.FluidOutputMax;
        if (!fractionatorPowerFields.isWorking)
        {
            return 0u;
        }
        return 1u;
    }

    public readonly void Save(ref FractionatorComponent fractionator,
                              ref readonly FractionatorPowerFields fractionatorPowerFields,
                              SignData[] signPool)
    {
        fractionator.fluidId = fluidId.ItemIndex;
        fractionator.productId = productId.ItemIndex;
        fractionator.produceProb = produceProb;
        fractionator.isWorking = fractionatorPowerFields.isWorking;
        fractionator.fluidInputCount = fractionatorPowerFields.fluidInputCount;
        fractionator.fluidInputCargoCount = fractionatorPowerFields.fluidInputCargoCount;
        fractionator.fluidInputInc = fractionatorPowerFields.fluidInputInc;
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
}