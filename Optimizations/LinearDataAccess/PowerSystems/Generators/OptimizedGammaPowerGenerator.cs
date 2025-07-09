using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedGammaPowerGenerator
{
    private readonly OptimizedCargoPath? slot0Belt;
    private readonly int slot0BeltOffset;
    private readonly bool slot0IsOutput;
    private readonly OptimizedCargoPath? slot1Belt;
    private readonly int slot1BeltOffset;
    private readonly bool slot1IsOutput;
    private readonly OptimizedItemId catalystId;
    private readonly OptimizedItemId productId;
    private readonly long productHeat;
    private readonly UnityEngine.Vector3 position;
    private readonly float ionEnhance;
    private readonly long genEnergyPerTick;
    private float currentStrength;
    private int catalystPoint;
    private bool incUsed;
    private long fuelHeat;
    private int catalystIncPoint;
    private float productCount;
    private float warmup;
    private float warmupSpeed;
    private long capacityCurrentTick;

    public readonly int catalystIncLevel
    {
        get
        {
            int num = catalystPoint != 0 ? catalystIncPoint / catalystPoint : 0;
            if (num >= 10)
            {
                return 10;
            }
            return num;
        }
    }

    public OptimizedGammaPowerGenerator(OptimizedCargoPath? slot0Belt,
                                        int slot0BeltOffset,
                                        bool slot0IsOutput,
                                        OptimizedCargoPath? slot1Belt,
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
        this.catalystId = catalystId;
        this.productId = productId;
        productHeat = powerGenerator.productHeat;
        catalystPoint = powerGenerator.catalystPoint;
        incUsed = powerGenerator.incUsed;
        fuelHeat = powerGenerator.fuelHeat;
        catalystIncPoint = powerGenerator.catalystIncPoint;
        productCount = powerGenerator.productCount;
        warmup = powerGenerator.warmup;
        warmupSpeed = powerGenerator.warmupSpeed;
        capacityCurrentTick = powerGenerator.capacityCurrentTick;
    }

    public long EnergyCap_Gamma_Req(UnityEngine.Vector3 normalizedSunDirection, float increase, float eta)
    {
        float num = (UnityEngine.Vector3.Dot(normalizedSunDirection, position) + increase * 0.8f + (catalystPoint > 0 ? ionEnhance : 0f)) * 6f + 0.5f;
        num = currentStrength = num > 1f ? 1f : num < 0f ? 0f : num;
        float num2 = (float)Cargo.accTableMilli[catalystIncLevel];
        capacityCurrentTick = (long)(currentStrength * (1f + warmup * 1.5f) * (catalystPoint > 0 ? 2f * (1f + num2) : 1f) * (productId.ItemIndex > 0 ? 8f : 1f) * genEnergyPerTick);
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

    public void GameTick_Gamma(bool useIon, bool useCata, bool keyFrame, int[] productRegister, int[] consumeRegister)
    {
        if (catalystPoint > 0)
        {
            int num = catalystPoint / 3600;
            if (useCata)
            {
                int num2 = catalystIncPoint / catalystPoint;
                catalystPoint--;
                catalystIncPoint -= num2;
                if (!incUsed)
                {
                    incUsed = num2 > 0;
                }
                if (catalystIncPoint < 0 || catalystPoint <= 0)
                {
                    catalystIncPoint = 0;
                }
            }
            int num3 = catalystPoint / 3600;
            consumeRegister[catalystId.OptimizedItemIndex] += num - num3;
        }
        if (productId.ItemIndex > 0 && productCount < 20f)
        {
            int num4 = (int)productCount;
            productCount += (float)(capacityCurrentTick / (double)productHeat);
            int num5 = (int)productCount;
            productRegister[productId.OptimizedItemIndex] += num5 - num4;
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
        bool flag2 = keyFrame && useIon && catalystPoint < 72000f;
        if (!(flag || flag2))
        {
            return;
        }
        bool flag3;
        bool flag4;
        if (slot0Belt == null)
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
        if (slot1Belt == null)
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
                    if (InsertInto(slot0Belt!, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                    {
                        productCount -= 1f;
                        fuelHeat = 1L;
                    }
                    else if (InsertInto(slot1Belt!, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                    {
                        productCount -= 1f;
                        fuelHeat = 0L;
                    }
                }
                else if (InsertInto(slot1Belt!, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 0L;
                }
                else if (InsertInto(slot0Belt!, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 1L;
                }
            }
            else if (flag3)
            {
                if (InsertInto(slot0Belt!, slot0BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
                {
                    productCount -= 1f;
                    fuelHeat = 1L;
                }
            }
            else if (flag5 && InsertInto(slot1Belt!, slot1BeltOffset, productId.ItemIndex, 1, 0, out _) == 1)
            {
                productCount -= 1f;
                fuelHeat = 0L;
            }
        }
        if (flag2)
        {
            if (flag4)
            {
                OptimizedCargo optimizedCargo = PickFrom(slot0Belt!, slot0BeltOffset, catalystId.ItemIndex, null);
                if (optimizedCargo.Item == catalystId.ItemIndex)
                {
                    catalystPoint += 3600 * optimizedCargo.Stack;
                    catalystIncPoint += 3600 * optimizedCargo.Inc;
                }
            }

            if (flag6)
            {
                OptimizedCargo optimizedCargo = PickFrom(slot1Belt!, slot1BeltOffset, catalystId.ItemIndex, null);
                if (optimizedCargo.Item == catalystId.ItemIndex)
                {
                    catalystPoint += 3600 * optimizedCargo.Stack;
                    catalystIncPoint += 3600 * optimizedCargo.Inc;
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
        powerGenerator.catalystIncPoint = catalystIncPoint;
        powerGenerator.productCount = productCount;
        powerGenerator.warmup = warmup;
        powerGenerator.warmupSpeed = warmupSpeed;
        powerGenerator.capacityCurrentTick = capacityCurrentTick;
    }

    private static int InsertInto(OptimizedCargoPath belt, int offset, int itemId, byte itemCount, byte itemInc, out byte remainInc)
    {
        remainInc = itemInc;
        if (belt.TryInsertItem(offset, itemId, itemCount, itemInc))
        {
            remainInc = 0;
            return itemCount;
        }
        return 0;
    }

    private static OptimizedCargo PickFrom(OptimizedCargoPath belt, int offset, int filter, int[]? needs)
    {
        if (needs == null)
        {
            if (filter != 0)
            {
                return belt.TryPickItem(offset - 2, 5, filter);
            }
            return belt.TryPickItem(offset - 2, 5);
        }

        return belt.TryPickItem(offset - 2, 5, filter, needs);
    }
}
