using System.Collections.Generic;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.PowerSystems.Generators;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedGammaPowerGenerator
{
    private readonly OptimizedIndexedCargoPath slot0Belt;
    private readonly int slot0BeltOffset;
    private readonly bool slot0IsOutput;
    private readonly OptimizedIndexedCargoPath slot1Belt;
    private readonly int slot1BeltOffset;
    private readonly bool slot1IsOutput;
    private readonly OptimizedItemId productId;
    private readonly long productHeat;
    private readonly UnityEngine.Vector3 position;
    private readonly float ionEnhance;
    private readonly long genEnergyPerTick;
    private float currentStrength;
    private OptimizedItemId catalystId;
    public readonly short catalystMask;
    public byte catalystIncLevel;
    public int curCatalystId;
    public short catalystCount;
    public short catalystInc;
    private int catalystPoint;
    private bool incUsed;
    private long fuelHeat;
    private float productCount;
    private float warmup;
    private float warmupSpeed;
    private long capacityCurrentTick;

    public OptimizedGammaPowerGenerator(OptimizedIndexedCargoPath slot0Belt,
                                        int slot0BeltOffset,
                                        bool slot0IsOutput,
                                        OptimizedIndexedCargoPath slot1Belt,
                                        int slot1BeltOffset,
                                        bool slot1IsOutput,
                                        OptimizedItemId catalystId,
                                        OptimizedItemId productId,
                                        ref readonly PowerGeneratorComponent powerGenerator)
    {
        this.slot0Belt = slot0Belt;
        this.slot0BeltOffset = slot0BeltOffset;
        this.slot0IsOutput = slot0IsOutput;
        this.slot1Belt = slot1Belt;
        this.slot1BeltOffset = slot1BeltOffset;
        this.slot1IsOutput = slot1IsOutput;
        position = new UnityEngine.Vector3(powerGenerator.x, powerGenerator.y, powerGenerator.z);
        ionEnhance = powerGenerator.ionEnhance;
        genEnergyPerTick = powerGenerator.genEnergyPerTick;
        currentStrength = powerGenerator.currentStrength;
        catalystMask = powerGenerator.catalystMask;
        catalystIncLevel = powerGenerator.catalystIncLevel;
        curCatalystId = powerGenerator.curCatalystId;
        catalystCount = powerGenerator.catalystCount;
        catalystInc = powerGenerator.catalystInc;
        this.catalystId = catalystId;
        this.productId = productId;
        productHeat = powerGenerator.productHeat;
        catalystPoint = powerGenerator.catalystPoint;
        incUsed = powerGenerator.incUsed;
        fuelHeat = powerGenerator.fuelHeat;
        productCount = powerGenerator.productCount;
        warmup = powerGenerator.warmup;
        warmupSpeed = powerGenerator.warmupSpeed;
        capacityCurrentTick = powerGenerator.capacityCurrentTick;
    }

    public long EnergyCap_Gamma_Req(UnityEngine.Vector3 normalizedSunDirection, float increase, float eta)
    {
        float num = (UnityEngine.Vector3.Dot(normalizedSunDirection, position) + increase * 0.8f + ((catalystPoint > 0 || catalystCount > 0) ? ionEnhance : 0f)) * 6f + 0.5f;
        num = currentStrength = num > 1f ? 1f : num < 0f ? 0f : num;
        float num2 = (float)Cargo.accTableMilli[catalystIncLevel];
        float num3 = ItemProto.catalystAbilityById[curCatalystId];
        capacityCurrentTick = (long)(currentStrength * (1f + warmup * 1.5f) * ((catalystPoint > 0 || catalystCount > 0) ? (num3 * (1f + num2)) : 1f) * ((productId.ItemIndex > 0) ? 8f : 1f) * genEnergyPerTick);
        eta = 1f - (1f - eta) * (1f - warmup * warmup * 0.4f);
        warmupSpeed = (num - 0.75f) * 4f * 1.3888889E-05f;
        return (long)(capacityCurrentTick / (double)eta + 0.49999999);
    }

    public long EnergyCap_Gamma(float response)
    {
        if (warmupSpeed > 0f && response < 0.25f)
        {
            warmupSpeed *= response * 4f;
        }
        capacityCurrentTick = (long)(capacityCurrentTick * (double)response);
        if (productId.ItemIndex == 0)
        {
            return capacityCurrentTick;
        }
        return 0L;
    }

    public void GameTick_Gamma(bool useIon, bool useCata, bool keyFrame, int[] productRegister, int[] consumeRegister, Dictionary<int, OptimizedItemId> catalystItemIdToOptimizedCatalystItemId)
    {
        if (useCata)
        {
            if (catalystPoint > 0)
            {
                catalystPoint--;
            }
            else if (catalystCount > 0)
            {
                int num = catalystInc / catalystCount;
                num = ((num > 0) ? ((num > 10) ? 10 : num) : 0);
                catalystInc -= (short)num;
                catalystIncLevel = (byte)num;
                curCatalystId = catalystId.ItemIndex;
                catalystPoint = 3600;
                catalystPoint--;
                catalystCount--;
                consumeRegister[catalystId.OptimizedItemIndex]++;
                if (!incUsed)
                {
                    incUsed = catalystIncLevel > 0;
                }
                if (catalystCount == 0)
                {
                    catalystId = default;
                    catalystInc = 0;
                }
            }
            else
            {
                curCatalystId = 0;
                catalystIncLevel = 0;
            }
        }
        if (productId.ItemIndex > 0 && productCount < 20f)
        {
            int num2 = (int)productCount;
            productCount += (float)(capacityCurrentTick / (double)productHeat);
            int num3 = (int)productCount;
            productRegister[productId.OptimizedItemIndex] += num3 - num2;
            if (productCount > 20f)
            {
                productCount = 20f;
            }
        }
        warmup += warmupSpeed;
        warmup = warmup > 1f ? 1f : warmup < 0f ? 0f : warmup;
        if (!keyFrame && !(productCount < 20f))
        {
            return;
        }
        bool flag = productId.ItemIndex > 0 && productCount >= 1f;
        bool flag2 = keyFrame && useIon && catalystCount < 10;
        if (!(flag || flag2))
        {
            return;
        }
        bool flag3;
        bool flag4;
        if (!slot0Belt.HasBelt)
        {
            flag3 = false;
            flag4 = false;
        }
        else
        {
            flag3 = slot0IsOutput;
            flag4 = !slot0IsOutput;
        }
        bool flag5;
        bool flag6;
        if (!slot1Belt.HasBelt)
        {
            flag5 = false;
            flag6 = false;
        }
        else
        {
            flag5 = slot1IsOutput;
            flag6 = !slot1IsOutput;
        }
        if (flag)
        {
            if (flag3 && flag5)
            {
                if (fuelHeat == 0L)
                {
                    if (InsertInto(ref slot0Belt.Belt, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                    {
                        productCount -= 1f;
                        fuelHeat = 1L;
                    }
                    else if (InsertInto(ref slot1Belt.Belt, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                    {
                        productCount -= 1f;
                        fuelHeat = 0L;
                    }
                }
                else if (InsertInto(ref slot1Belt.Belt, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 0L;
                }
                else if (InsertInto(ref slot0Belt.Belt, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 1L;
                }
            }
            else if (flag3)
            {
                if (InsertInto(ref slot0Belt.Belt, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 1L;
                }
            }
            else if (flag5 && InsertInto(ref slot1Belt.Belt, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
            {
                productCount -= 1f;
                fuelHeat = 0L;
            }
        }
        if (!flag2)
        {
            return;
        }
        if (flag4)
        {
            if (catalystCount > 0)
            {
                OptimizedCargo optimizedCargo = PickFrom(ref slot0Belt.Belt, slot0BeltOffset, catalystId.ItemIndex, null);
                if (optimizedCargo.Item == catalystId.ItemIndex)
                {
                    catalystCount += optimizedCargo.Stack;
                    catalystInc += optimizedCargo.Inc;
                }
            }
            else
            {
                int[] array = ItemProto.catalystNeeds[catalystMask];
                if (array != null && array.Length != 0)
                {
                    OptimizedCargo optimizedCargo = PickFrom(ref slot0Belt.Belt, slot0BeltOffset, 0, array);
                    if (optimizedCargo.Item > 0)
                    {
                        catalystId = catalystItemIdToOptimizedCatalystItemId[optimizedCargo.Item];
                        catalystCount += optimizedCargo.Stack;
                        catalystInc += optimizedCargo.Inc;
                    }
                }
            }
        }

        if (!flag6)
        {
            return;
        }
        if (catalystCount > 0)
        {
            OptimizedCargo optimizedCargo = PickFrom(ref slot1Belt.Belt, slot1BeltOffset, catalystId.ItemIndex, null);
            if (optimizedCargo.Item == catalystId.ItemIndex)
            {
                catalystCount += optimizedCargo.Stack;
                catalystInc += optimizedCargo.Inc;
            }
        }
        else
        {
            int[] array = ItemProto.catalystNeeds[catalystMask];
            if (array != null && array.Length != 0)
            {
                OptimizedCargo optimizedCargo = PickFrom(ref slot1Belt.Belt, slot1BeltOffset, 0, array);
                if (optimizedCargo.Item > 0)
                {
                    catalystId = catalystItemIdToOptimizedCatalystItemId[optimizedCargo.Item];
                    catalystCount += optimizedCargo.Stack;
                    catalystInc += optimizedCargo.Inc;
                }
            }
        }
    }

    public readonly void Save(ref PowerGeneratorComponent powerGenerator)
    {
        powerGenerator.currentStrength = currentStrength;
        powerGenerator.catalystPoint = catalystPoint;
        powerGenerator.incUsed = incUsed;
        powerGenerator.fuelHeat = fuelHeat;
        powerGenerator.catalystId = catalystId.ItemIndex;
        powerGenerator.catalystIncLevel = catalystIncLevel;
        powerGenerator.curCatalystId = curCatalystId;
        powerGenerator.catalystCount = catalystCount;
        powerGenerator.catalystInc = catalystInc;
        powerGenerator.productCount = productCount;
        powerGenerator.warmup = warmup;
        powerGenerator.warmupSpeed = warmupSpeed;
        powerGenerator.capacityCurrentTick = capacityCurrentTick;
    }

    private static int InsertInto(ref OptimizedCargoPath belt, int offset, int itemId, byte itemCount, byte itemInc, out byte remainInc)
    {
        remainInc = itemInc;
        if (belt.TryInsertItem(offset, itemId, itemCount, itemInc))
        {
            remainInc = 0;
            return itemCount;
        }
        return 0;
    }

    private static OptimizedCargo PickFrom(ref OptimizedCargoPath belt, int offset, int filter, int[]? needs)
    {
        if (needs == null)
        {
            if (filter != 0)
            {
                belt.TryPickItem(offset - 2, 5, filter, out OptimizedCargo cargo);
                return cargo;
            }
            return belt.TryPickItem(offset - 2, 5);
        }

        return belt.TryPickItem(offset - 2, 5, filter, needs);
    }
}
