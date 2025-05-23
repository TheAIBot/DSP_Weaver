using System;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

internal sealed class OptimizedCargoPath
{
    public readonly OptimizedCargoContainer cargoContainer;
    public readonly byte[] buffer;
    private readonly int[] chunks;
    public readonly int outputIndex = -1;
    public readonly bool closed;
    public readonly int bufferLength;
    public readonly int chunkCount;
    public OptimizedCargoPath? outputPath;
    private int outputChunk;
    private bool lastUpdateFrameOdd;
    public int updateLen;
    public int headSpeed => chunks[2];
    public int rearSpeed => chunks[chunkCount * 3 - 1];
    public int pathLength => bufferLength;

    public int cargoCapacity
    {
        get
        {
            if (!closed)
            {
                return bufferLength;
            }
            return bufferLength - 9;
        }
    }

    public OptimizedCargoPath(OptimizedCargoContainer _cargoContainer, byte[] buffer, CargoPath cargoPath)
    {
        cargoContainer = _cargoContainer;
        this.buffer = buffer;
        chunks = cargoPath.chunks;
        outputIndex = cargoPath.outputIndex;
        closed = cargoPath.closed;
        bufferLength = cargoPath.bufferLength;
        chunkCount = cargoPath.chunkCount;
        outputChunk = cargoPath.outputChunk;
        lastUpdateFrameOdd = cargoPath.lastUpdateFrameOdd;
        updateLen = cargoPath.updateLen;
    }

    public void Save(CargoPath cargoPath)
    {
        cargoPath.outputChunk = outputChunk;
        cargoPath.lastUpdateFrameOdd = lastUpdateFrameOdd;
        cargoPath.updateLen = updateLen;
    }

    public void SetOutputPath(OptimizedCargoPath cargoPath)
    {
        outputPath = cargoPath;
    }

