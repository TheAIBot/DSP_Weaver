using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedPowerExchanger
{
    private readonly float targetState;
    private readonly long energyPerTick;
    private readonly long maxPoolEnergy;
    private readonly OptimizedItemId emptyId;
    private readonly OptimizedItemId fullId;
    private readonly OptimizedCargoPath? belt0;
    private readonly OptimizedCargoPath? belt1;
    private readonly OptimizedCargoPath? belt2;
    private readonly OptimizedCargoPath? belt3;
    private readonly bool isOutput0;
    private readonly bool isOutput1;
    private readonly bool isOutput2;
    private readonly bool isOutput3;
    private short emptyCount;
    private short fullCount;
    private short emptyInc;
    private short fullInc;
    private long currPoolEnergy;
    private byte poolInc;
    private int outputSlot;
    private int inputSlot;
    private int outputRectify;
    private int inputRectify;
    public float state;
    public long currEnergyPerTick;
    public long capsCurrentTick;

    public OptimizedPowerExchanger(OptimizedItemId emptyId,
                                   OptimizedItemId fullId,
                                   OptimizedCargoPath? belt0,
                                   OptimizedCargoPath? belt1,
                                   OptimizedCargoPath? belt2,
                                   OptimizedCargoPath? belt3,
                                   ref readonly PowerExchangerComponent powerExchanger)
    {
        targetState = powerExchanger.targetState;
        energyPerTick = powerExchanger.energyPerTick;
        maxPoolEnergy = powerExchanger.maxPoolEnergy;
        this.emptyId = emptyId;
        this.fullId = fullId;
        this.belt0 = belt0;
        this.belt1 = belt1;
        this.belt2 = belt2;
        this.belt3 = belt3;
        isOutput0 = powerExchanger.isOutput0;
        isOutput1 = powerExchanger.isOutput1;
        isOutput2 = powerExchanger.isOutput2;
        isOutput3 = powerExchanger.isOutput3;
        emptyCount = powerExchanger.emptyCount;
        fullCount = powerExchanger.fullCount;
        emptyInc = powerExchanger.emptyInc;
        fullInc = powerExchanger.fullInc;
        currPoolEnergy = powerExchanger.currPoolEnergy;
        poolInc = powerExchanger.poolInc;
        outputSlot = powerExchanger.outputSlot;
        inputSlot = powerExchanger.inputSlot;
        outputRectify = powerExchanger.outputRectify;
        inputRectify = powerExchanger.inputRectify;
        state = powerExchanger.state;
        currEnergyPerTick = powerExchanger.currEnergyPerTick;
        capsCurrentTick = powerExchanger.capsCurrentTick;
    }

    public readonly void Save(ref PowerExchangerComponent powerExchanger)
    {
        powerExchanger.emptyCount = emptyCount;
        powerExchanger.fullCount = fullCount;
        powerExchanger.emptyInc = emptyInc;
        powerExchanger.fullInc = fullInc;
        powerExchanger.currPoolEnergy = currPoolEnergy;
        powerExchanger.poolInc = poolInc;
        powerExchanger.outputSlot = outputSlot;
        powerExchanger.inputSlot = inputSlot;
        powerExchanger.outputRectify = outputRectify;
        powerExchanger.inputRectify = inputRectify;
        powerExchanger.state = state;
        powerExchanger.currEnergyPerTick = currEnergyPerTick;
        powerExchanger.capsCurrentTick = capsCurrentTick;
    }

    public void StateUpdate()
    {
        if (state < targetState)
        {
            state += 0.00557f;
            if (state >= targetState)
            {
                state = targetState;
            }
        }
        else if (state > targetState)
        {
            state -= 0.00557f;
            if (state <= targetState)
            {
                state = targetState;
            }
        }
    }

    public long InputCaps()
    {
        long num = CalculateActualEnergyPerTick(isOutput: false);
        if (num > maxPoolEnergy - currPoolEnergy)
        {
            if (emptyCount > 0 && fullCount < 20)
            {
                capsCurrentTick = num;
            }
            else
            {
                capsCurrentTick = maxPoolEnergy - currPoolEnergy;
            }
        }
        else
        {
            capsCurrentTick = num;
        }
        return capsCurrentTick;
    }

    public long OutputCaps()
    {
        long num = CalculateActualEnergyPerTick(isOutput: true);
        if (currPoolEnergy < num)
        {
            if (fullCount > 0 && emptyCount < 20)
            {
                capsCurrentTick = num;
            }
            else
            {
                capsCurrentTick = currPoolEnergy;
            }
        }
        else
        {
            capsCurrentTick = num;
        }
        return capsCurrentTick;
    }

    public long InputUpdate(long remaining, int[] productRegister, int[] consumeRegister)
    {
        if (state != 1f)
        {
            return 0L;
        }
        long num = remaining;
        long num2 = CalculateActualEnergyPerTick(isOutput: false);
        num = num < num2 ? num : num2;
        if (num >= maxPoolEnergy - currPoolEnergy)
        {
            if (emptyCount > 0 && fullCount < 20)
            {
                if (num != remaining)
                {
                    num = num2;
                }
                currPoolEnergy -= maxPoolEnergy;
                int n = emptyCount;
                int m = emptyInc;
                byte b = (byte)split_inc(ref n, ref m, 1);
                emptyCount = (short)n;
                emptyInc = (short)m;
                fullCount++;
                fullInc += b;
                productRegister[fullId.OptimizedItemIndex]++;
                consumeRegister[emptyId.OptimizedItemIndex]++;
            }
            else
            {
                num = maxPoolEnergy - currPoolEnergy;
            }
        }
        currEnergyPerTick = num;
        currPoolEnergy += num;
        return num;
    }

    public long OutputUpdate(long energyPay, int[] productRegister, int[] consumeRegister)
    {
        if (state != -1f)
        {
            return 0L;
        }
        long num = energyPay;
        long num2 = CalculateActualEnergyPerTick(isOutput: true);
        num = num < num2 ? num : num2;
        if (num >= currPoolEnergy)
        {
            if (fullCount > 0 && emptyCount < 20)
            {
                if (num != energyPay)
                {
                    num = num2;
                }
                currPoolEnergy += maxPoolEnergy;
                int n = fullCount;
                int m = fullInc;
                byte b = (byte)split_inc(ref n, ref m, 1);
                fullCount = (short)n;
                fullInc = (short)m;
                emptyCount++;
                emptyInc += b;
                poolInc = b;
                productRegister[emptyId.OptimizedItemIndex]++;
                consumeRegister[fullId.OptimizedItemIndex]++;
            }
            else
            {
                num = currPoolEnergy;
                poolInc = 0;
            }
        }
        currEnergyPerTick = -num;
        currPoolEnergy -= num;
        return num;
    }

    public void BeltUpdate()
    {
        int num2 = 0;
        int num3 = 0;
        if (belt0 != null)
        {
            if (isOutput0)
            {
                num2++;
            }
            else
            {
                num3++;
            }
        }
        if (belt1 != null)
        {
            if (isOutput1)
            {
                num2++;
            }
            else
            {
                num3++;
            }
        }
        if (belt2 != null)
        {
            if (isOutput2)
            {
                num2++;
            }
            else
            {
                num3++;
            }
        }
        if (belt3 != null)
        {
            if (isOutput3)
            {
                num2++;
            }
            else
            {
                num3++;
            }
        }
        for (int i = 0; i < num3; i++)
        {
            if (i == 0 && inputRectify != inputSlot)
            {
                inputSlot = inputRectify;
            }
            OptimizedCargoPath? num = null;
            bool flag = false;
            if (inputSlot == 0)
            {
                if (belt0 != null)
                {
                    num = belt0;
                    flag = isOutput0;
                }
            }
            else if (inputSlot == 1)
            {
                if (belt1 != null)
                {
                    num = belt1;
                    flag = isOutput1;
                }
            }
            else if (inputSlot == 2)
            {
                if (belt2 != null)
                {
                    num = belt2;
                    flag = isOutput2;
                }
            }
            else if (inputSlot == 3 && belt3 != null)
            {
                num = belt3;
                flag = isOutput3;
            }
            if (num != null)
            {
                inputRectify = ComputeInsertOrPick(num, flag) ? FindTheNextSlot(flag) : inputRectify;
                inputSlot = FindTheNextSlot(flag);
            }
        }
        for (int j = 0; j < num2; j++)
        {
            if (j == 0 && outputRectify != outputSlot)
            {
                outputSlot = outputRectify;
            }
            OptimizedCargoPath? num = null;
            bool flag = false;
            if (outputSlot == 0)
            {
                if (belt0 != null)
                {
                    num = belt0;
                    flag = isOutput0;
                }
            }
            else if (outputSlot == 1)
            {
                if (belt1 != null)
                {
                    num = belt1;
                    flag = isOutput1;
                }
            }
            else if (outputSlot == 2)
            {
                if (belt2 != null)
                {
                    num = belt2;
                    flag = isOutput2;
                }
            }
            else if (outputSlot == 3 && belt3 != null)
            {
                num = belt3;
                flag = isOutput3;
            }
            if (num != null)
            {
                outputRectify = ComputeInsertOrPick(num, flag) ? FindTheNextSlot(flag) : outputRectify;
                outputSlot = FindTheNextSlot(flag);
            }
        }
    }

    public bool ComputeInsertOrPick(OptimizedCargoPath belt, bool isOutput)
    {
        if (belt != null)
        {
            if (isOutput)
            {
                if (state == 0f)
                {
                    return InsertItemToBelt(belt);
                }
                if (state == -1f)
                {
                    return InsertItemToBelt(belt, isEmptyAcc: true);
                }
                if (state == 1f)
                {
                    return InsertItemToBelt(belt, isEmptyAcc: false);
                }
            }
            else if (!isOutput)
            {
                if (state == 0f)
                {
                    return PickItemFromBelt(belt);
                }
                if (state == -1f)
                {
                    return PickItemFromBelt(belt);
                }
                if (state == 1f)
                {
                    return PickItemFromBelt(belt);
                }
            }
        }
        return false;
    }

    private bool InsertItemToBelt(OptimizedCargoPath belt, bool isEmptyAcc)
    {
        if (isEmptyAcc)
        {
            if (emptyCount > 0)
            {
                int n = emptyCount;
                int m = emptyInc;
                byte inc = (byte)split_inc(ref n, ref m, 1);
                if (belt.TryInsertItemAtHead(emptyId.ItemIndex, 1, inc))
                {
                    emptyCount = (short)n;
                    emptyInc = (short)m;
                    return true;
                }
            }
        }
        else if (fullCount > 0)
        {
            int n2 = fullCount;
            int m2 = fullInc;
            byte inc2 = (byte)split_inc(ref n2, ref m2, 1);
            if (belt.TryInsertItemAtHead(fullId.ItemIndex, 1, inc2))
            {
                fullCount = (short)n2;
                fullInc = (short)m2;
                return true;
            }
        }
        return false;
    }

    private bool InsertItemToBelt(OptimizedCargoPath belt)
    {
        if (emptyCount > 0)
        {
            int n = emptyCount;
            int m = emptyInc;
            byte inc = (byte)split_inc(ref n, ref m, 1);
            if (belt.TryInsertItemAtHead(emptyId.ItemIndex, 1, inc))
            {
                emptyCount = (short)n;
                emptyInc = (short)m;
                return true;
            }
        }
        if (fullCount > 0)
        {
            int n2 = fullCount;
            int m2 = fullInc;
            byte inc2 = (byte)split_inc(ref n2, ref m2, 1);
            if (belt.TryInsertItemAtHead(fullId.ItemIndex, 1, inc2))
            {
                fullCount = (short)n2;
                fullInc = (short)m2;
                return true;
            }
        }
        return false;
    }

    private bool PickItemFromBelt(OptimizedCargoPath beltId)
    {
        if (emptyCount < 5 && emptyId.ItemIndex == CargoPathMethods.TryPickItemAtRear(beltId, emptyId.ItemIndex, null, out var stack, out var inc))
        {
            emptyCount += stack;
            emptyInc += inc;
            return true;
        }
        if (fullCount < 5 && fullId.ItemIndex == CargoPathMethods.TryPickItemAtRear(beltId, fullId.ItemIndex, null, out stack, out inc))
        {
            fullCount += stack;
            fullInc += inc;
            return true;
        }
        return false;
    }

    private readonly int FindTheNextSlot(bool isOutput)
    {
        if (isOutput)
        {
            int num = outputSlot;
            for (int i = 0; i < 4; i++)
            {
                num++;
                num = num <= 3 ? num : num - 4;
                if (num == 0 && belt0 != null && isOutput0)
                {
                    return num;
                }
                if (num == 1 && belt1 != null && isOutput1)
                {
                    return num;
                }
                if (num == 2 && belt2 != null && isOutput2)
                {
                    return num;
                }
                if (num == 3 && belt3 != null && isOutput3)
                {
                    return num;
                }
            }
        }
        else
        {
            int num2 = inputSlot;
            for (int j = 0; j < 4; j++)
            {
                num2++;
                num2 = num2 <= 3 ? num2 : num2 - 4;
                if (num2 == 0 && belt0 != null && !isOutput0)
                {
                    return num2;
                }
                if (num2 == 1 && belt1 != null && !isOutput1)
                {
                    return num2;
                }
                if (num2 == 2 && belt2 != null && !isOutput2)
                {
                    return num2;
                }
                if (num2 == 3 && belt3 != null && !isOutput3)
                {
                    return num2;
                }
            }
        }
        return 0;
    }

    private readonly long CalculateActualEnergyPerTick(bool isOutput)
    {
        int num;
        if (isOutput)
        {
            num = poolInc;
        }
        else
        {
            int n = emptyCount;
            int m = emptyInc;
            num = split_inc(ref n, ref m, 1);
        }
        if (num != 0)
        {
            return energyPerTick + (long)(energyPerTick * Cargo.accTableMilli[num] + 0.1);
        }
        return energyPerTick;
    }

    private static int split_inc(ref int n, ref int m, int p)
    {
        if (n == 0)
        {
            return 0;
        }
        int num = m / n;
        int num2 = m - num * n;
        n -= p;
        num2 -= n;
        num = num2 > 0 ? num * p + num2 : num * p;
        m -= num;
        return num;
    }
}
