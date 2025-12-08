using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Splitters;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedSplitter
{
    private readonly BeltIndex beltA;
    private readonly BeltIndex beltB;
    private readonly BeltIndex beltC;
    private readonly BeltIndex beltD;
    private readonly int topId;
    private bool inPriority;
    private bool outPriority;
    private byte prioritySlotPresets;
    private int outFilter;
    private int outFilterPreset;
    private BeltIndex input0Index;
    private BeltIndex input1Index;
    private BeltIndex input2Index;
    private BeltIndex input3Index;
    private BeltIndex output0Index;
    private BeltIndex output1Index;
    private BeltIndex output2Index;
    private BeltIndex output3Index;

    public OptimizedSplitter(ref readonly SplitterComponent splitter,
                             BeltIndex beltA,
                             BeltIndex beltB,
                             BeltIndex beltC,
                             BeltIndex beltD,
                             BeltIndex input0Index,
                             BeltIndex input1Index,
                             BeltIndex input2Index,
                             BeltIndex input3Index,
                             BeltIndex output0Index,
                             BeltIndex output1Index,
                             BeltIndex output2Index,
                             BeltIndex output3Index)
    {
        this.beltA = beltA;
        this.beltB = beltB;
        this.beltC = beltC;
        this.beltD = beltD;
        topId = splitter.topId;
        inPriority = splitter.inPriority;
        outPriority = splitter.outPriority;
        prioritySlotPresets = splitter.prioritySlotPresets;
        outFilter = splitter.outFilter;
        outFilterPreset = splitter.outFilterPreset;
        this.input0Index = input0Index;
        this.input1Index = input1Index;
        this.input2Index = input2Index;
        this.input3Index = input3Index;
        this.output0Index = output0Index;
        this.output1Index = output1Index;
        this.output2Index = output2Index;
        this.output3Index = output3Index;
    }

    public void UpdateSplitter(OptimizedSubFactory subFactory, OptimizedCargoPath[] optimizedCargoPaths)
    {
        CheckPriorityPreset();
        if (topId == 0)
        {
            if (!input0Index.HasValue || !output0Index.HasValue)
            {
                return;
            }
        }
        else if (!input0Index.HasValue && !output0Index.HasValue)
        {
            return;
        }
        BeltIndex us_tmp_inputPath0 = BeltIndex.NoBelt;
        BeltIndex us_tmp_inputPath1 = BeltIndex.NoBelt;
        BeltIndex us_tmp_inputPath2 = BeltIndex.NoBelt;
        BeltIndex us_tmp_inputPath3 = BeltIndex.NoBelt;
        OptimizedCargo us_tmp_inputCargo;
        OptimizedCargo us_tmp_inputCargo0 = default;
        OptimizedCargo us_tmp_inputCargo1 = default;
        OptimizedCargo us_tmp_inputCargo2 = default;
        OptimizedCargo us_tmp_inputCargo3 = default;
        int us_tmp_inputIndex0 = -1;
        int us_tmp_inputIndex1 = -1;
        int us_tmp_inputIndex2 = -1;
        int us_tmp_inputIndex3 = -1;
        BeltIndex us_tmp_outputPath0 = BeltIndex.NoBelt;
        int us_tmp_outputIdx;

        if (input0Index.HasValue)
        {
            if (input0Index.GetBelt(optimizedCargoPaths).TryGetCargoIdAtRear(out us_tmp_inputCargo))
            {
                us_tmp_inputCargo0 = us_tmp_inputCargo;
                us_tmp_inputPath0 = input0Index;
                us_tmp_inputIndex0 = 0;
            }
            if (input1Index.HasValue)
            {
                if (input1Index.GetBelt(optimizedCargoPaths).TryGetCargoIdAtRear(out us_tmp_inputCargo))
                {
                    if (!us_tmp_inputPath0.HasValue)
                    {
                        us_tmp_inputCargo0 = us_tmp_inputCargo;
                        us_tmp_inputPath0 = input1Index;
                        us_tmp_inputIndex0 = 1;
                    }
                    else
                    {
                        us_tmp_inputCargo1 = us_tmp_inputCargo;
                        us_tmp_inputPath1 = input1Index;
                        us_tmp_inputIndex1 = 1;
                    }
                }
                if (input2Index.HasValue)
                {
                    if (input2Index.GetBelt(optimizedCargoPaths).TryGetCargoIdAtRear(out us_tmp_inputCargo))
                    {
                        if (!us_tmp_inputPath0.HasValue)
                        {
                            us_tmp_inputCargo0 = us_tmp_inputCargo;
                            us_tmp_inputPath0 = input2Index;
                            us_tmp_inputIndex0 = 2;
                        }
                        else if (!us_tmp_inputPath1.HasValue)
                        {
                            us_tmp_inputCargo1 = us_tmp_inputCargo;
                            us_tmp_inputPath1 = input2Index;
                            us_tmp_inputIndex1 = 2;
                        }
                        else
                        {
                            us_tmp_inputCargo2 = us_tmp_inputCargo;
                            us_tmp_inputPath2 = input2Index;
                            us_tmp_inputIndex2 = 2;
                        }
                    }
                    if (input3Index.HasValue)
                    {
                        if (input3Index.GetBelt(optimizedCargoPaths).TryGetCargoIdAtRear(out us_tmp_inputCargo))
                        {
                            if (!us_tmp_inputPath0.HasValue)
                            {
                                us_tmp_inputCargo0 = us_tmp_inputCargo;
                                us_tmp_inputPath0 = input3Index;
                                us_tmp_inputIndex0 = 3;
                            }
                            else if (!us_tmp_inputPath1.HasValue)
                            {
                                us_tmp_inputCargo1 = us_tmp_inputCargo;
                                us_tmp_inputPath1 = input3Index;
                                us_tmp_inputIndex1 = 3;
                            }
                            else if (!us_tmp_inputPath2.HasValue)
                            {
                                us_tmp_inputCargo2 = us_tmp_inputCargo;
                                us_tmp_inputPath2 = input3Index;
                                us_tmp_inputIndex2 = 3;
                            }
                            else
                            {
                                us_tmp_inputCargo3 = us_tmp_inputCargo;
                                us_tmp_inputPath3 = input3Index;
                                us_tmp_inputIndex3 = 3;
                            }
                        }
                    }
                }
            }
        }
        while (us_tmp_inputPath0.HasValue)
        {
            bool flag = true;
            if (outFilter != 0)
            {
                flag = us_tmp_inputCargo0.Item == outFilter;
            }
            us_tmp_outputPath0 = BeltIndex.NoBelt;
            us_tmp_outputIdx = 0;
            int num = -1;
            if (!flag && outFilter != 0)
            {
                goto IL_03e5;
            }
            if (output0Index.HasValue)
            {
                ref OptimizedCargoPath belt = ref output0Index.GetBelt(optimizedCargoPaths);
                num = belt.TestBlankAtHead();
                if (belt.pathLength <= 10 || num < 0)
                {
                    goto IL_03e5;
                }
                us_tmp_outputPath0 = output0Index;
                us_tmp_outputIdx = 0;
            }
            goto IL_0514;
        IL_03e5:
            if ((!flag || outFilter == 0) && output1Index.HasValue)
            {
                ref OptimizedCargoPath belt = ref output1Index.GetBelt(optimizedCargoPaths);
                num = belt.TestBlankAtHead();
                if (belt.pathLength > 10 && num >= 0)
                {
                    us_tmp_outputPath0 = output1Index;
                    us_tmp_outputIdx = 1;
                }
                else if (output2Index.HasValue)
                {
                    belt = ref output2Index.GetBelt(optimizedCargoPaths);
                    num = belt.TestBlankAtHead();
                    if (belt.pathLength > 10 && num >= 0)
                    {
                        us_tmp_outputPath0 = output2Index;
                        us_tmp_outputIdx = 2;
                    }
                    else if (output3Index.HasValue)
                    {
                        belt = ref output3Index.GetBelt(optimizedCargoPaths);
                        num = belt.TestBlankAtHead();
                        if (belt.pathLength > 10 && num >= 0)
                        {
                            us_tmp_outputPath0 = output3Index;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            goto IL_0514;
        IL_0514:
            if (us_tmp_outputPath0.HasValue)
            {
                ref OptimizedCargoPath inputBelt = ref us_tmp_inputPath0.GetBelt(optimizedCargoPaths);
                ref OptimizedCargoPath outputBelt = ref us_tmp_outputPath0.GetBelt(optimizedCargoPaths);
                inputBelt.TryPickCargoAtEnd(out OptimizedCargo num2);
                Assert.True(num2.Item >= 0);
                outputBelt.InsertCargoAtHeadDirect(num2, num);
                InputAlternate(us_tmp_inputIndex0);
                OutputAlternate(us_tmp_outputIdx);
            }
            else if (topId != 0 && (flag || outFilter == 0) && subFactory.InsertCargoIntoStorage(topId, us_tmp_inputCargo0))
            {
                ref OptimizedCargoPath inputBelt = ref us_tmp_inputPath0.GetBelt(optimizedCargoPaths);
                inputBelt.TryPickCargoAtEnd(out OptimizedCargo num3);
                Assert.True(num3.Item >= 0);
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
            us_tmp_inputPath3 = BeltIndex.NoBelt;
            us_tmp_inputCargo3 = default;
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
                us_tmp_outputPath0 = BeltIndex.NoBelt;
                us_tmp_outputIdx = 0;
                int num5 = -1;
                if (output0Index.HasValue)
                {
                    ref OptimizedCargoPath belt = ref output0Index.GetBelt(optimizedCargoPaths);
                    num5 = belt.TestBlankAtHead();
                    if (belt.pathLength > 10 && num5 >= 0)
                    {
                        us_tmp_outputPath0 = output0Index;
                        us_tmp_outputIdx = 0;
                    }
                    else if (output1Index.HasValue)
                    {
                        belt = ref output1Index.GetBelt(optimizedCargoPaths);
                        num5 = belt.TestBlankAtHead();
                        if (belt.pathLength > 10 && num5 >= 0)
                        {
                            us_tmp_outputPath0 = output1Index;
                            us_tmp_outputIdx = 1;
                        }
                        else if (output2Index.HasValue)
                        {
                            belt = ref output2Index.GetBelt(optimizedCargoPaths);
                            num5 = belt.TestBlankAtHead();
                            if (belt.pathLength > 10 && num5 >= 0)
                            {
                                us_tmp_outputPath0 = output2Index;
                                us_tmp_outputIdx = 2;
                            }
                            else if (output3Index.HasValue)
                            {
                                belt = ref output3Index.GetBelt(optimizedCargoPaths);
                                num5 = belt.TestBlankAtHead();
                                if (belt.pathLength > 10 && num5 >= 0)
                                {
                                    us_tmp_outputPath0 = output3Index;
                                    us_tmp_outputIdx = 3;
                                }
                            }
                        }
                    }
                }
                if (us_tmp_outputPath0.HasValue)
                {
                    int filter = us_tmp_outputIdx == 0 ? outFilter : -outFilter;
                    int inc;
                    int num6 = subFactory.PickFromStorageFiltered(topId, ref filter, 1, out inc);
                    if (filter > 0 && num6 > 0)
                    {
                        OptimizedCargo optimizedCargo = new OptimizedCargo((short)filter, (byte)num6, (byte)inc);
                        ref OptimizedCargoPath belt = ref us_tmp_outputPath0.GetBelt(optimizedCargoPaths);
                        belt.InsertCargoAtHeadDirect(optimizedCargo, num5);
                        OutputAlternate(us_tmp_outputIdx);
                        continue;
                    }
                    break;
                }
                break;
            }
            return;
        }
        us_tmp_outputPath0 = BeltIndex.NoBelt;
        us_tmp_outputIdx = 0;
        int num7 = -1;
        if (output0Index.HasValue)
        {
            ref OptimizedCargoPath belt = ref output0Index.GetBelt(optimizedCargoPaths);
            num7 = belt.TestBlankAtHead();
            if (belt.pathLength > 10 && num7 >= 0)
            {
                us_tmp_outputPath0 = output0Index;
                us_tmp_outputIdx = 0;
            }
        }
        if (us_tmp_outputPath0.HasValue)
        {
            int filter2 = outFilter;
            int inc2;
            int num8 = subFactory.PickFromStorageFiltered(topId, ref filter2, 1, out inc2);
            if (filter2 > 0 && num8 > 0)
            {
                OptimizedCargo optimizedCargo = new OptimizedCargo((short)filter2, (byte)num8, (byte)inc2);
                ref OptimizedCargoPath belt = ref us_tmp_outputPath0.GetBelt(optimizedCargoPaths);
                belt.InsertCargoAtHeadDirect(optimizedCargo, num7);
                OutputAlternate(us_tmp_outputIdx);
            }
        }
        int num9 = 3;
        while (num9-- > 0)
        {
            us_tmp_outputPath0 = BeltIndex.NoBelt;
            us_tmp_outputIdx = 0;
            int num10 = -1;
            if (output1Index.HasValue)
            {
                ref OptimizedCargoPath belt = ref output1Index.GetBelt(optimizedCargoPaths);
                num10 = belt.TestBlankAtHead();
                if (belt.pathLength > 10 && num10 >= 0)
                {
                    us_tmp_outputPath0 = output1Index;
                    us_tmp_outputIdx = 1;
                }
                else if (output2Index.HasValue)
                {
                    belt = ref output2Index.GetBelt(optimizedCargoPaths);
                    num10 = belt.TestBlankAtHead();
                    if (belt.pathLength > 10 && num10 >= 0)
                    {
                        us_tmp_outputPath0 = output2Index;
                        us_tmp_outputIdx = 2;
                    }
                    else if (output3Index.HasValue)
                    {
                        belt = ref output3Index.GetBelt(optimizedCargoPaths);
                        num10 = belt.TestBlankAtHead();
                        if (belt.pathLength > 10 && num10 >= 0)
                        {
                            us_tmp_outputPath0 = output3Index;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            if (us_tmp_outputPath0.HasValue)
            {
                int filter3 = -outFilter;
                int inc3;
                int num11 = subFactory.PickFromStorageFiltered(topId, ref filter3, 1, out inc3);
                if (filter3 > 0 && num11 > 0)
                {
                    OptimizedCargo optimizedCargo = new OptimizedCargo((short)filter3, (byte)num11, (byte)inc3);
                    ref OptimizedCargoPath belt = ref us_tmp_outputPath0.GetBelt(optimizedCargoPaths);
                    belt.InsertCargoAtHeadDirect(optimizedCargo, num10);
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
        BeltIndex num = BeltIndex.NoBelt;
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
        if (!num.HasValue)
        {
            if (isPriority)
            {
                prioritySlotPresets |= (byte)(1 << slot);
                outFilterPreset = filter;
            }
            else
            {
                prioritySlotPresets &= (byte)~(1 << slot);
            }
            return;
        }
        prioritySlotPresets &= (byte)~(1 << slot);
        if (prioritySlotPresets == 0)
        {
            outFilterPreset = 0;
        }
        if (isPriority)
        {
            if (input0Index == num)
            {
                inPriority = true;
            }
            else if (input1Index == num)
            {
                inPriority = true;
                BeltIndex num2 = input0Index;
                input0Index = input1Index;
                input1Index = num2;
            }
            else if (input2Index == num)
            {
                inPriority = true;
                BeltIndex num2 = input0Index;
                input0Index = input2Index;
                input2Index = num2;
            }
            else if (input3Index == num)
            {
                inPriority = true;
                BeltIndex num2 = input0Index;
                input0Index = input3Index;
                input3Index = num2;
            }
            else if (output0Index == num)
            {
                outPriority = true;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output1Index == num)
            {
                outPriority = true;
                BeltIndex num2 = output0Index;
                output0Index = output1Index;
                output1Index = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output2Index == num)
            {
                outPriority = true;
                BeltIndex num2 = output0Index;
                output0Index = output2Index;
                output2Index = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            else if (output3Index == num)
            {
                outPriority = true;
                BeltIndex num2 = output0Index;
                output0Index = output3Index;
                output3Index = num2;
                outFilter = filter;
                outFilterPreset = 0;
            }
            InputReorder();
            OutputReorder();
        }
        else if (input0Index == num)
        {
            inPriority = false;
        }
        else if (output0Index == num)
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
                    BeltIndex num = input0Index;
                    input0Index = input1Index;
                    input1Index = input2Index;
                    input2Index = input3Index;
                    input3Index = num;
                    break;
                }
            case 1:
                {
                    BeltIndex num = input1Index;
                    input1Index = input2Index;
                    input2Index = input3Index;
                    input3Index = num;
                    break;
                }
            case 2:
                {
                    BeltIndex num = input2Index;
                    input2Index = input3Index;
                    input3Index = num;
                    break;
                }
        }
        InputReorder();
    }

    private void InputReorder()
    {
        if (!input0Index.HasValue)
        {
            if (!input1Index.HasValue)
            {
                if (input2Index.HasValue)
                {
                    input0Index = input2Index;
                    input1Index = input3Index;
                    input2Index = BeltIndex.NoBelt;
                    input3Index = BeltIndex.NoBelt;
                }
                else if (input3Index.HasValue)
                {
                    input0Index = input3Index;
                    input1Index = BeltIndex.NoBelt;
                    input2Index = BeltIndex.NoBelt;
                    input3Index = BeltIndex.NoBelt;
                }
                return;
            }
            input0Index = input1Index;
            input1Index = input2Index;
            input2Index = input3Index;
            input3Index = BeltIndex.NoBelt;
        }
        if (!input1Index.HasValue)
        {
            if (input2Index.HasValue)
            {
                input1Index = input2Index;
                input2Index = input3Index;
                input3Index = BeltIndex.NoBelt;
            }
            else if (input3Index.HasValue)
            {
                input1Index = input3Index;
                input2Index = BeltIndex.NoBelt;
                input3Index = BeltIndex.NoBelt;
            }
        }
        else if (!input2Index.HasValue)
        {
            input2Index = input3Index;
            input3Index = BeltIndex.NoBelt;
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
                    BeltIndex num = output0Index;
                    output0Index = output1Index;
                    output1Index = output2Index;
                    output2Index = output3Index;
                    output3Index = num;
                    break;
                }
            case 1:
                {
                    BeltIndex num = output1Index;
                    output1Index = output2Index;
                    output2Index = output3Index;
                    output3Index = num;
                    break;
                }
            case 2:
                {
                    BeltIndex num = output2Index;
                    output2Index = output3Index;
                    output3Index = num;
                    break;
                }
        }
        OutputReorder();
    }

    private void OutputReorder()
    {
        if (!output0Index.HasValue)
        {
            if (!output1Index.HasValue)
            {
                if (output2Index.HasValue)
                {
                    output0Index = output2Index;
                    output1Index = output3Index;
                    output2Index = BeltIndex.NoBelt;
                    output3Index = BeltIndex.NoBelt;
                }
                else if (output3Index.HasValue)
                {
                    output0Index = output3Index;
                    output1Index = BeltIndex.NoBelt;
                    output2Index = BeltIndex.NoBelt;
                    output3Index = BeltIndex.NoBelt;
                }
                return;
            }
            output0Index = output1Index;
            output1Index = output2Index;
            output2Index = output3Index;
            output3Index = BeltIndex.NoBelt;
        }
        if (!output1Index.HasValue)
        {
            if (output2Index.HasValue)
            {
                output1Index = output2Index;
                output2Index = output3Index;
                output3Index = BeltIndex.NoBelt;
            }
            else if (output3Index.HasValue)
            {
                output1Index = output3Index;
                output2Index = BeltIndex.NoBelt;
                output3Index = BeltIndex.NoBelt;
            }
        }
        else if (!output2Index.HasValue)
        {
            output2Index = output3Index;
            output3Index = BeltIndex.NoBelt;
        }
    }
}
