using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Inserters;

namespace Weaver.Optimizations.Tanks;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct OptimizedTank
{
    public const int NO_TANK = -1;
    private int id;
    private readonly OptimizedCargoPath? belt0;
    private readonly OptimizedCargoPath? belt1;
    private readonly OptimizedCargoPath? belt2;
    private readonly OptimizedCargoPath? belt3;
    private readonly bool isOutput0;
    private readonly bool isOutput1;
    private readonly bool isOutput2;
    private readonly bool isOutput3;
    private readonly int fluidCapacity;
    private readonly bool outputSwitch;
    private readonly bool inputSwitch;
    private readonly bool isBottom;
    private readonly int lastTankIndex;
    private readonly int nextTankIndex;
    private int fluidInc;
    public int fluidCount;
    public int fluidId;

    public OptimizedTank(ref readonly TankComponent tank,
                         int tankIndex,
                         OptimizedCargoPath? belt0,
                         OptimizedCargoPath? belt1,
                         OptimizedCargoPath? belt2,
                         OptimizedCargoPath? belt3)
    {
        id = tankIndex;
        lastTankIndex = int.MaxValue;
        nextTankIndex = int.MaxValue;
        this.belt0 = belt0;
        this.belt1 = belt1;
        this.belt2 = belt2;
        this.belt3 = belt3;
        isOutput0 = tank.isOutput0;
        isOutput1 = tank.isOutput1;
        isOutput2 = tank.isOutput2;
        isOutput3 = tank.isOutput3;
        fluidCapacity = tank.fluidCapacity;
        outputSwitch = tank.outputSwitch;
        inputSwitch = tank.inputSwitch;
        isBottom = tank.isBottom;
        fluidCount = tank.fluidCount;
        fluidInc = tank.fluidInc;
        fluidId = tank.fluidId;
    }

    public OptimizedTank(int? lastTankIndex,
                         int? nextTankIndex,
                         OptimizedTank tank)
    {
        id = tank.id;
        this.lastTankIndex = lastTankIndex ?? NO_TANK;
        this.nextTankIndex = nextTankIndex ?? NO_TANK;
        belt0 = tank.belt0;
        belt1 = tank.belt1;
        belt2 = tank.belt2;
        belt3 = tank.belt3;
        isOutput0 = tank.isOutput0;
        isOutput1 = tank.isOutput1;
        isOutput2 = tank.isOutput2;
        isOutput3 = tank.isOutput3;
        fluidCapacity = tank.fluidCapacity;
        outputSwitch = tank.outputSwitch;
        inputSwitch = tank.inputSwitch;
        isBottom = tank.isBottom;
        fluidCount = tank.fluidCount;
        fluidInc = tank.fluidInc;
        fluidId = tank.fluidId;
    }

    public void TickOutput(TankExecutor tankExecutor)
    {
        if (lastTankIndex == NO_TANK || !outputSwitch)
        {
            return;
        }
        OptimizedTank tankComponent = tankExecutor._optimizedTanks[lastTankIndex];
        if (!tankComponent.inputSwitch || tankComponent.fluidId > 0 && tankComponent.fluidId != fluidId)
        {
            return;
        }
        if (tankComponent.fluidCount <= tankComponent.fluidCapacity - 2)
        {
            if (fluidCount >= 2)
            {
                if (tankComponent.fluidId == 0)
                {
                    tankExecutor._optimizedTanks[lastTankIndex].fluidId = fluidId;
                }
                int n = fluidCount;
                int m = fluidInc;
                int num = 2;
                int num2 = split_inc(ref n, ref m, num);
                fluidCount -= num;
                fluidInc -= num2;
                tankExecutor._optimizedTanks[lastTankIndex].fluidCount += num;
                tankExecutor._optimizedTanks[lastTankIndex].fluidInc += num2;
            }
            else if (fluidCount > 0 && fluidCount < 2)
            {
                if (tankComponent.fluidId == 0)
                {
                    tankExecutor._optimizedTanks[lastTankIndex].fluidId = fluidId;
                }
                int num3 = fluidCount;
                int num4 = fluidInc;
                fluidCount = 0;
                fluidInc = 0;
                if (fluidId != 0)
                {
                    fluidId = 0;
                }
                tankExecutor._optimizedTanks[lastTankIndex].fluidCount += num3;
                tankExecutor._optimizedTanks[lastTankIndex].fluidInc += num4;
            }
        }
        else
        {
            if (tankComponent.fluidCount >= tankComponent.fluidCapacity || tankComponent.fluidCount <= tankComponent.fluidCapacity - 2)
            {
                return;
            }
            int num5 = tankComponent.fluidCapacity - tankComponent.fluidCount;
            if (fluidCount >= num5)
            {
                if (tankComponent.fluidId == 0)
                {
                    tankExecutor._optimizedTanks[lastTankIndex].fluidId = fluidId;
                }
                int n2 = fluidCount;
                int m2 = fluidInc;
                int num6 = num5;
                int num7 = split_inc(ref n2, ref m2, num6);
                fluidCount -= num6;
                fluidInc -= num7;
                tankExecutor._optimizedTanks[lastTankIndex].fluidCount += num6;
                tankExecutor._optimizedTanks[lastTankIndex].fluidInc += num7;
            }
            else if (fluidCount > 0 && fluidCount < num5)
            {
                if (tankComponent.fluidId == 0)
                {
                    tankExecutor._optimizedTanks[lastTankIndex].fluidId = fluidId;
                }
                int num8 = fluidCount;
                int num9 = fluidInc;
                fluidCount = 0;
                fluidInc = 0;
                if (fluidId != 0)
                {
                    fluidId = 0;
                }
                tankExecutor._optimizedTanks[lastTankIndex].fluidCount += num8;
                tankExecutor._optimizedTanks[lastTankIndex].fluidInc += num9;
            }
        }
    }