    public bool TryInsertCargo(int index, int cargoId)
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
        bool flag = false;
        while (index > num2)
        {
            if (buffer[num] != 0)
            {
                index--;
                num--;
                continue;
            }
            flag = true;
            break;
        }
        if (!flag)
        {
            return false;
        }
        if (num + 6 < bufferLength)
        {
            if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
        }
        if (index < 4)
        {
            return false;
        }
        int num3 = index + 5;
        int num4 = 0;
        int num5 = 0;
        bool flag2 = false;
        bool flag3 = false;
        int num6 = num3;
        while (num6 >= num3 - 2880 && num6 >= 0)
        {
            if (buffer[num6] == 0)
            {
                num5++;
                if (!flag2)
                {
                    num4++;
                }
                if (num4 == 10 && (!closed || num6 >= 10))
                {
                    InsertCargoDirect(index, cargoId);
                    return true;
                }
                if (num5 == 10 && (!closed || num6 >= 10))
                {
                    flag3 = true;
                    break;
                }
            }
            else
            {
                flag2 = true;
                if (num4 < 1)
                {
                    return false;
                }
                if (buffer[num6] == byte.MaxValue)
                {
                    num6 -= 9;
                }
            }
            num6--;
        }
        if (closed && !flag3 && num5 >= 10 && num5 < 20 && num3 < 2880)
        {
            num5 -= 10;
            if (num4 > 10)
            {
                num4 = 10;
            }
            int num7 = bufferLength - 1;
            while (num7 > num3 && num7 > bufferLength + num3 - 2880)
            {
                if (buffer[num7] == 0)
                {
                    num5++;
                }
                else if (buffer[num7] == byte.MaxValue)
                {
                    num7 -= 9;
                }
                if (num5 >= 10)
                {
                    if (num4 == 10)
                    {
                        InsertCargoDirect(index, cargoId);
                        return true;
                    }
                    flag3 = true;
                    break;
                }
                num7--;
            }
        }
        if (flag3)
        {
            int num8 = 10 - num4;
            int num9 = num3 - num4 + 1;
            int num10 = index - 4;
            while (num10 >= num3 - 2880 && num10 >= 0)
            {
                if (buffer[num10] == 246)
                {
                    int num11 = 0;
                    int num12 = num10 - 1;
                    while (num12 >= num3 - 2880 && num12 >= 0 && num11 < num8 && buffer[num12] == 0)
                    {
                        num11++;
                        num12--;
                    }
                    if (num11 > 0)
                    {
                        Array.Copy(buffer, num10, buffer, num10 - num11, num9 - num10);
                        num8 -= num11;
                        num9 -= num11;
                        num10 -= num11;
                    }
                }
                num10--;
            }
            if (num8 == 0)
            {
                InsertCargoDirect(index, cargoId);
                return true;
            }
            Assert.CannotBeReached("断言失败：插入货物逻辑有误");
        }
        return false;
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
        bool flag = false;
        while (index > num2)
        {
            if (buffer[num] != 0)
            {
                index--;
                num--;
                continue;
            }
            flag = true;
            break;
        }
        if (!flag)
        {
            return false;
        }
        if (num + 6 < bufferLength)
        {
            if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
        }
        if (index < 4)
        {
            return false;
        }
        int num3 = index + 5;
        int num4 = index - 4;
        if (buffer[num4] == 0 && (!closed || num4 >= 10))
        {
            InsertItemDirect(index, itemId, stack, inc);
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
            if (buffer[num8] == 0)
            {
                num7++;
                if (!flag2)
                {
                    num6++;
                }
                if (num6 == 10 && (!closed || num8 >= 10))
                {
                    InsertItemDirect(index, itemId, stack, inc);
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
                if (buffer[num8] == byte.MaxValue)
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
                if (buffer[num9] == 0)
                {
                    num7++;
                }
                else if (buffer[num9] == byte.MaxValue)
                {
                    num9 -= 9;
                }
                if (num7 >= 10)
                {
                    if (num6 == 10)
                    {
                        InsertItemDirect(index, itemId, stack, inc);
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
                if (buffer[num12] == 246)
                {
                    int num13 = 0;
                    int num14 = num12 - 1;
                    while (num14 >= num5 && num13 < num10 && buffer[num14] == 0)
                    {
                        num13++;
                        num14--;
                    }
                    if (num13 > 0)
                    {
                        Array.Copy(buffer, num12, buffer, num12 - num13, num11 - num12);
                        num10 -= num13;
                        num11 -= num13;
                        num12 -= num13;
                    }
                }
            }
            if (num10 == 0)
            {
                InsertItemDirect(index, itemId, stack, inc);
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
            int num2 = buffer[num];
            if (num2 > 0)
            {
                int num3 = num;
                num3 = ((num2 < 246) ? (num3 + (246 - buffer[num - 4])) : (num3 + (250 - num2)));
                int cargoId = buffer[num3 + 1] - 1 + (buffer[num3 + 2] - 1) * 100 + (buffer[num3 + 3] - 1) * 10000 + (buffer[num3 + 4] - 1) * 1000000;
                cargoContainer.AddItemStackToCargo(cargoId, itemId, maxStack, ref count, ref inc);
            }
        }
        if (count == 0)
        {
            return;
        }
        int num4 = index - 4;
        if (num4 >= 0 && num4 < bufferLength)
        {
            int num5 = buffer[num4];
            if (num5 > 0)
            {
                int num6 = num4;
                num6 = ((num5 < 246) ? (num6 + (246 - buffer[num4 - 4])) : (num6 + (250 - num5)));
                int cargoId2 = buffer[num6 + 1] - 1 + (buffer[num6 + 2] - 1) * 100 + (buffer[num6 + 3] - 1) * 10000 + (buffer[num6 + 4] - 1) * 1000000;
                cargoContainer.AddItemStackToCargo(cargoId2, itemId, maxStack, ref count, ref inc);
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
        bool flag = false;
        while (index > num7)
        {
            if (buffer[num] != 0)
            {
                index--;
                num--;
                continue;
            }
            flag = true;
            break;
        }
        if (!flag)
        {
            return;
        }
        if (num + 6 < bufferLength)
        {
            if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
            else if (buffer[++num] != 0)
            {
                index = num - 1 - 5;
            }
        }
        if (index < 4)
        {
            return;
        }
        int num8 = index + 5;
        int num9 = index - 4;
        if (buffer[num9] == 0 && (!closed || num9 >= 10))
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
                num11 = ((num12 > 0) ? (num11 * num10 + num12) : (num11 * num10));
                inc -= num11;
            }
            else
            {
                count = 0;
                inc = 0;
            }
            InsertItemDirect(index, itemId, (byte)num10, (byte)num11);
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
            if (buffer[num16] == 0)
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
                        num18 = ((num19 > 0) ? (num18 * num17 + num19) : (num18 * num17));
                        inc -= num18;
                    }
                    else
                    {
                        count = 0;
                        inc = 0;
                    }
                    InsertItemDirect(index, itemId, (byte)num17, (byte)num18);
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
                if (buffer[num16] == byte.MaxValue)
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
                if (buffer[num20] == 0)
                {
                    num15++;
                }
                else if (buffer[num20] == byte.MaxValue)
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
                            num22 = ((num23 > 0) ? (num22 * num21 + num23) : (num22 * num21));
                            inc -= num22;
                        }
                        else
                        {
                            count = 0;
                            inc = 0;
                        }
                        InsertItemDirect(index, itemId, (byte)num21, (byte)num22);
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
            if (buffer[num26] == 246)
            {
                int num27 = 0;
                int num28 = num26 - 1;
                while (num28 >= num13 && num27 < num24 && buffer[num28] == 0)
                {
                    num27++;
                    num28--;
                }
                if (num27 > 0)
                {
                    Array.Copy(buffer, num26, buffer, num26 - num27, num25 - num26);
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
                num30 = ((num31 > 0) ? (num30 * num29 + num31) : (num30 * num29));
                inc -= num30;
            }
            else
            {
                count = 0;
                inc = 0;
            }
            InsertItemDirect(index, itemId, (byte)num29, (byte)num30);
        }
        else
        {
            Assert.CannotBeReached("断言失败：插入货物逻辑有误");
        }
    }

    public bool TryInsertCargoNoSqueeze(int index, int cargoId)
    {
        if (index < 4 || index + 5 >= bufferLength)
        {
            return false;
        }
        if (buffer[index + 5] != 0)
        {
            return false;
        }
        int num = index - 4;
        int num2 = index + 5;
        for (int i = num; i < num2; i++)
        {
            if (buffer[i] != 0)
            {
                return false;
            }
        }
        InsertCargoDirect(index, cargoId);
        return true;
    }

    public void InsertCargoAtHeadDirect(int cargoId)
    {
        buffer[0] = 246;
        buffer[1] = 247;
        buffer[2] = 248;
        buffer[3] = 249;
        buffer[4] = 250;
        buffer[5] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[6] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[7] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[8] = (byte)(cargoId % 100 + 1);
        buffer[9] = byte.MaxValue;
    }

    public void InsertCargoAtHeadDirect(int cargoId, int headIndex)
    {
        buffer[headIndex++] = 246;
        buffer[headIndex++] = 247;
        buffer[headIndex++] = 248;
        buffer[headIndex++] = 249;
        buffer[headIndex++] = 250;
        buffer[headIndex++] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[headIndex++] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[headIndex++] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[headIndex++] = (byte)(cargoId % 100 + 1);
        buffer[headIndex++] = byte.MaxValue;
    }

    private void InsertCargoDirect(int index, int cargoId)
    {
        int num = index - 4;
        buffer[num] = 246;
        buffer[num + 1] = 247;
        buffer[num + 2] = 248;
        buffer[num + 3] = 249;
        buffer[num + 4] = 250;
        buffer[num + 5] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[num + 6] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[num + 7] = (byte)(cargoId % 100 + 1);
        cargoId /= 100;
        buffer[num + 8] = (byte)(cargoId % 100 + 1);
        buffer[num + 9] = byte.MaxValue;
    }

    public void InsertItemDirect(int index, int itemId, byte stack, byte inc)
    {
        int num = cargoContainer.AddCargo((short)itemId, stack, inc);
        int num2 = index - 4;
        buffer[num2] = 246;
        buffer[num2 + 1] = 247;
        buffer[num2 + 2] = 248;
        buffer[num2 + 3] = 249;
        buffer[num2 + 4] = 250;
        buffer[num2 + 5] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[num2 + 6] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[num2 + 7] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[num2 + 8] = (byte)(num % 100 + 1);
        buffer[num2 + 9] = byte.MaxValue;
    }

    public bool InsertItemDirectWithFastCheck(int index, int itemId, byte stack, byte inc)
    {
        int num = index - 4;
        if (buffer[num] == 0 && buffer[num + 9] == 0)
        {
            int num2 = cargoContainer.AddCargo((short)itemId, stack, inc);
            buffer[num] = 246;
            buffer[num + 1] = 247;
            buffer[num + 2] = 248;
            buffer[num + 3] = 249;
            buffer[num + 4] = 250;
            buffer[num + 5] = (byte)(num2 % 100 + 1);
            num2 /= 100;
            buffer[num + 6] = (byte)(num2 % 100 + 1);
            num2 /= 100;
            buffer[num + 7] = (byte)(num2 % 100 + 1);
            num2 /= 100;
            buffer[num + 8] = (byte)(num2 % 100 + 1);
            buffer[num + 9] = byte.MaxValue;
            return true;
        }
        return false;
    }

    public bool TryInsertItemAtHead(int itemId, byte stack, byte inc)
    {
        if (buffer[0] != 0 || buffer[9] != 0)
        {
            return false;
        }
        int num = cargoContainer.AddCargo((short)itemId, stack, inc);
        buffer[0] = 246;
        buffer[1] = 247;
        buffer[2] = 248;
        buffer[3] = 249;
        buffer[4] = 250;
        buffer[5] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[6] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[7] = (byte)(num % 100 + 1);
        num /= 100;
        buffer[8] = (byte)(num % 100 + 1);
        buffer[9] = byte.MaxValue;
        return true;
    }

    public bool TryInsertItemAtHead(int itemId, byte stack, byte inc, int headIndex)
    {
        int num = headIndex + 9;
        if (bufferLength <= num)
        {
            return false;
        }
        if (buffer[headIndex] != 0 || buffer[num] != 0)
        {
            return false;
        }
        int num2 = cargoContainer.AddCargo((short)itemId, stack, inc);
        buffer[headIndex++] = 246;
        buffer[headIndex++] = 247;
        buffer[headIndex++] = 248;
        buffer[headIndex++] = 249;
        buffer[headIndex++] = 250;
        buffer[headIndex++] = (byte)(num2 % 100 + 1);
        num2 /= 100;
        buffer[headIndex++] = (byte)(num2 % 100 + 1);
        num2 /= 100;
        buffer[headIndex++] = (byte)(num2 % 100 + 1);
        num2 /= 100;
        buffer[headIndex++] = (byte)(num2 % 100 + 1);
        buffer[headIndex++] = byte.MaxValue;
        return true;
    }

    public bool TryInsertItemAtHeadAndFillBlank(int itemId, byte stack, byte inc)
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
        int num3 = cargoContainer.AddCargo((short)itemId, stack, inc);
        buffer[num++] = 246;
        buffer[num++] = 247;
        buffer[num++] = 248;
        buffer[num++] = 249;
        buffer[num++] = 250;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        buffer[num++] = byte.MaxValue;
        return true;
    }

    public bool TryUpdateItemAtHeadAndFillBlank(int itemId, int maxStack, byte stack, byte inc)
    {
        int num = TestBlankAtHead();
        if (num < 0)
        {
            int cargoIdAtIndex = GetCargoIdAtIndex(0, 10);
            if (cargoIdAtIndex == -1)
            {
                return false;
            }
            int stack2 = cargoContainer.cargoPool[cargoIdAtIndex].stack;
            if (cargoContainer.cargoPool[cargoIdAtIndex].item == itemId && stack2 + stack <= maxStack)
            {
                cargoContainer.cargoPool[cargoIdAtIndex].stack += stack;
                cargoContainer.cargoPool[cargoIdAtIndex].inc += inc;
                return true;
            }
            return false;
        }
        int num2 = num + 9;
        if (bufferLength <= num2)
        {
            return false;
        }
        int num3 = cargoContainer.AddCargo((short)itemId, stack, inc);
        buffer[num++] = 246;
        buffer[num++] = 247;
        buffer[num++] = 248;
        buffer[num++] = 249;
        buffer[num++] = 250;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        num3 /= 100;
        buffer[num++] = (byte)(num3 % 100 + 1);
        buffer[num++] = byte.MaxValue;
        return true;
    }

    public int QueryItemAtIndex(int index, out byte stack, out byte inc)
    {
        stack = 0;
        inc = 0;
        if (index < 0)
        {
            return 0;
        }
        if (index >= bufferLength)
        {
            return 0;
        }
        if (buffer[index] == 0)
        {
            return 0;
        }
        int num = index + 10 - 1;
        if (num >= bufferLength)
        {
            num = bufferLength - 1;
        }
        for (int i = index; i <= num; i++)
        {
            if (buffer[i] >= 246)
            {
                i += 250 - buffer[i];
                int num2 = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
                short item = cargoContainer.cargoPool[num2].item;
                stack = cargoContainer.cargoPool[num2].stack;
                inc = cargoContainer.cargoPool[num2].inc;
                return item;
            }
        }
        Assert.CannotBeReached();
        stack = 0;
        return 0;
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
        if (buffer[index] == 0)
        {
            return false;
        }
        int num = index + 10 - 1;
        if (num >= bufferLength)
        {
            num = bufferLength - 1;
        }
        for (int i = index; i <= num; i++)
        {
            if (buffer[i] >= 246)
            {
                i += 250 - buffer[i];
                int index2 = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
                Array.Clear(buffer, i - 4, 10);
                int num2 = i + 5 + 1;
                if (updateLen < num2)
                {
                    updateLen = num2;
                }
                cargoContainer.RemoveCargo(index2);
                return true;
            }
        }
        Assert.CannotBeReached();
        return false;
    }

    public void RemoveCargoAtIndexDirect(int index)
    {
        int index2 = buffer[index + 1] - 1 + (buffer[index + 2] - 1) * 100 + (buffer[index + 3] - 1) * 10000 + (buffer[index + 4] - 1) * 1000000;
        Array.Clear(buffer, index - 4, 10);
        int num = index + 5 + 1;
        if (updateLen < num)
        {
            updateLen = num;
        }
        cargoContainer.RemoveCargo(index2);
    }

    public OptimizedCargo PickCargoAtIndexDirect(int index)
    {
        int num = buffer[index + 1] - 1 + (buffer[index + 2] - 1) * 100 + (buffer[index + 3] - 1) * 10000 + (buffer[index + 4] - 1) * 1000000;
        Array.Clear(buffer, index - 4, 10);
        int num2 = index + 5 + 1;
        if (updateLen < num2)
        {
            updateLen = num2;
        }
        OptimizedCargo result = cargoContainer.cargoPool[num];
        cargoContainer.RemoveCargo(num);
        return result;
    }

    public int TryPickItem(int index, int length, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
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
        for (int i = index; i < num; i++)
        {
            if (buffer[i] >= 246)
            {
                i += 250 - buffer[i];
                int num2 = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
                short item = cargoContainer.cargoPool[num2].item;
                stack = cargoContainer.cargoPool[num2].stack;
                inc = cargoContainer.cargoPool[num2].inc;
                Array.Clear(buffer, i - 4, 10);
                int num3 = i + 5 + 1;
                if (updateLen < num3)
                {
                    updateLen = num3;
                }
                cargoContainer.RemoveCargo(num2);
                return item;
            }
        }
        return 0;
    }

    public int TryPickItem(int index, int length, int filter, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
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
        for (int i = index; i < num; i++)
        {
            if (buffer[i] < 246)
            {
                continue;
            }
            i += 250 - buffer[i];
            int num2 = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
            int item = cargoContainer.cargoPool[num2].item;
            stack = cargoContainer.cargoPool[num2].stack;
            inc = cargoContainer.cargoPool[num2].inc;
            if (filter == 0 || item == filter)
            {
                Array.Clear(buffer, i - 4, 10);
                int num3 = i + 5 + 1;
                if (updateLen < num3)
                {
                    updateLen = num3;
                }
                cargoContainer.RemoveCargo(num2);
                return item;
            }
            return 0;
        }
        return 0;
    }

    public int TryPickItem(int index, int length, int filter, int[] needs, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
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
        for (int i = index; i < num; i++)
        {
            if (buffer[i] < 246)
            {
                continue;
            }
            i += 250 - buffer[i];
            int num2 = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
            int item = cargoContainer.cargoPool[num2].item;
            stack = cargoContainer.cargoPool[num2].stack;
            inc = cargoContainer.cargoPool[num2].inc;
            if ((filter == 0 || item == filter) && (item == needs[0] || item == needs[1] || item == needs[2] || item == needs[3] || item == needs[4] || item == needs[5]))
            {
                Array.Clear(buffer, i - 4, 10);
                int num3 = i + 5 + 1;
                if (updateLen < num3)
                {
                    updateLen = num3;
                }
                cargoContainer.RemoveCargo(num2);
                return item;
            }
            return 0;
        }
        return 0;
    }

    public void TryRemoveItemAtRear(int cargoId)
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            cargoContainer.RemoveCargo(cargoId);
            Array.Clear(buffer, num - 4, 10);
            int num2 = num + 5 + 1;
            if (updateLen < num2)
            {
                updateLen = num2;
            }
        }
    }

    public bool HasCargoAtRear()
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            return true;
        }
        return false;
    }

    public int TestBlankAtHead()
    {
        int num = 9;
        if (buffer[num] != 0)
        {
            return -1;
        }
        if (bufferLength < 20)
        {
            return 0;
        }
        if (buffer[++num] != 0)
        {
            return 0;
        }
        if (buffer[++num] != 0)
        {
            return 1;
        }
        if (buffer[++num] != 0)
        {
            return 2;
        }
        if (buffer[++num] != 0)
        {
            return 3;
        }
        if (buffer[++num] != 0)
        {
            return 4;
        }
        if (buffer[++num] != 0)
        {
            return 5;
        }
        if (buffer[++num] != 0)
        {
            return 6;
        }
        if (buffer[++num] != 0)
        {
            return 7;
        }
        if (buffer[++num] != 0)
        {
            return 8;
        }
        if (buffer[++num] != 0)
        {
            return 9;
        }
        return 0;
    }

    public bool TestCargoAtRear()
    {
        return buffer[bufferLength - 5 - 1] == 250;
    }

    public int GetCargoIdAtRear()
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            return buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000;
        }
        return -1;
    }

    public int GetItemIdAtRear()
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            return cargoContainer.cargoPool[buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000].item;
        }
        return 0;
    }

    public int TryPickItemAtRear(int[] needs, out int needIdx, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
        needIdx = -1;
        if (buffer[bufferLength - 5 - 1] == 250)
        {
            int num = bufferLength - 5 - 1;
            int num2 = buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000;
            int item = cargoContainer.cargoPool[num2].item;
            stack = cargoContainer.cargoPool[num2].stack;
            inc = cargoContainer.cargoPool[num2].inc;
            if (item == needs[0])
            {
                Array.Clear(buffer, num - 4, 10);
                int num3 = num + 5 + 1;
                if (updateLen < num3)
                {
                    updateLen = num3;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 0;
                return item;
            }
            if (item == needs[1])
            {
                Array.Clear(buffer, num - 4, 10);
                int num4 = num + 5 + 1;
                if (updateLen < num4)
                {
                    updateLen = num4;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 1;
                return item;
            }
            if (item == needs[2])
            {
                Array.Clear(buffer, num - 4, 10);
                int num5 = num + 5 + 1;
                if (updateLen < num5)
                {
                    updateLen = num5;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 2;
                return item;
            }
            if (item == needs[3])
            {
                Array.Clear(buffer, num - 4, 10);
                int num6 = num + 5 + 1;
                if (updateLen < num6)
                {
                    updateLen = num6;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 3;
                return item;
            }
            if (item == needs[4])
            {
                Array.Clear(buffer, num - 4, 10);
                int num7 = num + 5 + 1;
                if (updateLen < num7)
                {
                    updateLen = num7;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 4;
                return item;
            }
            if (item == needs[5])
            {
                Array.Clear(buffer, num - 4, 10);
                int num8 = num + 5 + 1;
                if (updateLen < num8)
                {
                    updateLen = num8;
                }
                cargoContainer.RemoveCargo(num2);
                needIdx = 5;
                return item;
            }
        }
        return 0;
    }

    public bool CanPickItemFromRear(int[] needs)
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            int num2 = buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000;
            int item = cargoContainer.cargoPool[num2].item;
            if (item == 0)
            {
                return false;
            }
            if (item == needs[0] || item == needs[1] || item == needs[2] || item == needs[3] || item == needs[4] || item == needs[5])
            {
                return true;
            }
        }
        return false;
    }

    public int TryPickCargoAtEnd()
    {
        int num = bufferLength - 5 - 1;
        if (buffer[num] == 250)
        {
            int result = buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000;
            Array.Clear(buffer, num - 4, 10);
            int num2 = num + 5 + 1;
            if (updateLen < num2)
            {
                updateLen = num2;
            }
            return result;
        }
        return -1;
    }

    public int TryPickCargo(int index, int length)
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
        for (int i = index; i < num; i++)
        {
            if (buffer[i] >= 246)
            {
                i += 250 - buffer[i];
                int result = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
                Array.Clear(buffer, i - 4, 10);
                int num2 = i + 5 + 1;
                if (updateLen < num2)
                {
                    updateLen = num2;
                }
                return result;
            }
        }
        return -1;
    }

    public void RemoveCargoAcrossIndex(int index)
    {
        if (buffer[index] == 0 || buffer[index] == 246)
        {
            return;
        }
        int num = -1;
        for (int num2 = index; num2 >= 0; num2--)
        {
            if (buffer[num2] == 246)
            {
                num = num2;
                break;
            }
        }
        Assert.True(num >= 0);
        if (num >= 0)
        {
            int num3 = num + 4;
            int index2 = buffer[num3 + 1] - 1 + (buffer[num3 + 2] - 1) * 100 + (buffer[num3 + 3] - 1) * 10000 + (buffer[num3 + 4] - 1) * 1000000;
            cargoContainer.RemoveCargo(index2);
            Array.Clear(buffer, num, 10);
        }
    }

    public void RemoveCargosInSegment(int begin, int end)
    {
        if (end == 0)
        {
            end = bufferLength - 1;
        }
        if (end < begin)
        {
            end = begin;
        }
        if (buffer[begin] != 0)
        {
            for (int num = begin; num >= 0; num--)
            {
                if (buffer[num] == 246)
                {
                    begin = num;
                    break;
                }
            }
        }
        if (buffer[end] != 0)
        {
            for (int i = end; i < bufferLength; i++)
            {
                if (buffer[i] == byte.MaxValue)
                {
                    end = i;
                    break;
                }
            }
        }
        int num2 = begin;
        while (num2 <= end)
        {
            int num3 = 5;
            int num4 = 10;
            if (buffer[num2] == 0)
            {
                num2 += num3;
                continue;
            }
            if (buffer[num2] == 250)
            {
                int index = buffer[num2 + 1] - 1 + (buffer[num2 + 2] - 1) * 100 + (buffer[num2 + 3] - 1) * 10000 + (buffer[num2 + 4] - 1) * 1000000;
                cargoContainer.RemoveCargo(index);
                num2 += num4;
                continue;
            }
            if (246 <= buffer[num2] && buffer[num2] < 250)
            {
                num2 += 250 - buffer[num2];
                int index2 = buffer[num2 + 1] - 1 + (buffer[num2 + 2] - 1) * 100 + (buffer[num2 + 3] - 1) * 10000 + (buffer[num2 + 4] - 1) * 1000000;
                cargoContainer.RemoveCargo(index2);
                num2 += num4;
                continue;
            }
            Assert.CannotBeReached("断言失败：buffer数据有误");
            break;
        }
        Array.Clear(buffer, begin, end - begin + 1);
    }

    public bool GetCargoAtIndex(int index, out OptimizedCargo cargo, out int cargoId, out int offset)
    {
        cargo.stack = 1;
        cargo.inc = 0;
        cargo.item = 0;
        cargoId = -1;
        offset = -1;
        byte b = buffer[index];
        if (b == 0)
        {
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
                if (buffer[num2] == 250)
                {
                    num = num2;
                    break;
                }
            }
        }
        if (num >= 0 && buffer[num] == 250)
        {
            cargoId = buffer[num + 1] - 1 + (buffer[num + 2] - 1) * 100 + (buffer[num + 3] - 1) * 10000 + (buffer[num + 4] - 1) * 1000000;
            cargo = cargoContainer.cargoPool[cargoId];
            offset = index - num + 4;
            return true;
        }
        return false;
    }

    public int GetCargoIdAtIndex(int index, int length)
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
        for (int i = index; i < num; i++)
        {
            if (buffer[i] >= 246)
            {
                i += 250 - buffer[i];
                return buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
            }
        }
        return -1;
    }

    public void Update()
    {
        if (outputPath != null)
        {
            int num;
            if (outputPath.chunkCount == 1)
            {
                num = outputPath.chunks[2];
                outputChunk = 0;
            }
            else
            {
                int num2 = outputPath.chunkCount - 1;
                if (outputChunk > num2)
                {
                    outputChunk = num2;
                }
                int num3 = 0;
                while (true)
                {
                    if (outputIndex < outputPath.chunks[outputChunk * 3])
                    {
                        num2 = outputChunk - 1;
                        outputChunk = (num3 + num2) / 2;
                        continue;
                    }
                    if (outputIndex < outputPath.chunks[outputChunk * 3] + outputPath.chunks[outputChunk * 3 + 1])
                    {
                        break;
                    }
                    num3 = outputChunk + 1;
                    outputChunk = (num3 + num2) / 2;
                }
                num = outputPath.chunks[outputChunk * 3 + 2];
            }
            int num4 = bufferLength - 5 - 1;
            if (buffer[num4] == 250)
            {
                int cargoId = buffer[num4 + 1] - 1 + (buffer[num4 + 2] - 1) * 100 + (buffer[num4 + 3] - 1) * 10000 + (buffer[num4 + 4] - 1) * 1000000;
                if (closed)
                {
                    if (outputPath.TryInsertCargoNoSqueeze(outputIndex, cargoId))
                    {
                        Array.Clear(buffer, num4 - 4, 10);
                        updateLen = bufferLength;
                    }
                }
                else if (outputPath.TryInsertCargo((lastUpdateFrameOdd == outputPath.lastUpdateFrameOdd) ? outputIndex : ((outputIndex + num > outputPath.bufferLength - 6) ? (outputPath.bufferLength - 6) : (outputIndex + num)), cargoId))
                {
                    Array.Clear(buffer, num4 - 4, 10);
                    updateLen = bufferLength;
                }
            }
        }
        else if (bufferLength <= 10)
        {
            return;
        }
        lastUpdateFrameOdd = (GameMain.gameTick & 1) == 1;
        int num5 = updateLen - 1;
        while (num5 >= 0 && buffer[num5] != 0)
        {
            updateLen--;
            num5--;
        }
        if (updateLen == 0)
        {
            return;
        }
        int num6 = updateLen;
        for (int num7 = chunkCount - 1; num7 >= 0; num7--)
        {
            int num8 = chunks[num7 * 3];
            int num9 = chunks[num7 * 3 + 2];
            if (num8 < num6)
            {
                if (buffer[num8] != 0)
                {
                    for (int i = num8 - 5; i < num8 + 4; i++)
                    {
                        if (i >= 0 && buffer[i] == 250)
                        {
                            num8 = ((i >= num8) ? (i - 4) : (i + 5 + 1));
                            break;
                        }
                    }
                }
                int num10 = 0;
                while (num10 < num9)
                {
                    int num11 = num6 - num8;
                    if (num11 < 10)
                    {
                        num10 = ((num9 < num11) ? num9 : num11);
                        break;
                    }
                    int num12 = 0;
                    for (int j = 0; j < num9 - num10; j++)
                    {
                        int num13 = num6 - 1 - j;
                        if (buffer[num13] != 0)
                        {
                            break;
                        }
                        num12++;
                    }
                    if (num12 > 0)
                    {
                        Array.Copy(buffer, num8, buffer, num8 + num12, num11 - num12);
                        Array.Clear(buffer, num8, num12);
                        num10 += num12;
                    }
                    int num14 = num6 - 1;
                    while (num14 >= 0 && buffer[num14] != 0)
                    {
                        num6--;
                        num14--;
                    }
                }
                int num15 = num8 + ((num10 == 0) ? 1 : num10);
                if (num6 > num15)
                {
                    num6 = num15;
                }
            }
        }
    }
}
