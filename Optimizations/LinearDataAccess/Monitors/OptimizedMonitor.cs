using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Monitors;

/// <summary>
/// Monitors are tightly coupled with the <see cref="WarningSystem"/>.
/// If i wan to optimize this further then i also have to deal with the <see cref="WarningSystem"/>
/// which i currently don't want to do.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct OptimizedMonitor
{
    private readonly OptimizedCargoPath targetBelt;
    private readonly int targetBeltSpeed;
    private readonly int targetBeltOffset;

    public OptimizedMonitor(OptimizedCargoPath targetBelt, int targetBeltSpeed, int targetBeltOffset)
    {
        this.targetBelt = targetBelt;
        this.targetBeltSpeed = targetBeltSpeed;
        this.targetBeltOffset = targetBeltOffset;
    }

    public void InternalUpdate(ref MonitorComponent monitor, float power, bool sandbox, SpeakerComponent[] _speakerPool)
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
        monitor.periodCargoBytesArray[monitor.periodTickCount - 1] = monitor.cargoFlow;
        bool flag = power > 0.1f;
        int num3 = targetBeltOffset + monitor.offset;
        int num4 = num3 + 10;
        if (monitor.prewarmSampleTick < monitor.periodTickCount * 2)
        {
            monitor.prewarmSampleTick++;
        }
        GetCargoAtIndexByFilter(monitor.cargoFilter, targetBelt, num3, out var cargo, out int cargoId, out int num5);
        if (monitor.lastCargoId == -1 && cargoId >= 0)
        {
            num = ((cargoId != monitor.formerCargoId) ? (num + (10 - num5 - 1) * cargo.stack) : (num - (num5 + 1) * cargo.stack));
        }
        else if (monitor.lastCargoId >= 0 && cargoId >= 0)
        {
            num = ((monitor.lastCargoId == cargoId) ? (num + (monitor.lastCargoOffset - num5) * cargo.stack) : ((monitor.formerCargoId != cargoId) ? (num + ((monitor.lastCargoOffset + 1) * monitor.lastCargoStack + (10 - num5 - 1) * cargo.stack)) : (num + ((monitor.lastCargoOffset + 1) * monitor.lastCargoStack + (10 - num5 - 1) * cargo.stack - 10 * (monitor.lastCargoStack + cargo.stack)))));
        }
        else if (monitor.lastCargoId >= 0 && cargoId == -1)
        {
            num += (monitor.lastCargoOffset + 1) * monitor.lastCargoStack;
        }
        if (num4 < targetBelt.pathLength)
        {
            GetCargoAtIndexByFilter(monitor.cargoFilter, targetBelt, num4, out var _, out monitor.formerCargoId, out _);
        }
        else
        {
            monitor.formerCargoId = -1;
        }
        monitor.lastCargoId = cargoId;
        monitor.lastCargoOffset = num5;
        monitor.lastCargoStack = cargo.stack;
        if (sandbox && monitor.spawnItemOperator > 0 && flag)
        {
            int num7 = ((targetBeltSpeed == 1) ? 10 : ((targetBeltSpeed != 2) ? 2 : 5));
            double num8 = (double)monitor.targetCargoBytes / 10.0;
            double num9 = (double)power * num8 / (double)(int)monitor.periodTickCount;
            monitor.spawnItemAccumulator += num9;
            int num10 = (int)(num9 * (double)num7 + 0.99996);
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
            if (monitor.spawnItemAccumulator >= (double)num10 + 0.99996)
            {
                monitor.spawnItemAccumulator = (double)num10 + 0.99996;
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
                    if (targetBelt.QueryItemAtIndex(index, out var stack, out var _) == monitor.cargoFilter)
                    {
                        int cargoIdAtIndex = targetBelt.GetCargoIdAtIndex(index, 10);
                        int num13 = num10 - stack;
                        if (num13 > 0)
                        {
                            num12 = ((num11 >= num13) ? num13 : num11);
                            targetBelt.cargoContainer.cargoPool[cargoIdAtIndex].stack += (byte)num12;
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
                    int cargoIdAtIndex2 = targetBelt.GetCargoIdAtIndex(index2, 10);
                    if (cargoIdAtIndex2 >= 0)
                    {
                        byte stack2;
                        int num15 = targetBelt.QueryItemAtIndex(index2, out stack2, out _);
                        if (monitor.cargoFilter == 0 || (monitor.cargoFilter > 0 && num15 == monitor.cargoFilter))
                        {
                            int num14;
                            if (num11 >= stack2)
                            {
                                targetBelt.RemoveCargoAtIndex(index2);
                                num14 = stack2;
                            }
                            else
                            {
                                targetBelt.cargoContainer.cargoPool[cargoIdAtIndex2].stack -= (byte)num11;
                                num14 = num11;
                            }
                            monitor.spawnItemAccumulator -= num14;
                            num += num14 * 10;
                        }
                    }
                }
            }
        }
        monitor.cargoFlow += num;
        monitor.totalCargoBytes += num;
        monitor.cargoBytesArray[monitor.periodTickCount - 1] = (sbyte)num;
        monitor.periodCargoBytesArray[monitor.periodTickCount - 1] = monitor.cargoFlow;
        monitor.isSpeakerAlarming = Alarming(ref monitor, monitor.alarmMode) && flag;
        monitor.isSystemAlarming = Alarming(ref monitor, monitor.systemWarningMode);
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

    private bool Alarming(ref MonitorComponent monitor, int _mode)
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
                        GetCargoAtIndexByFilter(monitor.cargoFilter, targetBelt, num + i, out _, out int cargoId, out _);
                        if (cargoId < 0)
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
                            int num3 = (targetBelt.closed ? 9 : 0);
                            int pathLength = targetBelt.pathLength;
                            for (int j = -25; j < 26; j++)
                            {
                                int num4 = num + j;
                                num4 = ((num4 < num3) ? num3 : num4);
                                num4 = ((num4 >= pathLength) ? (pathLength - 1) : num4);
                                GetCargoAtIndexByFilter(monitor.cargoFilter, targetBelt, num4, out _, out int cargoId, out _);
                                if (cargoId < 0)
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

    private void GetCargoAtIndexByFilter(int filter, OptimizedCargoPath path, int index, out OptimizedCargo cargo, out int cargoId, out int offset)
    {
        path.GetCargoAtIndex(index, out cargo, out cargoId, out offset);
        if (cargoId >= 0 && cargo.item != filter && filter != 0)
        {
            cargo.item = 0;
            cargoId = -1;
            offset = -1;
        }
    }
}