    public void GameTick(TankExecutor tankExecutor)
    {
        if (fluidInc < 0)
        {
            fluidInc = 0;
        }
        if (!isBottom)
        {
            return;
        }

        byte stack = 0;
        byte inc = 0;
        UpdateTankBelt(belt0, isOutput0, tankExecutor, ref stack, ref inc);
        UpdateTankBelt(belt1, isOutput1, tankExecutor, ref stack, ref inc);
        UpdateTankBelt(belt2, isOutput2, tankExecutor, ref stack, ref inc);
        UpdateTankBelt(belt3, isOutput3, tankExecutor, ref stack, ref inc);
    }

    public readonly void Save(ref TankComponent tank)
    {
        tank.fluidCount = fluidCount;
        tank.fluidInc = fluidInc;
        tank.fluidId = fluidId;
    }

    private void UpdateTankBelt(OptimizedCargoPath? belt, bool isOutput, TankExecutor tankExecutor, ref byte stack, ref byte inc)
    {
        if (belt != null)
        {
            if (isOutput && outputSwitch)
            {
                if (fluidId > 0 && fluidCount > 0)
                {
                    int num = fluidInc != 0 ? fluidInc / fluidCount : 0;
                    if (belt.TryInsertItemAtHeadAndFillBlank(fluidId, 1, (byte)num))
                    {
                        fluidCount--;
                        fluidInc -= num;
                    }
                }
            }
            else if (!isOutput && inputSwitch)
            {
                if (fluidId > 0 && fluidCount < fluidCapacity)
                {
                    bool hasCargo = CargoPathMethods.TryPickItemAtRear(belt, fluidId, null, out OptimizedCargo optimizedCargo);
                    stack = optimizedCargo.Stack;
                    inc = optimizedCargo.Inc;
                    if (hasCargo)
                    {
                        fluidCount += stack;
                        fluidInc += inc;
                    }
                }
                if (fluidId == 0)
                {
                    bool hasCargo = CargoPathMethods.TryPickItemAtRear(belt, 0, ItemProto.fluids, out OptimizedCargo optimizedCargo);
                    stack = optimizedCargo.Stack;
                    inc = optimizedCargo.Inc;
                    if (hasCargo)
                    {
                        fluidId = optimizedCargo.Item;
                        fluidCount += stack;
                        fluidInc += inc;
                    }
                }
                if (fluidCount >= fluidCapacity)
                {
                    belt.TryGetCargoIdAtRear(out OptimizedCargo optimizedCargo);
                    if (optimizedCargo.Item == fluidId && nextTankIndex > NO_TANK)
                    {
                        OptimizedTank tankComponent = tankExecutor._optimizedTanks[nextTankIndex];
                        OptimizedTank tankComponent2 = tankComponent;
                        while (tankComponent.fluidCount >= tankComponent.fluidCapacity)
                        {
                            OptimizedTank tankComponent3 = tankExecutor._optimizedTanks[tankComponent2.lastTankIndex];
                            if (tankComponent.fluidId != tankComponent3.fluidId)
                            {
                                tankComponent2 = tankComponent3;
                                break;
                            }
                            if (tankComponent.inputSwitch)
                            {
                                if (tankComponent.nextTankIndex > NO_TANK)
                                {
                                    tankComponent = tankExecutor._optimizedTanks[tankComponent.nextTankIndex];
                                    tankComponent2 = tankComponent;
                                    continue;
                                }
                                tankComponent2.id = id;
                                break;
                            }
                            tankComponent2 = tankExecutor._optimizedTanks[tankComponent2.lastTankIndex];
                            break;
                        }
                        OptimizedTank tankComponent4 = tankExecutor._optimizedTanks[tankComponent2.lastTankIndex];
                        if (!tankComponent2.inputSwitch || tankComponent2.fluidId != tankComponent4.fluidId && tankComponent2.fluidId != 0)
                        {
                            tankComponent2 = tankComponent4;
                        }
                        bool flag = true;
                        if (tankComponent2.id == id || tankComponent2.fluidCount >= tankComponent2.fluidCapacity || !tankComponent4.outputSwitch)
                        {
                            flag = false;
                        }
                        if (flag)
                        {
                            bool hasCargo = CargoPathMethods.TryPickItemAtRear(belt, fluidId, null, out OptimizedCargo someOtherCargo);
                            stack = someOtherCargo.Stack;
                            inc = someOtherCargo.Inc;
                            if (hasCargo)
                            {
                                if (tankExecutor._optimizedTanks[tankComponent2.id].fluidCount == 0)
                                {
                                    tankExecutor._optimizedTanks[tankComponent2.id].fluidId = fluidId;
                                }
                                tankExecutor._optimizedTanks[tankComponent2.id].fluidCount += stack;
                                tankExecutor._optimizedTanks[tankComponent2.id].fluidInc += inc;
                            }
                        }
                    }
                }
            }
        }
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
