using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Splitters;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedSplitter
{
    private readonly int beltA;
    private readonly int beltB;
    private readonly int beltC;
    private readonly int beltD;
    private readonly int topId;
    private bool inPriority;
    private bool outPriority;
    private byte prioritySlotPresets;
    private int outFilter;
    private int outFilterPreset;
    private int input0;
    private int input1;
    private int input2;
    private int input3;
    private int output0;
    private int output1;
    private int output2;
    private int output3;

    public OptimizedSplitter(ref readonly SplitterComponent splitter)
    {
        beltA = splitter.beltA;
        beltB = splitter.beltB;
        beltC = splitter.beltC;
        beltD = splitter.beltD;
        topId = splitter.topId;
        inPriority = splitter.inPriority;
        outPriority = splitter.outPriority;
        prioritySlotPresets = splitter.prioritySlotPresets;
        outFilter = splitter.outFilter;
        outFilterPreset = splitter.outFilterPreset;
        input0 = splitter.input0;
        input1 = splitter.input1;
        input2 = splitter.input2;
        input3 = splitter.input3;
        output0 = splitter.output0;
        output1 = splitter.output1;
        output2 = splitter.output2;
        output3 = splitter.output3;
    }

    public void UpdateSplitter(CargoTraffic cargoTraffic, OptimizedSubFactory subFactory, BeltExecutor beltExecutor)
    {
        CheckPriorityPreset();
        if (topId == 0)
        {
            if (input0 == 0 || output0 == 0)
            {
                return;
            }
        }
        else if (input0 == 0 && output0 == 0)
        {
            return;
        }
        OptimizedCargoPath us_tmp_inputPath;
        OptimizedCargoPath us_tmp_inputPath0 = null;
        OptimizedCargoPath us_tmp_inputPath1 = null;
        OptimizedCargoPath us_tmp_inputPath2 = null;
        OptimizedCargoPath us_tmp_inputPath3 = null;
        int us_tmp_inputCargo;
        int us_tmp_inputCargo0 = -1;
        int us_tmp_inputCargo1 = -1;
        int us_tmp_inputCargo2 = -1;
        int us_tmp_inputCargo3 = -1;
        int us_tmp_inputIndex0 = -1;
        int us_tmp_inputIndex1 = -1;
        int us_tmp_inputIndex2 = -1;
        int us_tmp_inputIndex3 = -1;
        OptimizedCargoPath us_tmp_outputPath;
        OptimizedCargoPath us_tmp_outputPath0;
        int us_tmp_outputIdx;

        if (input0 != 0)
        {
            // SUPER HACK!!!!
            us_tmp_inputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[input0].segPathId));
            us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
            if (us_tmp_inputCargo != -1)
            {
                us_tmp_inputCargo0 = us_tmp_inputCargo;
                us_tmp_inputPath0 = us_tmp_inputPath;
                us_tmp_inputIndex0 = 0;
            }
            if (input1 != 0)
            {
                // SUPER HACK!!!!
                us_tmp_inputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[input1].segPathId));
                us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                if (us_tmp_inputCargo != -1)
                {
                    if (us_tmp_inputPath0 == null)
                    {
                        us_tmp_inputCargo0 = us_tmp_inputCargo;
                        us_tmp_inputPath0 = us_tmp_inputPath;
                        us_tmp_inputIndex0 = 1;
                    }
                    else
                    {
                        us_tmp_inputCargo1 = us_tmp_inputCargo;
                        us_tmp_inputPath1 = us_tmp_inputPath;
                        us_tmp_inputIndex1 = 1;
                    }
                }
                if (input2 != 0)
                {
                    // SUPER HACK!!!!
                    us_tmp_inputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[input2].segPathId));
                    us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                    if (us_tmp_inputCargo != -1)
                    {
                        if (us_tmp_inputPath0 == null)
                        {
                            us_tmp_inputCargo0 = us_tmp_inputCargo;
                            us_tmp_inputPath0 = us_tmp_inputPath;
                            us_tmp_inputIndex0 = 2;
                        }
                        else if (us_tmp_inputPath1 == null)
                        {
                            us_tmp_inputCargo1 = us_tmp_inputCargo;
                            us_tmp_inputPath1 = us_tmp_inputPath;
                            us_tmp_inputIndex1 = 2;
                        }
                        else
                        {
                            us_tmp_inputCargo2 = us_tmp_inputCargo;
                            us_tmp_inputPath2 = us_tmp_inputPath;
                            us_tmp_inputIndex2 = 2;
                        }
                    }
                    if (input3 != 0)
                    {
                        // SUPER HACK!!!!
                        us_tmp_inputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[input3].segPathId));
                        us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                        if (us_tmp_inputCargo != -1)
                        {
                            if (us_tmp_inputPath0 == null)
                            {
                                us_tmp_inputCargo0 = us_tmp_inputCargo;
                                us_tmp_inputPath0 = us_tmp_inputPath;
                                us_tmp_inputIndex0 = 3;
                            }
                            else if (us_tmp_inputPath1 == null)
                            {
                                us_tmp_inputCargo1 = us_tmp_inputCargo;
                                us_tmp_inputPath1 = us_tmp_inputPath;
                                us_tmp_inputIndex1 = 3;
                            }
                            else if (us_tmp_inputPath2 == null)
                            {
                                us_tmp_inputCargo2 = us_tmp_inputCargo;
                                us_tmp_inputPath2 = us_tmp_inputPath;
                                us_tmp_inputIndex2 = 3;
                            }
                            else
                            {
                                us_tmp_inputCargo3 = us_tmp_inputCargo;
                                us_tmp_inputPath3 = us_tmp_inputPath;
                                us_tmp_inputIndex3 = 3;
                            }
                        }
                    }
                }
            }
        }
        while (us_tmp_inputPath0 != null)
        {
            bool flag = true;
            if (outFilter != 0)
            {
                flag = beltExecutor.OptimizedCargoContainer.cargoPool[us_tmp_inputCargo0].item == outFilter;
            }
            us_tmp_outputPath0 = null;
            us_tmp_outputIdx = 0;
            int num = -1;
            if (!flag && outFilter != 0)
            {
                goto IL_03e5;
            }
            if (output0 != 0)
            {
                // SUPER HACK!!!!
                us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output0].segPathId));
                num = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength <= 10 || num < 0)
                {
                    goto IL_03e5;
                }
                us_tmp_outputPath0 = us_tmp_outputPath;
                us_tmp_outputIdx = 0;
            }
            goto IL_0514;
        IL_03e5:
            if ((!flag || outFilter == 0) && output1 != 0)
            {
                // SUPER HACK!!!!
                us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output1].segPathId));
                num = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                {
                    us_tmp_outputPath0 = us_tmp_outputPath;
                    us_tmp_outputIdx = 1;
                }
                else if (output2 != 0)
                {
                    // SUPER HACK!!!!
                    us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output2].segPathId));
                    num = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 2;
                    }
                    else if (output3 != 0)
                    {
                        // SUPER HACK!!!!
                        us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output3].segPathId));
                        num = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            goto IL_0514;
        IL_0514:
            if (us_tmp_outputPath0 != null)
            {
                int num2 = us_tmp_inputPath0.TryPickCargoAtEnd();
                Assert.True(num2 >= 0);
                us_tmp_outputPath0.InsertCargoAtHeadDirect(num2, num);
                InputAlternate(us_tmp_inputIndex0);
                OutputAlternate(us_tmp_outputIdx);
            }
            else if (topId != 0 && (flag || outFilter == 0) && subFactory.InsertCargoIntoStorage(topId, ref beltExecutor.OptimizedCargoContainer.cargoPool[us_tmp_inputCargo0]))
            {
                int num3 = us_tmp_inputPath0.TryPickCargoAtEnd();
                Assert.True(num3 >= 0);
                beltExecutor.OptimizedCargoContainer.RemoveCargo(num3);
                InputAlternate(us_tmp_inputIndex0);
            }
            us_tmp_inputPath0 = us_tmp_inputPath1;
            us_tmp_inputCargo0 = us_tmp_inputCargo1;
            us_tmp_inputIndex0 = us_tmp_inputIndex1;
            us_tmp_inputPath1 = us_tmp_inputPath2;
            us_tmp_inputCargo1 = us_tmp_inputCargo2;
            us_tmp_inputIndex1 = us_tmp_inputIndex2;
            us_tmp_inputPath2 = us_tmp_inputPath3;
            us_tmp_inputCargo2 = us_tmp_inputCargo3;
            us_tmp_inputIndex2 = us_tmp_inputIndex3;
            us_tmp_inputPath3 = null;
            us_tmp_inputCargo3 = -1;
            us_tmp_inputIndex3 = -1;
        }
        if (topId == 0)
        {
            return;
        }
        if (outFilter == 0)
        {
            int num4 = 4;
            while (num4-- > 0)
            {
                us_tmp_outputPath0 = null;
                us_tmp_outputIdx = 0;
                int num5 = -1;
                if (output0 != 0)
                {
                    // SUPER HACK!!!!
                    us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output0].segPathId));
                    num5 = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 0;
                    }
                    else if (output1 != 0)
                    {
                        // SUPER HACK!!!!
                        us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output1].segPathId));
                        num5 = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 1;
                        }
                        else if (output2 != 0)
                        {
                            // SUPER HACK!!!!
                            us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output2].segPathId));
                            num5 = us_tmp_outputPath.TestBlankAtHead();
                            if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                            {
                                us_tmp_outputPath0 = us_tmp_outputPath;
                                us_tmp_outputIdx = 2;
                            }
                            else if (output3 != 0)
                            {
                                // SUPER HACK!!!!
                                us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output3].segPathId));
                                num5 = us_tmp_outputPath.TestBlankAtHead();
                                if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                                {
                                    us_tmp_outputPath0 = us_tmp_outputPath;
                                    us_tmp_outputIdx = 3;
                                }
                            }
                        }
                    }
                }
                if (us_tmp_outputPath0 != null)
                {
                    int filter = us_tmp_outputIdx == 0 ? outFilter : -outFilter;
                    int inc;
                    int num6 = subFactory.PickFromStorageFiltered(topId, ref filter, 1, out inc);
                    if (filter > 0 && num6 > 0)
                    {
                        int cargoId = beltExecutor.OptimizedCargoContainer.AddCargo((short)filter, (byte)num6, (byte)inc);
                        us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId, num5);
                        OutputAlternate(us_tmp_outputIdx);
                        continue;
                    }
                    break;
                }
                break;
            }
            return;
        }
        us_tmp_outputPath0 = null;
        us_tmp_outputIdx = 0;
        int num7 = -1;
        if (output0 != 0)
        {
            // SUPER HACK!!!!
            us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output0].segPathId));
            num7 = us_tmp_outputPath.TestBlankAtHead();
            if (us_tmp_outputPath.pathLength > 10 && num7 >= 0)
            {
                us_tmp_outputPath0 = us_tmp_outputPath;
                us_tmp_outputIdx = 0;
            }
        }
        if (us_tmp_outputPath0 != null)
        {
            int filter2 = outFilter;
            int inc2;
            int num8 = subFactory.PickFromStorageFiltered(topId, ref filter2, 1, out inc2);
            if (filter2 > 0 && num8 > 0)
            {
                int cargoId2 = beltExecutor.OptimizedCargoContainer.AddCargo((short)filter2, (byte)num8, (byte)inc2);
                us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId2, num7);
                OutputAlternate(us_tmp_outputIdx);
            }
        }
        int num9 = 3;
        while (num9-- > 0)
        {
            us_tmp_outputPath0 = null;
            us_tmp_outputIdx = 0;
            int num10 = -1;
            if (output1 != 0)
            {
                // SUPER HACK!!!!
                us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output1].segPathId));
                num10 = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                {
                    us_tmp_outputPath0 = us_tmp_outputPath;
                    us_tmp_outputIdx = 1;
                }
                else if (output2 != 0)
                {
                    // SUPER HACK!!!!
                    us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output2].segPathId));
                    num10 = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 2;
                    }
                    else if (output3 != 0)
                    {
                        // SUPER HACK!!!!
                        us_tmp_outputPath = beltExecutor.GetOptimizedCargoPath(cargoTraffic.GetCargoPath(cargoTraffic.beltPool[output3].segPathId));
                        num10 = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            if (us_tmp_outputPath0 != null)
            {
                int filter3 = -outFilter;
                int inc3;
                int num11 = subFactory.PickFromStorageFiltered(topId, ref filter3, 1, out inc3);
                if (filter3 > 0 && num11 > 0)
                {
                    int cargoId3 = beltExecutor.OptimizedCargoContainer.AddCargo((short)filter3, (byte)num11, (byte)inc3);
                    us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId3, num10);
                    OutputAlternate(us_tmp_outputIdx);
                    continue;
                }
                break;
            }
            break;
        }
    }

    private void CheckPriorityPreset()
    {
        if (prioritySlotPresets > 0)
        {
            if ((prioritySlotPresets & 1) == 1)
            {
                SetPriority(0, isPriority: true, outFilterPreset);
            }
            if ((prioritySlotPresets & 2) == 2)
            {
                SetPriority(1, isPriority: true, outFilterPreset);
            }
            if ((prioritySlotPresets & 4) == 4)
            {
                SetPriority(2, isPriority: true, outFilterPreset);
            }
            if ((prioritySlotPresets & 8) == 8)
            {
                SetPriority(3, isPriority: true, outFilterPreset);
            }
        }
        else
        {
            outFilterPreset = 0;
        }
    }

    private void SetPriority(int slot, bool isPriority, int filter)
    {
        int num = 0;
        switch (slot)
        {
            case 0:
                num = beltA;
                break;
            case 1:
                num = beltB;
                break;
            case 2:
                num = beltC;
                break;
            case 3:
                num = beltD;
                break;
        }
        if (num == 0)
        {
            if (isPriority)
            {
                prioritySlotPresets |= (byte)(1 << slot);
                outFilterPreset = filter;
            }
            else
            {
                prioritySlotPresets &= (byte)(~(1 << slot));
            }
            return;
        }
        prioritySlotPresets &= (byte)(~(1 << slot));
        if (prioritySlotPresets == 0)
        {
            outFilterPreset = 0;
        }
        if (isPriority)
        {
            if (input0 == num)
            {
                inPriority = true;
            }
            else if (input1 == num)
            {
                inPriority = true;
                int num2 = input0;
                input0 = input1;
                input1 = num2;
            }
            else if (input2 == num)
            {
                inPriority = true;
                int num2 = input0;
                input0 = input2;
                input2 = num2;
            }
            else if (input3 == num)
            {
                inPriority = true;
                int num2 = input0;
                input0 = input3;
                input3 = num2;
            }
            else if (output0 == num)
            {
                outPriority = true;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output1 == num)
            {
                outPriority = true;
                int num2 = output0;
                output0 = output1;
                output1 = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output2 == num)
            {
                outPriority = true;
                int num2 = output0;
                output0 = output2;
                output2 = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output3 == num)
            {
                outPriority = true;
                int num2 = output0;
                output0 = output3;
                output3 = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            InputReorder();
            OutputReorder();
        }
        else if (input0 == num)
        {
            inPriority = false;
        }
        else if (output0 == num)
        {
            outPriority = false;
            outFilter = 0;
            outFilterPreset = 0;
        }
    }

    private void InputAlternate(int index)
    {
        switch (index)
        {
            case 0:
                {
                    if (inPriority)
                    {
                        return;
                    }
                    int num = input0;
                    input0 = input1;
                    input1 = input2;
                    input2 = input3;
                    input3 = num;
                    break;
                }
            case 1:
                {
                    int num = input1;
                    input1 = input2;
                    input2 = input3;
                    input3 = num;
                    break;
                }
            case 2:
                {
                    int num = input2;
                    input2 = input3;
                    input3 = num;
                    break;
                }
        }
        InputReorder();
    }

    private void InputReorder()
    {
        if (input0 == 0)
        {
            if (input1 == 0)
            {
                if (input2 != 0)
                {
                    input0 = input2;
                    input1 = input3;
                    input2 = 0;
                    input3 = 0;
                }
                else if (input3 != 0)
                {
                    input0 = input3;
                    input1 = 0;
                    input2 = 0;
                    input3 = 0;
                }
                return;
            }
            input0 = input1;
            input1 = input2;
            input2 = input3;
            input3 = 0;
        }
        if (input1 == 0)
        {
            if (input2 != 0)
            {
                input1 = input2;
                input2 = input3;
                input3 = 0;
            }
            else if (input3 != 0)
            {
                input1 = input3;
                input2 = 0;
                input3 = 0;
            }
        }
        else if (input2 == 0)
        {
            input2 = input3;
            input3 = 0;
        }
    }

    private void OutputAlternate(int index)
    {
        switch (index)
        {
            case 0:
                {
                    if (outPriority)
                    {
                        return;
                    }
                    int num = output0;
                    output0 = output1;
                    output1 = output2;
                    output2 = output3;
                    output3 = num;
                    break;
                }
            case 1:
                {
                    int num = output1;
                    output1 = output2;
                    output2 = output3;
                    output3 = num;
                    break;
                }
            case 2:
                {
                    int num = output2;
                    output2 = output3;
                    output3 = num;
                    break;
                }
        }
        OutputReorder();
    }

    private void OutputReorder()
    {
        if (output0 == 0)
        {
            if (output1 == 0)
            {
                if (output2 != 0)
                {
                    output0 = output2;
                    output1 = output3;
                    output2 = 0;
                    output3 = 0;
                }
                else if (output3 != 0)
                {
                    output0 = output3;
                    output1 = 0;
                    output2 = 0;
                    output3 = 0;
                }
                return;
            }
            output0 = output1;
            output1 = output2;
            output2 = output3;
            output3 = 0;
        }
        if (output1 == 0)
        {
            if (output2 != 0)
            {
                output1 = output2;
                output2 = output3;
                output3 = 0;
            }
            else if (output3 != 0)
            {
                output1 = output3;
                output2 = 0;
                output3 = 0;
            }
        }
        else if (output2 == 0)
        {
            output2 = output3;
            output3 = 0;
        }
    }
}

internal sealed class SplitterExecutor
{
    private OptimizedSplitter[] _optimizedSplitters;

    public void GameTick(PlanetFactory planet, OptimizedSubFactory subFactory, BeltExecutor beltExecutor, long time)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        OptimizedSplitter[] optimizedSplitters = _optimizedSplitters;
        for (int i = 0; i < optimizedSplitters.Length; i++)
        {
            optimizedSplitters[i].UpdateSplitter(cargoTraffic, subFactory, beltExecutor);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        List<OptimizedSplitter> optimizedSplitters = [];
        foreach (int splitterIndex in subFactoryGraph.GetAllNodes()
                                                     .Where(x => x.EntityTypeIndex.EntityType == EntityType.Splitter)
                                                     .Select(x => x.EntityTypeIndex.Index)
                                                     .OrderBy(x => x))
        {
            ref readonly SplitterComponent splitter = ref planet.cargoTraffic.splitterPool[splitterIndex];
            if (splitter.id != splitterIndex)
            {
                continue;
            }

            optimizedSplitters.Add(new OptimizedSplitter(in splitter));
        }

        _optimizedSplitters = optimizedSplitters.ToArray();
    }
}
