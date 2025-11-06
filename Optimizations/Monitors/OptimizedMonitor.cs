using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Monitors;

/// <summary>
/// Monitors are tightly coupled with the <see cref="WarningSystem"/>.
/// If i wanr to optimize this further then i also have to deal with the <see cref="WarningSystem"/>
/// which i currently don't want to do.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack=1)]
internal readonly struct OptimizedMonitor
{
    private readonly BeltIndex targetBeltIndex;
    private readonly int targetBeltSpeed;
    private readonly int targetBeltOffset;

    public OptimizedMonitor(BeltIndex targetBeltIndex, int targetBeltSpeed, int targetBeltOffset)
    {
        this.targetBeltIndex = targetBeltIndex;
        this.targetBeltSpeed = targetBeltSpeed;
        this.targetBeltOffset = targetBeltOffset;
    }

    public void InternalUpdate(ref MonitorComponent monitor, float power, bool sandbox, SpeakerComponent[] _speakerPool, OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (monitor.periodTickCount < 60)
        {
            monitor.SetPeriodTickCount(60);
        }
        int num = 0;
        monitor.cargoFlow -= monitor.cargoBytesArray[0];
        Array.Copy(monitor.cargoBytesArray, 1, monitor.cargoBytesArray, 0, monitor.periodTickCount - 1);
        if (monitor.periodCargoBytesArray == null || monitor.periodCargoBytesArray.Length != monitor.cargoBytesArray.Length)
        {
            monitor.SetPeriodTickCount(monitor.periodTickCount);
        }
        Array.Copy(monitor.periodCargoBytesArray, 1, monitor.periodCargoBytesArray, 0, monitor.periodTickCount - 1);
        monitor.cargoBytesArray[monitor.periodTickCount - 1] = 0;
        monitor.periodCargoBytesArray![monitor.periodTickCount - 1] = monitor.cargoFlow;
        bool flag = power > 0.1f;
        int num3 = targetBeltOffset + monitor.offset;
        int num4 = num3 + 10;
        if (monitor.prewarmSampleTick < monitor.periodTickCount * 2)
        {
            monitor.prewarmSampleTick++;
        }
        ref OptimizedCargoPath targetBelt = ref targetBeltIndex.GetBelt(optimizedCargoPaths);
        GetCargoAtIndexByFilter(monitor.cargoFilter, ref targetBelt, num3, out var cargo, out int cargoBufferIndex, out int num5);
        if (monitor.lastCargoId == -1 && cargoBufferIndex >= 0)
        {
            num = cargoBufferIndex != monitor.formerCargoId ? num + (10 - num5 - 1) * cargo.Stack : num - (num5 + 1) * cargo.Stack;
        }
        else if (monitor.lastCargoId >= 0 && cargoBufferIndex >= 0)
        {
            num = monitor.lastCargoId == cargoBufferIndex ? num + (monitor.lastCargoOffset - num5) * cargo.Stack : monitor.formerCargoId != cargoBufferIndex ? num + (monitor.lastCargoOffset + 1) * monitor.lastCargoStack + (10 - num5 - 1) * cargo.Stack : num + ((monitor.lastCargoOffset + 1) * monitor.lastCargoStack + (10 - num5 - 1) * cargo.Stack - 10 * (monitor.lastCargoStack + cargo.Stack));
        }
        else if (monitor.lastCargoId >= 0 && cargoBufferIndex == -1)
        {
            num += (monitor.lastCargoOffset + 1) * monitor.lastCargoStack;
        }
        if (num4 < targetBelt.pathLength)
        {
            GetCargoAtIndexByFilter(monitor.cargoFilter, ref targetBelt, num4, out var _, out monitor.formerCargoId, out _);
        }
        else
        {
            monitor.formerCargoId = -1;
        }
        monitor.lastCargoId = cargoBufferIndex;
        monitor.lastCargoOffset = num5;
        monitor.lastCargoStack = cargo.Stack;
        if (sandbox && monitor.spawnItemOperator > 0 && flag)
        {
            int num7 = targetBeltSpeed == 1 ? 10 : targetBeltSpeed != 2 ? 2 : 5;
            double num8 = monitor.targetCargoBytes / 10.0;
            double num9 = (double)power * num8 / monitor.periodTickCount;
            monitor.spawnItemAccumulator += num9;
            int num10 = (int)(num9 * num7 + 0.99996);
            if (num10 < 1)
            {
                num10 = 1;
            }
            else if (num10 > 4)
            {
                num10 = 4;
            }
            if (monitor.spawnItemOperator == 2)
            {
                num10 = 4;
            }
            if (monitor.spawnItemAccumulator >= num10 + 0.99996)
            {
                monitor.spawnItemAccumulator = num10 + 0.99996;
            }
            int num11 = (int)(monitor.spawnItemAccumulator + 1E-08);
            if (num11 >= num10)
            {
                num11 = num10;
            }
            if (num11 > 0)
            {
                if (monitor.cargoFilter > 0 && monitor.spawnItemOperator == 1)
                {
                    int num12 = 0;
                    int index = num3 + 10;
                    OptimizedCargo cargo1 = targetBelt.QueryItemAtIndex(index, out int cargoBufferIndex1);
                    if (cargo1.Item == monitor.cargoFilter)
                    {
                        int num13 = num10 - cargo1.Stack;
                        if (num13 > 0)
                        {
                            num12 = num11 >= num13 ? num13 : num11;
                            cargo1.Stack += (byte)num12;
                            targetBelt.SetCargoInBuffer(cargoBufferIndex1, cargo1);
                        }
                    }
                    else if (targetBelt.TryInsertItem(index, monitor.cargoFilter, (byte)num11, 0))
                    {
                        num12 = num11;
                    }
                    monitor.spawnItemAccumulator -= num12;
                    num += num12 * 10;
                }
                else if (monitor.spawnItemOperator == 2)
                {
                    int index2 = num3 - 10;
                    OptimizedCargo cargo1 = targetBelt.QueryItemAtIndex(index2, out int cargoBufferIndex1);
                    if (monitor.cargoFilter == 0 || monitor.cargoFilter > 0 && cargo1.Item == monitor.cargoFilter)
                    {
                        int num14;
                        if (num11 >= cargo1.Stack)
                        {
                            targetBelt.RemoveCargoAtIndex(index2);
                            num14 = cargo1.Stack;
                        }
                        else
                        {
                            cargo1.Stack -= (byte)num11;
                            targetBelt.SetCargoInBuffer(cargoBufferIndex1, cargo1);
                            num14 = num11;
                        }
                        monitor.spawnItemAccumulator -= num14;
                        num += num14 * 10;
                    }
                }
            }
        }
        monitor.cargoFlow += num;
        monitor.totalCargoBytes += num;
        monitor.cargoBytesArray[monitor.periodTickCount - 1] = (sbyte)num;
        monitor.periodCargoBytesArray[monitor.periodTickCount - 1] = monitor.cargoFlow;
        monitor.isSpeakerAlarming = Alarming(ref monitor, monitor.alarmMode, ref targetBelt) && flag;
        monitor.isSystemAlarming = Alarming(ref monitor, monitor.systemWarningMode, ref targetBelt);
        if (monitor.isSpeakerAlarming)
        {
            _speakerPool[monitor.speakerId].Play(ESpeakerPlaybackOrigin.Current);
        }
        else
        {
            _speakerPool[monitor.speakerId].Stop();
        }
        _speakerPool[monitor.speakerId].SetPowerRatio(power);
    }

    private bool Alarming(ref MonitorComponent monitor, int _mode, ref OptimizedCargoPath targetBelt)
    {
        if (monitor.periodTickCount == 0 || _mode == 0)
        {
            return false;
        }

        MonitorComponent.ELogicState logicState = monitor.GetLogicState();
        switch (_mode)
        {
            case 1:
                return logicState == MonitorComponent.ELogicState.Fail;
            case 2:
                return logicState == MonitorComponent.ELogicState.Pass;
            default:
                {
                    int num = targetBeltOffset;
                    bool flag = true;
                    for (int i = 0; i < targetBeltSpeed; i++)
                    {
                        GetCargoAtIndexByFilter(monitor.cargoFilter, ref targetBelt, num + i, out _, out int cargoBufferIndex, out _);
                        if (cargoBufferIndex < 0)
                        {
                            flag = false;
                            break;
                        }
                    }
                    switch (_mode)
                    {
                        case 3:
                            return flag;
                        case 4:
                            return !flag;
                        case 5:
                            if (flag)
                            {
                                return logicState == MonitorComponent.ELogicState.Fail;
                            }
                            return false;
                        case 6:
                            flag = true;
                            int num3 = targetBelt.closed ? 9 : 0;
                            int pathLength = targetBelt.pathLength;
                            for (int j = -25; j < 26; j++)
                            {
                                int num4 = num + j;
                                num4 = num4 < num3 ? num3 : num4;
                                num4 = num4 >= pathLength ? pathLength - 1 : num4;
                                GetCargoAtIndexByFilter(monitor.cargoFilter, ref targetBelt, num4, out _, out int cargoBufferIndex, out _);
                                if (cargoBufferIndex < 0)
                                {
                                    flag = false;
                                    break;
                                }
                            }
                            if (!flag)
                            {
                                return logicState == MonitorComponent.ELogicState.Fail;
                            }
                            return false;
                        default:
                            return false;
                    }
                }
        }
    }

    private static void GetCargoAtIndexByFilter(int filter, ref OptimizedCargoPath targetBelt, int index, out OptimizedCargo cargo, out int cargoBufferIndex, out int offset)
    {
        targetBelt.GetCargoAtIndex(index, out cargo, out cargoBufferIndex, out offset);
        if (cargoBufferIndex >= 0 && cargo.Item != filter && filter != 0)
        {
            cargo.Item = 0;
            cargoBufferIndex = -1;
            offset = -1;
        }
    }
}
