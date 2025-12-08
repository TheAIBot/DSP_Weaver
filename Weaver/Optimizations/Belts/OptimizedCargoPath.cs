using System.Runtime.InteropServices;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Belts;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedCargoPath
{
    public BeltBuffer buffer;
    private readonly ReadonlyArray<int> chunks; // begin, length, speed
    public readonly int outputIndex = -1;
    public readonly bool closed;
    public readonly int bufferLength;
    public readonly int chunkCount;
    public BeltIndex outputCargoPathIndex;
    private int outputChunk;
    private bool lastUpdateFrameOdd;
    public readonly int pathLength => bufferLength;

    public OptimizedCargoPath(BeltBuffer buffer, CargoPath cargoPath, UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        //if (buffer.Length != cargoPath.bufferLength)
        //{
        //    throw new InvalidOperationException("Expectation that buffer.length and cargoPath.bufferLength is equal was not correct.");
        //}
        this.buffer = buffer;
        chunks = universeStaticDataBuilder.DeduplicateArrayUnmanaged(cargoPath.chunks);
        outputIndex = cargoPath.outputIndex;
        closed = cargoPath.closed;
        bufferLength = cargoPath.bufferLength;
        chunkCount = cargoPath.chunkCount;
        outputCargoPathIndex = BeltIndex.NoBelt;
        outputChunk = cargoPath.outputChunk;
        lastUpdateFrameOdd = cargoPath.lastUpdateFrameOdd;
    }

    public OptimizedCargoPath(BeltBuffer buffer,
                              ReadonlyArray<int> chunks,
                              int outputIndex,
                              bool closed,
                              int bufferLength,
                              int chunkCount,
                              BeltIndex outputCargoPathIndex,
                              int outputChunk,
                              bool lastUpdateFrameOdd)
    {
        this.buffer = buffer;
        this.chunks = chunks;
        this.outputIndex = outputIndex;
        this.closed = closed;
        this.bufferLength = bufferLength;
        this.chunkCount = chunkCount;
        this.outputCargoPathIndex = outputCargoPathIndex;
        this.outputChunk = outputChunk;
        this.lastUpdateFrameOdd = lastUpdateFrameOdd;
    }

    public readonly void Save(CargoPath cargoPath)
    {
        cargoPath.outputChunk = outputChunk;
        cargoPath.lastUpdateFrameOdd = lastUpdateFrameOdd;
        cargoPath.updateLen = bufferLength;
    }

    public void SetOutputPath(BeltIndex cargoPathIndex)
    {
        outputCargoPathIndex = cargoPathIndex;
    }

    public bool TryInsertItem(int index, int itemId, byte stack, byte inc)
    {
        int num = index + 5;
        int num2 = index - 5;
        if (index < 4)
        {
            return false;
        }
        if (num >= bufferLength)
        {
            return false;
        }
        if (!buffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num2))
        {
            return false;
        }
        if (num + 6 < bufferLength)
        {
            int nonZeroValueIndex = buffer.GetIndexOfNonZeroValue(num + 1, 5);
            if (nonZeroValueIndex != -1)
            {
                index = nonZeroValueIndex - 1 - 5;
            }
        }
        if (index < 4)
        {
            return false;
        }
        int num3 = index + 5;
        int num4 = index - 4;
        if (buffer.GetBufferValue(num4, out int actualIndex) == 0 && (!closed || num4 >= 10))
        {
            buffer.SetCargoWithPaddingFromActualIndex(actualIndex, itemId, stack, inc);
            return true;
        }
        int num5 = num3 - 2880;
        if (num5 < 0)
        {
            num5 = 0;
        }
        int num6 = 0;
        int num7 = 0;
        bool flag2 = false;
        bool flag3 = false;
        for (int num8 = num3; num8 >= num5; num8--)
        {
            if (buffer.GetBufferValue(num8) == 0)
            {
                num7++;
                if (!flag2)
                {
                    num6++;
                }
                if (num6 == 10 && (!closed || num8 >= 10))
                {
                    buffer.SetCargoWithPadding(index - 4, itemId, stack, inc);
                    return true;
                }
                if (num7 == 10 && (!closed || num8 >= 10))
                {
                    flag3 = true;
                    break;
                }
            }
            else
            {
                flag2 = true;
                if (num6 < 1)
                {
                    return false;
                }
                if (buffer.GetBufferValue(num8) == byte.MaxValue)
                {
                    num8 -= 9;
                }
            }
        }
        if (closed && !flag3 && num7 >= 10 && num7 < 20 && num3 < 2880)
        {
            num7 -= 10;
            if (num6 > 10)
            {
                num6 = 10;
            }
            int num9 = bufferLength - 1;
            while (num9 > num3 && num9 > bufferLength + num3 - 2880)
            {
                if (buffer.GetBufferValue(num9) == 0)
                {
                    num7++;
                }
                else if (buffer.GetBufferValue(num9) == byte.MaxValue)
                {
                    num9 -= 9;
                }
                if (num7 >= 10)
                {
                    if (num6 == 10)
                    {
                        buffer.SetCargoWithPadding(index - 4, itemId, stack, inc);
                        return true;
                    }
                    flag3 = true;
                    break;
                }
                num9--;
            }
        }
        if (flag3)
        {
            int num10 = 10 - num6;
            int num11 = num3 - num6 + 1;
            for (int num12 = num4; num12 >= num5; num12--)
            {
                if (buffer.GetBufferValue(num12) == 246)
                {
                    int num13 = 0;
                    int num14 = num12 - 1;
                    while (num14 >= num5 && num13 < num10 && buffer.GetBufferValue(num14) == 0)
                    {
                        num13++;
                        num14--;
                    }
                    if (num13 > 0)
                    {
                        buffer.Copy(num12, num12 - num13, num11 - num12);
                        num10 -= num13;
                        num11 -= num13;
                        num12 -= num13;
                    }
                }
            }
            if (num10 == 0)
            {
                buffer.SetCargoWithPadding(index - 4, itemId, stack, inc);
                return true;
            }
            Assert.CannotBeReached("断言失败：插入货物逻辑有误");
        }
        return false;
    }

    public void TryInsertItemWithStackIncreasement(int index, int itemId, int maxStack, ref int count, ref int inc)
    {
        int num = index + 5;
        if (num >= 0 && num < bufferLength)
        {
            int num2 = buffer.GetBufferValue(num, out int actualIndex);
            if (num2 > 0)
            {
                int offset = num2 < 246 ? (246 - buffer.GetBufferValueFromActualIndex(actualIndex - 4)) : (250 - num2);
                actualIndex += offset;
                buffer.GetCargoFromActualIndex(actualIndex + 1, out OptimizedCargo optimizedCargo);
                AddItemStackToCargo(ref optimizedCargo, itemId, maxStack, ref count, ref inc);
                buffer.SetCargoFromActualIndex(actualIndex + 1, optimizedCargo);
            }
        }
        if (count == 0)
        {
            return;
        }
        int num4 = index - 4;
        if (num4 >= 0 && num4 < bufferLength)
        {
            int num5 = buffer.GetBufferValue(num4, out int actualIndex);
            if (num5 > 0)
            {
                int offset = num5 < 246 ? (246 - buffer.GetBufferValueFromActualIndex(actualIndex - 4)) : (250 - num5);
                actualIndex += offset;
                buffer.GetCargoFromActualIndex(actualIndex + 1, out OptimizedCargo optimizedCargo);
                AddItemStackToCargo(ref optimizedCargo, itemId, maxStack, ref count, ref inc);
                buffer.SetCargoFromActualIndex(actualIndex + 1, optimizedCargo);
            }
        }
        if (count == 0)
        {
            return;
        }
        int num7 = index - 5;
        if (index < 4 || num >= bufferLength)
        {
            return;
        }
        if (!buffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num7))
        {
            return;
        }
        if (num + 6 < bufferLength)
        {
            int nonZeroValueIndex = buffer.GetIndexOfNonZeroValue(num + 1, 5);
            if (nonZeroValueIndex != -1)
            {
                index = nonZeroValueIndex - 1 - 5;
            }
        }
        if (index < 4)
        {
            return;
        }
        int num8 = index + 5;
        int num9 = index - 4;
        if (buffer.GetBufferValue(num9, out int actualIndex2) == 0 && (!closed || num9 >= 10))
        {
            int num10 = count;
            int num11 = inc;
            if (count > maxStack)
            {
                num10 = maxStack;
                num11 = inc / count;
                int num12 = inc - num11 * count;
                count -= num10;
                num12 -= count;
                num11 = num12 > 0 ? num11 * num10 + num12 : num11 * num10;
                inc -= num11;
            }
            else
            {
                count = 0;
                inc = 0;
            }
            buffer.SetCargoWithPaddingFromActualIndex(actualIndex2, itemId, (byte)num10, (byte)num11);
            return;
        }
        int num13 = num8 - 2880;
        if (num13 < 0)
        {
            num13 = 0;
        }
        int num14 = 0;
        int num15 = 0;
        bool flag2 = false;
        bool flag3 = false;
        for (int num16 = num8; num16 >= num13; num16--)
        {
            if (buffer.GetBufferValue(num16) == 0)
            {
                num15++;
                if (!flag2)
                {
                    num14++;
                }
                if (num14 == 10 && (!closed || num16 >= 10))
                {
                    int num17 = count;
                    int num18 = inc;
                    if (count > maxStack)
                    {
                        num17 = maxStack;
                        num18 = inc / count;
                        int num19 = inc - num18 * count;
                        count -= num17;
                        num19 -= count;
                        num18 = num19 > 0 ? num18 * num17 + num19 : num18 * num17;
                        inc -= num18;
                    }
                    else
                    {
                        count = 0;
                        inc = 0;
                    }
                    buffer.SetCargoWithPadding(index - 4, itemId, (byte)num17, (byte)num18);
                    return;
                }
                if (num15 == 10 && (!closed || num16 >= 10))
                {
                    flag3 = true;
                    break;
                }
            }
            else
            {
                flag2 = true;
                if (num14 < 1)
                {
                    return;
                }
                if (buffer.GetBufferValue(num16) == byte.MaxValue)
                {
                    num16 -= 9;
                }
            }
        }
        if (closed && !flag3 && num15 >= 10 && num15 < 20 && num8 < 2880)
        {
            num15 -= 10;
            if (num14 > 10)
            {
                num14 = 10;
            }
            int num20 = bufferLength - 1;
            while (num20 > num8 && num20 > bufferLength + num8 - 2880)
            {
                if (buffer.GetBufferValue(num20) == 0)
                {
                    num15++;
                }
                else if (buffer.GetBufferValue(num20) == byte.MaxValue)
                {
                    num20 -= 9;
                }
                if (num15 >= 10)
                {
                    if (num14 == 10)
                    {
                        int num21 = count;
                        int num22 = inc;
                        if (count > maxStack)
                        {
                            num21 = maxStack;
                            num22 = inc / count;
                            int num23 = inc - num22 * count;
                            count -= num21;
                            num23 -= count;
                            num22 = num23 > 0 ? num22 * num21 + num23 : num22 * num21;
                            inc -= num22;
                        }
                        else
                        {
                            count = 0;
                            inc = 0;
                        }
                        buffer.SetCargoWithPadding(index - 4, itemId, (byte)num21, (byte)num22);
                        return;
                    }
                    flag3 = true;
                    break;
                }
                num20--;
            }
        }
        if (!flag3)
        {
            return;
        }
        int num24 = 10 - num14;
        int num25 = num8 - num14 + 1;
        for (int num26 = num9; num26 >= num13; num26--)
        {
            if (buffer.GetBufferValue(num26) == 246)
            {
                int num27 = 0;
                int num28 = num26 - 1;
                while (num28 >= num13 && num27 < num24 && buffer.GetBufferValue(num28) == 0)
                {
                    num27++;
                    num28--;
                }
                if (num27 > 0)
                {
                    buffer.Copy(num26, num26 - num27, num25 - num26);
                    num24 -= num27;
                    num25 -= num27;
                    num26 -= num27;
                }
            }
        }
        if (num24 == 0)
        {
            int num29 = count;
            int num30 = inc;
            if (count > maxStack)
            {
                num29 = maxStack;
                num30 = inc / count;
                int num31 = inc - num30 * count;
                count -= num29;
                num31 -= count;
                num30 = num31 > 0 ? num30 * num29 + num31 : num30 * num29;
                inc -= num30;
            }
            else
            {
                count = 0;
                inc = 0;
            }
            buffer.SetCargoWithPadding(index - 4, itemId, (byte)num29, (byte)num30);
        }
        else
        {
            Assert.CannotBeReached("断言失败：插入货物逻辑有误");
        }
    }

    public readonly bool TryInsertCargoNoSqueeze(int index, OptimizedCargo optimizedCargo)
    {
        if (index < 4 || index + 5 >= bufferLength)
        {
            return false;
        }
        if (buffer.GetBufferValue(index + 5) != 0)
        {
            return false;
        }
        int num = index - 4;
        int num2 = index + 5;
        int nonZeroValueIndex = buffer.GetIndexOfNonZeroValue(num, num2 - num);
        if (nonZeroValueIndex != -1)
        {
            return false;
        }
        buffer.SetCargoWithPadding(index - 4, optimizedCargo);
        return true;
    }

    public readonly void InsertCargoAtHeadDirect(OptimizedCargo optimizedCargo, int headIndex)
    {
        buffer.SetCargoWithPadding(headIndex, optimizedCargo);
    }

    public readonly bool TryInsertItemAtHead(int itemId, byte stack, byte inc)
    {
        if (buffer.GetBufferValue(0, out int actualIndex) != 0 || buffer.GetBufferValue(9) != 0)
        {
            return false;
        }

        buffer.SetCargoWithPaddingFromActualIndex(actualIndex, itemId, stack, inc);
        return true;
    }

    public readonly bool TryInsertItemAtHeadAndFillBlank(int itemId, byte stack, byte inc)
    {
        int num = TestBlankAtHead();
        if (num < 0)
        {
            return false;
        }
        int num2 = num + 9;
        if (bufferLength <= num2)
        {
            return false;
        }

        buffer.SetCargoWithPadding(num, itemId, stack, inc);
        return true;
    }

    public readonly bool TryUpdateItemAtHeadAndFillBlank(int itemId, int maxStack, byte stack, byte inc)
    {
        int num = TestBlankAtHead();
        if (num < 0)
        {
            if (!TryGetCargoIdAtIndex(0, 10, out OptimizedCargo cargo, out int actualCargoBufferIndex))
            {
                return false;
            }
            int stack2 = cargo.Stack;
            if (cargo.Item == itemId && stack2 + stack <= maxStack)
            {
                cargo.Stack += stack;
                cargo.Inc += inc;
                buffer.SetCargoFromActualIndex(actualCargoBufferIndex, cargo);
                return true;
            }
            return false;
        }
        int num2 = num + 9;
        if (bufferLength <= num2)
        {
            return false;
        }

        buffer.SetCargoWithPadding(num, itemId, stack, inc);
        return true;
    }

    public readonly bool QueryItemAtIndex(int index, out OptimizedCargo optimizedCargo, out int actualCargoBufferIndex)
    {
        if (index < 0)
        {
            actualCargoBufferIndex = -1;
            optimizedCargo = default;
            return false;
        }
        if (index >= bufferLength)
        {
            actualCargoBufferIndex = -1;
            optimizedCargo = default;
            return false;
        }
        if (buffer.GetBufferValue(index) == 0)
        {
            actualCargoBufferIndex = -1;
            optimizedCargo = default;
            return false;
        }
        int num = index + 10 - 1;
        if (num >= bufferLength)
        {
            num = bufferLength - 1;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out optimizedCargo, out int actualIndex))
        {
            actualCargoBufferIndex = actualIndex + 1;
            return true;
        }

        Assert.CannotBeReached();
        actualCargoBufferIndex = -1;
        return false;
    }

    public bool RemoveCargoAtIndex(int index)
    {
        if (index < 0)
        {
            return false;
        }
        if (index >= bufferLength)
        {
            return false;
        }
        if (buffer.GetBufferValue(index) == 0)
        {
            return false;
        }
        int num = index + 10 - 1;
        if (num >= bufferLength)
        {
            num = bufferLength - 1;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index + 1, out _, out int actualIndex))
        {
            buffer.ClearFromActualIndex(actualIndex - 4, 10);
            return true;
        }

        Assert.CannotBeReached();
        return false;
    }

    public bool TryPickFuel(int index, int length, int filter, OptimizedItemId[]? fuelMask, out OptimizedCargo optimizedCargo)
    {
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= bufferLength)
        {
            index = bufferLength - 1;
        }


        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out OptimizedCargo cargo, out int actualIndex))
        {
            for (int j = 0; j < fuelMask.Length; j++)
            {
                if (fuelMask[j].ItemIndex == cargo.Item)
                {
                    buffer.ClearFromActualIndex(actualIndex - 4, 10);
                    optimizedCargo = cargo;
                    return true;
                }
            }
        }

        optimizedCargo = default;
        return false;
    }

    public void TryPickItem(int index, int length, out OptimizedCargo optimizedCargo)
    {
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= bufferLength)
        {
            index = bufferLength - 1;
        }

        TryPickItemWithoutBoundsChecks(index, length, out optimizedCargo);
    }

    public void TryPickItemWithoutBoundsChecks(int index, int length, out OptimizedCargo optimizedCargo)
    {
        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out optimizedCargo, out int actualIndex))
        {
            buffer.ClearFromActualIndex(actualIndex - 4, 10);
            return;
        }

        optimizedCargo = default;
    }

    public bool TryPickItem(int index, int length, int filter, out OptimizedCargo cargo)
    {
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= bufferLength)
        {
            index = bufferLength - 1;
        }

        return TryPickItemWithoutBoundsChecks(index, length, filter, out cargo);
    }

    public bool TryPickItemWithoutBoundsChecks(int index, int length, int filter, out OptimizedCargo cargo)
    {
        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out OptimizedCargo optimizedCargo, out int actualIndex))
        {
            if (filter == 0 || optimizedCargo.Item == filter)
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                cargo = optimizedCargo;
                return true;
            }
        }

        cargo = default;
        return false;
    }

    public bool TryPickItem(int index, int length, int filter, int[] needs, out OptimizedCargo optimizedCargo)
    {
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= bufferLength)
        {
            index = bufferLength - 1;
        }
        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out optimizedCargo, out int actualIndex))
        {
            int item = optimizedCargo.Item;
            if ((filter == 0 || item == filter) && (item == needs[0] || item == needs[1] || item == needs[2] || item == needs[3] || item == needs[4] || item == needs[5]))
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                return true;
            }
        }

        optimizedCargo = default;
        return false;
    }

    public void TryPickItemWithoutBoundsChecks(int index, int length, int filter, ComponentNeeds componentNeeds, short[] needsPatterns, int needsSize, out OptimizedCargo optimizedCargo)
    {
        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        if (buffer.TryGetCargoWithinRange(index, num - index, out optimizedCargo, out int actualIndex))
        {
            int item = optimizedCargo.Item;
            if ((filter == 0 || item == filter) && AnyMatch(componentNeeds, needsPatterns, needsSize, item))
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                return;
            }
        }

        optimizedCargo = default;
    }

    public void TryRemoveItemAtRear()
    {
        int num = bufferLength - 5 - 1;
        if (buffer.GetBufferValue(num, out int actualIndex) == 250)
        {
            buffer.ClearFromActualIndex(actualIndex - 4, 10);
        }
    }

    /// <summary>
    /// This method return -1 if there is an item in the first 10 fields.
    /// If there is no item in the first 10 fields but one in the first 20
    /// then it return the index to insert the item at so it's placed right behind the
    /// found item. Essentially an item can be placed forward so there is no holes in the
    /// item spacing.
    /// 
    /// Return -1 if the first 10 fields are not empty.
    /// Returns 0 if there is no items in the first 20 fields.
    /// Returns "index - 10" of the closest non 0 field within the first 20.
    /// </summary>
    public readonly int TestBlankAtHead()
    {
        int num = 9;
        if (buffer.GetBufferValue(num) != 0)
        {
            return -1;
        }
        if (bufferLength < 20)
        {
            return 0;
        }

        int indexOfNonZeroValue = buffer.GetIndexOfNonZeroValue(num + 1, 10);
        if (indexOfNonZeroValue == -1)
        {
            return 0;
        }

        return indexOfNonZeroValue - num - 1;
    }

    public readonly bool TryGetCargoIdAtRear(out OptimizedCargo cargo)
    {
        int num = bufferLength - 5 - 1;
        return buffer.TryGetCargo(num, out cargo);
    }

    public OptimizedCargo TryPickItemAtRear(int[] needs, out int needIdx)
    {
        needIdx = -1;
        int num = bufferLength - 5 - 1;
        if (buffer.TryGetCargo(num, out OptimizedCargo optimizedCargo, out int actualIndex))
        {
            int item = optimizedCargo.Item;
            if (item == needs[0])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 0;
                return optimizedCargo;
            }
            if (item == needs[1])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 1;
                return optimizedCargo;
            }
            if (item == needs[2])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 2;
                return optimizedCargo;
            }
            if (item == needs[3])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 3;
                return optimizedCargo;
            }
            if (item == needs[4])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 4;
                return optimizedCargo;
            }
            if (item == needs[5])
            {
                buffer.ClearFromActualIndex(actualIndex - 4, 10);
                needIdx = 5;
                return optimizedCargo;
            }
        }
        return default;
    }

    public bool TryPickCargoAtEnd(out OptimizedCargo cargo)
    {
        int num = bufferLength - 5 - 1;
        if (buffer.TryGetCargo(num, out cargo, out int actualIndex))
        {
            buffer.ClearFromActualIndex(actualIndex - 4, 10);
            return true;
        }

        cargo = default;
        return false;
    }

    public readonly bool GetCargoAtIndex(int index, out OptimizedCargo cargo, out int cargoBufferIndex, out int offset)
    {
        cargo = new OptimizedCargo(0, 1, 0);
        offset = -1;
        byte b = buffer.GetBufferValue(index);
        if (b == 0)
        {
            cargoBufferIndex = -1;
            return false;
        }
        int num = -1;
        if (b >= 246)
        {
            num = index - (b - 250);
        }
        else
        {
            for (int num2 = index; num2 >= index - 5; num2--)
            {
                if (buffer.GetBufferValue(num2) == 250)
                {
                    num = num2;
                    break;
                }
            }
        }
        if (num >= 0 && buffer.TryGetCargo(num, out cargo))
        {
            cargoBufferIndex = num + 1;
            offset = index - num + 4;
            return true;
        }

        cargoBufferIndex = -1;
        return false;
    }

    private readonly bool TryGetCargoIdAtIndex(int index, int length, out OptimizedCargo cargo, out int actualCargoBufferIndex)
    {
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= bufferLength)
        {
            index = bufferLength - 1;
        }
        int num = index + length;
        if (num > bufferLength)
        {
            num = bufferLength;
        }

        bool foundCargo = buffer.TryGetCargoWithinRange(index, num - index, out cargo, out int actualIndex);
        actualCargoBufferIndex = actualIndex + 1;
        return foundCargo;
    }

    public void Update(OptimizedCargoPath[] optimizedCargoPaths, long time)
    {
        if (outputCargoPathIndex.HasValue)
        {
            ref OptimizedCargoPath outputCargoPath = ref outputCargoPathIndex.GetBelt(optimizedCargoPaths);
            int num;
            if (outputCargoPath.chunkCount == 1)
            {
                num = outputCargoPath.chunks[2];
                outputChunk = 0;
            }
            else
            {
                int num2 = outputCargoPath.chunkCount - 1;
                if (outputChunk > num2)
                {
                    outputChunk = num2;
                }
                int num3 = 0;
                while (true)
                {
                    if (outputIndex < outputCargoPath.chunks[outputChunk * 3])
                    {
                        num2 = outputChunk - 1;
                        outputChunk = (num3 + num2) / 2;
                        continue;
                    }
                    if (outputIndex < outputCargoPath.chunks[outputChunk * 3] + outputCargoPath.chunks[outputChunk * 3 + 1])
                    {
                        break;
                    }
                    num3 = outputChunk + 1;
                    outputChunk = (num3 + num2) / 2;
                }
                num = outputCargoPath.chunks[outputChunk * 3 + 2];
            }
            int num4 = bufferLength - 5 - 1;
            if (buffer.GetBufferValue(num4, out int actualIndex) == 250)
            {
                buffer.GetCargoFromActualIndex(actualIndex + 1, out OptimizedCargo optimizedCargo);
                if (closed)
                {
                    if (outputCargoPath.TryInsertCargoNoSqueeze(outputIndex, optimizedCargo))
                    {
                        buffer.ClearFromActualIndex(actualIndex - 4, 10);
                    }
                }
                else if (outputCargoPath.TryInsertItem(lastUpdateFrameOdd == outputCargoPath.lastUpdateFrameOdd ? outputIndex : outputIndex + num > outputCargoPath.bufferLength - 6 ? outputCargoPath.bufferLength - 6 : outputIndex + num,
                                                       optimizedCargo.Item,
                                                       optimizedCargo.Stack,
                                                       optimizedCargo.Inc))
                {
                    buffer.ClearFromActualIndex(actualIndex - 4, 10);
                }
            }
        }
        else if (bufferLength <= 10)
        {
            return;
        }
        lastUpdateFrameOdd = (time & 1) == 1;

        buffer.Update(chunkCount, chunks);
    }

    private static bool AnyMatch(ComponentNeeds componentNeeds, short[] needsPatterns, int needsSize, int match)
    {
        for (int i = 0; i < needsSize; i++)
        {
            if (componentNeeds.GetNeeds(i) && needsPatterns[componentNeeds.PatternIndex + i] == match)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddItemStackToCargo(ref OptimizedCargo cargo, int itemId, int maxStack, ref int count, ref int inc)
    {
        if (cargo.Item == itemId && cargo.Stack < maxStack)
        {
            int num = maxStack - cargo.Stack;
            int num2 = inc;
            if (num < count)
            {
                num2 = inc / count;
                int num3 = inc - num2 * count;
                count -= num;
                num3 -= count;
                num2 = num3 > 0 ? num2 * num + num3 : num2 * num;
                inc -= num2;
            }
            else
            {
                num = count;
                count = 0;
                inc = 0;
            }
            cargo.Stack += (byte)num;
            cargo.Inc += (byte)num2;
        }
    }
}
