using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct OptimizedFuelGenerator
{
    public int id;
    private readonly long genEnergyPerTick;
    private readonly long useFuelPerTick;
    public readonly short fuelMask;
    private readonly bool boost;
    private long fuelEnergy;
    private short curFuelId;
    public OptimizedItemId fuelId;
    public short fuelCount;
    public short fuelInc;
    private bool productive;
    private bool incUsed;
    private byte fuelIncLevel;
    private long fuelHeat;
    private float currentStrength;
    private long capacityCurrentTick;

    public OptimizedFuelGenerator(OptimizedItemId fuelId, ref readonly PowerGeneratorComponent powerGenerator)
    {
        id = powerGenerator.id;
        genEnergyPerTick = powerGenerator.genEnergyPerTick;
        useFuelPerTick = powerGenerator.useFuelPerTick;
        fuelMask = powerGenerator.fuelMask;
        boost = powerGenerator.boost;
        fuelEnergy = powerGenerator.fuelEnergy;
        curFuelId = powerGenerator.curFuelId;
        this.fuelId = fuelId;
        fuelCount = powerGenerator.fuelCount;
        fuelInc = powerGenerator.fuelInc;
        productive = powerGenerator.productive;
        incUsed = powerGenerator.incUsed;
        fuelIncLevel = powerGenerator.fuelIncLevel;
        fuelHeat = powerGenerator.fuelHeat;
        currentStrength = powerGenerator.currentStrength;
        capacityCurrentTick = powerGenerator.capacityCurrentTick;
    }

    public long EnergyCap_Fuel()
    {
        long num = fuelCount > 0 || fuelEnergy >= useFuelPerTick ? genEnergyPerTick : fuelEnergy * genEnergyPerTick / useFuelPerTick;
        capacityCurrentTick = productive ? (long)(num * (1.0 + Cargo.incTableMilli[fuelIncLevel]) + 0.1) : (long)(num * (1.0 + Cargo.accTableMilli[fuelIncLevel]) + 0.1);
        if (fuelMask == 4)
        {
            if (boost)
            {
                capacityCurrentTick *= 100L;
            }
            if (curFuelId == 1804) // Strange annihilation fuel rod
            {
                capacityCurrentTick *= 2L;
            }
        }
        return capacityCurrentTick;
    }

    public void GeneratePower(ref long num44, double num51, int[] consumeRegister)
    {
        currentStrength = num44 > 0 && capacityCurrentTick > 0 ? 1 : 0;
        if (num44 > 0)
        {
            long num57 = (long)(num51 * capacityCurrentTick + 0.99999);
            long num56 = num44 < num57 ? num44 : num57;
            if (num56 > 0)
            {
                num44 -= num56;
                GenEnergyByFuel(num56, consumeRegister);
            }
        }
    }

    public void GenEnergyByFuel(long energy, int[] consumeRegister)
    {
        long num = productive ? energy * useFuelPerTick * 40 / (genEnergyPerTick * Cargo.incFastDivisionNumerator[fuelIncLevel]) : energy * useFuelPerTick / genEnergyPerTick;
        num = energy > 0 && num == 0L ? 1 : num;
        if (fuelEnergy >= num)
        {
            fuelEnergy -= num;
            return;
        }
        curFuelId = 0;
        if (fuelCount > 0)
        {
            int num2 = fuelInc / fuelCount;
            num2 = num2 > 0 ? num2 > 10 ? 10 : num2 : 0;
            fuelInc -= (short)num2;
            productive = LDB.items.Select(fuelId.ItemIndex).Productive;
            if (productive)
            {
                fuelIncLevel = (byte)num2;
                num = energy * useFuelPerTick * 40 / (genEnergyPerTick * Cargo.incFastDivisionNumerator[fuelIncLevel]);
            }
            else
            {
                fuelIncLevel = (byte)num2;
                num = energy * useFuelPerTick / genEnergyPerTick;
            }
            if (!incUsed)
            {
                incUsed = fuelIncLevel > 0;
            }
            long num3 = num - fuelEnergy;
            fuelEnergy = fuelHeat - num3;
            curFuelId = fuelId.ItemIndex;
            fuelCount--;
            consumeRegister[fuelId.OptimizedItemIndex]++;
            if (fuelCount == 0)
            {
                fuelId = default;
                fuelInc = 0;
                fuelHeat = 0L;
            }
            if (fuelEnergy < 0)
            {
                fuelEnergy = 0L;
            }
        }
        else
        {
            fuelEnergy = 0L;
            productive = false;
        }
    }

    public void SetNewFuel(OptimizedItemId _itemId, short _count, short _inc)
    {
        fuelId = _itemId;
        fuelCount = _count;
        fuelInc = _inc;
        fuelHeat = LDB.items.Select(_itemId.ItemIndex)?.HeatValue ?? 0;
        incUsed = false;
    }

    public OptimizedItemId PickFuelFrom(int filter, out int inc)
    {
        inc = 0;
        if (fuelId.ItemIndex > 0 && fuelCount > 5 && (filter == 0 || filter == fuelId.ItemIndex))
        {
            if (fuelInc > 0)
            {
                inc = fuelInc / fuelCount;
            }
            fuelInc -= (short)inc;
            fuelCount--;
            return fuelId;
        }
        return default;
    }

    public readonly void Save(ref PowerGeneratorComponent powerGenerator)
    {
        powerGenerator.genEnergyPerTick = genEnergyPerTick;
        powerGenerator.useFuelPerTick = useFuelPerTick;
        powerGenerator.fuelMask = fuelMask;
        powerGenerator.boost = boost;
        powerGenerator.fuelEnergy = fuelEnergy;
        powerGenerator.curFuelId = curFuelId;
        powerGenerator.fuelId = fuelId.ItemIndex;
        powerGenerator.fuelCount = fuelCount;
        powerGenerator.fuelInc = fuelInc;
        powerGenerator.productive = productive;
        powerGenerator.incUsed = incUsed;
        powerGenerator.fuelIncLevel = fuelIncLevel;
        powerGenerator.fuelHeat = fuelHeat;
        powerGenerator.currentStrength = currentStrength;
        powerGenerator.capacityCurrentTick = capacityCurrentTick;
    }
}
