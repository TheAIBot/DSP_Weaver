using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Belts;

/*
 * Loosely inspired by this forum post
 * https://forums.factorio.com/viewtopic.php?p=241825#p241825
 * 
 * Example
 * 
 *
 * 
 * | Indexes | 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 |
 * | Values  | 0, 0, 0, x, 0, x, 0, x, x, x,  x |
 *                   ^              ^
 *                   |              |
 *      _maxOffsetBeforeMove        |
 *                                  |
 *                     _stoppedItemsActualIndex
 * 
 */
internal unsafe struct BeltBuffer
{
    private readonly byte* _buffer;
    private readonly int _beltSpeed;
    private readonly int _maxOffsetBeforeMove;
    private int _offset;
    private int _updatedActualIndex;
    private int _stoppedItemsActualIndex;

    private BeltBuffer(byte* buffer, int beltSpeed, int offset, int stoppedItemsIndex, int maxOffsetBeforeMove)
    {
        _buffer = buffer;
        _beltSpeed = beltSpeed;
        _offset = offset;
        _updatedActualIndex = 0;
        _stoppedItemsActualIndex = stoppedItemsIndex;
        _maxOffsetBeforeMove = maxOffsetBeforeMove;
    }

    public static BeltBuffer CreateFromExistingBuffer(byte[] copyFrom, int beltSpeed)
    {
        const int offsetUpdatesPerMove = 10;
        return CreateFromExistingBuffer(copyFrom, beltSpeed, offsetUpdatesPerMove);
    }

    public static BeltBuffer CreateFromExistingBuffer(byte[] copyFrom, int beltSpeed, int offsetUpdatesPerMove)
    {
        return CreateFromExistingBuffer(copyFrom, copyFrom.Length, beltSpeed, offsetUpdatesPerMove);
    }

    public static BeltBuffer CreateFromExistingBuffer(byte[] copyFrom, int bufferLength, int beltSpeed, int offsetUpdatesPerMove)
    {
        int maxOffsetBeforeMove = offsetUpdatesPerMove * beltSpeed;
        byte* buffer = (byte*)Marshal.AllocCoTaskMem(bufferLength + maxOffsetBeforeMove);
        Clear(buffer, 0, bufferLength + maxOffsetBeforeMove);
        for (int i = 0; i < bufferLength; i++)
        {
            *(buffer + i + maxOffsetBeforeMove) = copyFrom[i];
        }

        return new BeltBuffer(buffer, beltSpeed, 0, bufferLength + maxOffsetBeforeMove, maxOffsetBeforeMove);
    }

    public readonly byte GetBufferValue(int beltIndex)
    {
        int actualIndex = GetActualIndex(beltIndex);
        return GetBufferValueFromActualIndex(actualIndex);
    }

    public readonly byte GetBufferValue(int beltIndex, out int actualIndex)
    {
        actualIndex = GetActualIndex(beltIndex);
        return GetBufferValueFromActualIndex(actualIndex);
    }

    public readonly byte GetBufferValueFromActualIndex(int actualIndex)
    {
        //if (actualIndex < 0 || actualIndex >= _buffer.Length)
        //{
        //    throw new InvalidOperationException($"""
        //        Index out of range.
        //        Belt index: {beltIndex}
        //        Actual index: {actualIndex}
        //        Buffer length: {_buffer.Length}
        //        Belt Speed: {_beltSpeed}
        //        Max Offset: {_maxOffsetBeforeMove}
        //        Offset: {_offset}
        //        Updated actual index: {_updatedActualIndex}
        //        Stopped item actual index: {_stoppedItemsActualIndex}
        //        """);
        //}

        return _buffer[actualIndex];
    }

    public readonly bool TryGetCargo(int beltIndex, out OptimizedCargo optimizedCargo)
    {
        return TryGetCargo(beltIndex, out optimizedCargo, out _);
    }

    public readonly bool TryGetCargo(int beltIndex, out OptimizedCargo optimizedCargo, out int actualIndex)
    {
        byte* buffer = _buffer;
        actualIndex = GetActualIndex(beltIndex);
        if (buffer[actualIndex] != 250)
        {
            optimizedCargo = default;
            return false;
        }

        GetCargoFromActualIndex(actualIndex + 1, out optimizedCargo);
        return true;
    }

    public readonly int GetIndexOfNonZeroValue(int beltStartIndex, int length)
    {
        if (IsInStoppedRegion(beltStartIndex) == IsInStoppedRegion(beltStartIndex + length - 1))
        {
            byte* buffer = _buffer;
            int actualIndex = GetActualIndex(beltStartIndex);
            for (int i = 0; i < length; i++)
            {
                if (*(buffer + actualIndex + i) != 0)
                {
                    return beltStartIndex + i;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                if (GetBufferValue(beltStartIndex + i) != 0)
                {
                    return beltStartIndex + i;
                }
            }
        }

        return -1;
    }

    public readonly bool TryGetCargoWithinRange(int beltStartIndex, int length, out OptimizedCargo optimizedCargo, out int actualIndex)
    {
        if (IsInStoppedRegion(beltStartIndex) == IsInStoppedRegion(beltStartIndex + length - 1))
        {
            actualIndex = GetActualIndex(beltStartIndex);
            for (int i = 0; i < length; i++)
            {
                int bufferValue = GetBufferValueFromActualIndex(actualIndex + i);
                if (bufferValue >= 246)
                {
                    int offset = 250 - bufferValue;
                    actualIndex = actualIndex + i + offset;
                    GetCargoFromActualIndex(actualIndex + 1, out optimizedCargo);

                    return true;
                }
            }
        }
        else
        {
            for (int i = beltStartIndex; i < beltStartIndex + length; i++)
            {
                int bufferValue = GetBufferValue(i, out actualIndex);
                if (bufferValue >= 246)
                {
                    int offset = 250 - bufferValue;
                    actualIndex += offset;
                    GetCargoFromActualIndex(actualIndex + 1, out optimizedCargo);

                    return true;
                }
            }
        }

        optimizedCargo = default;
        actualIndex = -1;
        return false;
    }

    public readonly bool TryFindIndexOfFirstPreviousZeroValue(ref int index, ref int num, int num2)
    {
        int beltStartIndex = num2 + 1;
        int length = index - num2;
        if (IsInStoppedRegion(beltStartIndex) == IsInStoppedRegion(beltStartIndex + length))
        {
            int actualIndex = GetActualIndex(num);
            while (index > num2)
            {
                if (GetBufferValueFromActualIndex(actualIndex) != 0)
                {
                    index--;
                    num--;
                    actualIndex--;
                    continue;
                }
                return true;
            }
        }
        else
        {
            while (index > num2)
            {
                if (GetBufferValue(num) != 0)
                {
                    index--;
                    num--;
                    continue;
                }
                return true;
            }
        }

        return false;
    }

    public void SetBufferValue(int beltIndex, byte value)
    {
        int actualIndex = GetActualIndex(beltIndex);
        _buffer[actualIndex] = value;
        if (value > 0)
        {
            return;
        }

        _updatedActualIndex = Math.Max(_updatedActualIndex, actualIndex);
    }

    public readonly void SetCargo(int beltIndex, OptimizedCargo optimizedCargo)
    {
        int actualIndex = GetActualIndex(beltIndex);
        SetCargoFromActualIndex(actualIndex, optimizedCargo);
    }

    public readonly void SetCargoFromActualIndex(int actualIndex, OptimizedCargo optimizedCargo)
    {
        byte* buffer = _buffer + actualIndex;
        *(buffer + 0) = (byte)((optimizedCargo.Item & 0b0111_1111) + 1);
        *(buffer + 1) = (byte)((optimizedCargo.Item >> 7) + 1);
        *(buffer + 2) = (byte)(optimizedCargo.Stack + 1);
        *(buffer + 3) = (byte)(optimizedCargo.Inc + 1);
    }

    public readonly void SetCargoWithPadding(int beltIndex, OptimizedCargo optimizedCargo)
    {
        int actualIndex = GetActualIndex(beltIndex);
        SetCargoWithPaddingFromActualIndex(actualIndex, optimizedCargo.Item, optimizedCargo.Stack, optimizedCargo.Inc);
    }

    public readonly void SetCargoWithPadding(int beltIndex, int itemId, byte stack, byte inc)
    {
        int actualIndex = GetActualIndex(beltIndex);
        SetCargoWithPaddingFromActualIndex(actualIndex, itemId, stack, inc);
    }

    public readonly void SetCargoWithPaddingFromActualIndex(int actualIndex, int itemId, byte stack, byte inc)
    {
        byte* buffer = _buffer + actualIndex;
        *(buffer + 0) = 246;
        *(buffer + 1) = 247;
        *(buffer + 2) = 248;
        *(buffer + 3) = 249;
        *(buffer + 4) = 250;
        *(buffer + 5) = (byte)((itemId & 0b0111_1111) + 1);
        *(buffer + 6) = (byte)((itemId >> 7) + 1);
        *(buffer + 7) = (byte)(stack + 1);
        *(buffer + 8) = (byte)(inc + 1);
        *(buffer + 9) = byte.MaxValue;
    }

    public readonly void GetCargo(int beltIndex, out OptimizedCargo optimizedCargo)
    {
        int actualIndex = GetActualIndex(beltIndex);
        GetCargoFromActualIndex(actualIndex, out optimizedCargo);
    }

    public readonly void GetCargoFromActualIndex(int actualIndex, out OptimizedCargo optimizedCargo)
    {
        byte* buffer = _buffer + actualIndex;
        optimizedCargo = new OptimizedCargo((short)(*(buffer + 0) - 1 + (*(buffer + 1) - 1 << 7)),
                                            (byte)(*(buffer + 2) - 1),
                                            (byte)(*(buffer + 3) - 1));
    }

    public void Copy(int sourceBeltIndex, int destinationBeltIndex, int length)
    {
        int actualSourceIndex = GetActualIndex(sourceBeltIndex);
        int actualDestinationIndex = GetActualIndex(destinationBeltIndex);
        MemoryMove(_buffer, actualSourceIndex, _buffer, actualDestinationIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, Math.Max(actualSourceIndex, actualDestinationIndex) + length - 1);
    }

    public void Clear(int beltIndex, int length)
    {
        int actualIndex = GetActualIndex(beltIndex);
        ClearFromActualIndex(actualIndex, length);
    }

    public void ClearFromActualIndex(int actualIndex, int length)
    {
        Clear(_buffer, actualIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, actualIndex + length - 1);
    }

    public void Update()
    {

        UpdateStoppedItems();
        MoveItemsAndResetOffset();
    }

    public void Update(int chunkCount, ReadonlyArray<int> chunks)
    {
        MoveItemsOnBeltsWithLowerSpeed(chunkCount, chunks);
        UpdateStoppedItems();
        MoveItemsAndResetOffset();
    }

    public void Free()
    {
        Marshal.FreeCoTaskMem(new IntPtr(_buffer));
    }

    /// <summary>
    /// The speed of belts is not necessarily the same
    /// in a belt buffer. The speed of different belts are represented
    /// as belt chunks.
    /// Chunks are ordered from lowest to highest start index.
    /// Chunks can't overlap.
    /// 
    /// Changing the offset will "move" all items in the buffer at the
    /// same speed. This method moves items in chunks with a lower speed
    /// so items on those chunks move at the correct speed.
    /// </summary>
    private void MoveItemsOnBeltsWithLowerSpeed(int chunkCount, ReadonlyArray<int> chunks)
    {
        if (chunkCount == 1)
        {
            return;
        }

        /*
        Speed: 5,  5,  5,  5,  5,  5,  5,  2,  2,  2,  2
        Items: 0,  0,  0,  1,  1,  1,  0,  1,  1,  1,  0
        Index: 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10

        |
        Move items on slower belt back
        |
        v

        Speed: 5,  5,  2,  2,  2,  2,  2,  2,  2,  2,  2
        Items: 0,  1,  1,  1,  1,  1,  1,  0,  0,  0,  0
        Index: 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10

        |
        Update offset(Done in another method)
        |
        v

        Speed: 5,  5,  2,  2,  2,  2,  2,  2,  2,  2,  2
        Items: 0,  1,  1,  1,  1,  1,  1,  0,  0,  0,  0
        Index: 5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15
         */

        byte* buffer = _buffer;
        for (int i = 0; i < chunkCount; i++)
        {
            int chunkStartIndex = chunks[i * 3];
            int chunkLength = chunks[i * 3 + 1];
            int chunkSpeed = chunks[i * 3 + 2];

            if (chunkSpeed == _beltSpeed)
            {
                continue;
            }

            // No need to move items if all items on the belt are stopped
            int chunkStartActualIndex = chunkStartIndex + _maxOffsetBeforeMove - _offset;
            if (chunkStartActualIndex >= _stoppedItemsActualIndex)
            {
                break;
            }

            int speedDifference = _beltSpeed - chunkSpeed;
            int chunkEndActualIndex = chunkStartActualIndex + chunkLength - 1;
            // Stopped items are not moving forward and should therefore not be
            // moved back to compensate for the offset being too high.
            if (chunkEndActualIndex >= _stoppedItemsActualIndex)
            {
                chunkEndActualIndex = _stoppedItemsActualIndex - 1;
            }
            else
            {
                // An item may start at the end of this chunk and cross over onto the next chunk.
                // This here ensures that the entirety of the last items is moved.
                while (buffer[chunkEndActualIndex] != 0 && buffer[chunkEndActualIndex] != CargoPath.kCargoRear)
                {
                    chunkEndActualIndex++;
                }
            }
            int chunkUpdateLength = chunkEndActualIndex - chunkStartActualIndex + 1;

            int emptySpacesFound = 0;

            // Attempt to find the free spaces required to move the chunk back
            // by checking forward in the chunk.
            bool foundAllRequiredFreeSpaces = true;
            for (int x = 0; x < speedDifference; x++)
            {
                if (buffer[chunkStartActualIndex] != 0)
                {
                    foundAllRequiredFreeSpaces = false;
                    break;
                }

                chunkStartActualIndex++;
            }

            // If the free spaces could not be found by looking forward in the chunk then
            // the free spaces must be found in the previous spaces. If any items are found
            // before all free spaces are found then those items will have to be moved back
            // As well. If the items on the backwards belts are slower then they would've
            // been moved as well by this method so only items on a faster chunk than the
            // current chunk would be able to stay close to items in this chunk. Those
            // faster moving items will collide and be slowed down by the items moving in
            // the slow chunk. This is why it makes sense to move items in the backwards
            // belts.
            if (!foundAllRequiredFreeSpaces)
            {
                int startBackwardsSearchEmptySpacesActualIndex = chunkStartActualIndex - 1;
                int backwardsSearchActualIndex = startBackwardsSearchEmptySpacesActualIndex;

                int nonEmptySpacesFound = 0;
                while (backwardsSearchActualIndex >= 0)
                {
                    if (buffer[backwardsSearchActualIndex] != 0)
                    {
                        nonEmptySpacesFound++;
                        backwardsSearchActualIndex--;
                        continue;
                    }

                    emptySpacesFound++;
                    if (emptySpacesFound == speedDifference)
                    {
                        break;
                    }

                    backwardsSearchActualIndex--;
                }

                if (nonEmptySpacesFound > 0)
                {
                    int copyToActualIndex = backwardsSearchActualIndex;
                    int copyFromActualIndex = copyToActualIndex;
                    int targetIndex = startBackwardsSearchEmptySpacesActualIndex - emptySpacesFound;
                    while (copyToActualIndex < targetIndex)
                    {

                        while (buffer[copyFromActualIndex] == 0 && copyFromActualIndex <= startBackwardsSearchEmptySpacesActualIndex)
                        {
                            copyFromActualIndex++;
                        }

                        int copyLength = 0;
                        while (buffer[copyFromActualIndex + copyLength] != 0 && copyFromActualIndex + copyLength <= startBackwardsSearchEmptySpacesActualIndex)
                        {
                            copyLength++;
                        }

                        MemoryMove(buffer,
                                   copyFromActualIndex,
                                   buffer,
                                   copyToActualIndex,
                                   copyLength);
                        copyToActualIndex += copyLength;
                        copyFromActualIndex += copyLength;
                    }
                }
            }

            MemoryMove(buffer, chunkStartActualIndex, buffer, chunkStartActualIndex - emptySpacesFound, chunkUpdateLength);
            ClearFromActualIndex(chunkStartActualIndex - emptySpacesFound + chunkUpdateLength, emptySpacesFound);
        }
    }

    private void UpdateStoppedItems()
    {
        if (_stoppedItemsActualIndex <= _maxOffsetBeforeMove && _updatedActualIndex < _stoppedItemsActualIndex)
        {
            return;
        }

        // If any items have cleared in the stopped section then all items before that item
        // will start moving again. Move those items back to the moving section and update
        // the index of the last stopped item.
        if (_updatedActualIndex >= _stoppedItemsActualIndex)
        {
            MemoryMove(_buffer, _stoppedItemsActualIndex, _buffer, _stoppedItemsActualIndex - _offset, _updatedActualIndex - _stoppedItemsActualIndex + 1);
            Clear(_buffer, _updatedActualIndex - _offset + 1, _offset);
            _stoppedItemsActualIndex = _updatedActualIndex + 1;
        }

        _updatedActualIndex = -1;

        int endMovePosition = _stoppedItemsActualIndex - _offset - 1;
        int movedCount = 0;
        while (movedCount < _beltSpeed)
        {
            int freeSpacesFound = 0;
            for (int i = 0; i <= endMovePosition; i++)
            {
                if (_buffer[endMovePosition - i] != 0)
                {
                    break;
                }

                freeSpacesFound++;

                if (freeSpacesFound + movedCount == _beltSpeed)
                {
                    break;
                }
            }

            int itemsToMoveCount = 0;
            for (int i = 0; i <= endMovePosition - freeSpacesFound; i++)
            {
                if (_buffer[endMovePosition - freeSpacesFound - i] == 0)
                {
                    break;
                }

                itemsToMoveCount++;
            }

            if (itemsToMoveCount == 0)
            {
                break;
            }
            // endMovePosition can be further back than _stoppedItemsActualIndex if partial moves are done.
            // This is because endMovePosition moves freeSpacesFound + itemsToMoveCount back on each partial move while 
            // _stoppedItemsActualIndex represents where the items should be moved to.
            MemoryMove(_buffer, endMovePosition - itemsToMoveCount - freeSpacesFound + 1, _buffer, _stoppedItemsActualIndex - itemsToMoveCount, itemsToMoveCount);

            endMovePosition -= freeSpacesFound;
            endMovePosition -= itemsToMoveCount;
            movedCount += freeSpacesFound;
            _stoppedItemsActualIndex -= itemsToMoveCount;
        }

        if (movedCount > 0)
        {
            Clear(_buffer, _stoppedItemsActualIndex - movedCount - 1, movedCount);
        }
    }

    private void MoveItemsAndResetOffset()
    {
        if (_stoppedItemsActualIndex <= _maxOffsetBeforeMove)
        {
            return;
        }

#if DEBUG
// This checks to too expensive to run in game
        for (int i = _stoppedItemsActualIndex - 1; i >= _stoppedItemsActualIndex - 1 - _offset; i--)
        {
            if (_buffer[i] != 0)
            {
                throw new InvalidOperationException($"{nameof(_offset)} number of items before {nameof(_stoppedItemsActualIndex)} were not empty after {nameof(UpdateStoppedItems)} had executed.");
            }
        }
#endif

        _offset += _beltSpeed;

        if (_offset <= _maxOffsetBeforeMove)
        {
            return;
        }

        // UpdateStoppedItems have cleared a space of _maxOffsetBeforeMove between _stoppedItemsActualIndex
        // and the nearest item
        MemoryMove(_buffer, 0, _buffer, _maxOffsetBeforeMove, _stoppedItemsActualIndex - _maxOffsetBeforeMove);
        Clear(_buffer, 0, _maxOffsetBeforeMove);

        // Items are moved to adjust for previous updates.
        // Offset still needs to be updated so items are moved for this update.
        _offset = _beltSpeed;
    }

    public readonly int GetActualIndex(int beltIndex)
    {
        if (IsInStoppedRegion(beltIndex))
        {
            return beltIndex + _maxOffsetBeforeMove;
        }
        else
        {
            return beltIndex + _maxOffsetBeforeMove - _offset;
        }
    }

    private readonly bool IsInStoppedRegion(int beltIndex) => beltIndex + _maxOffsetBeforeMove >= _stoppedItemsActualIndex;

    private static void MemoryMove(byte* source, int sourceOffset, byte* destination, int destinationOffset, int length)
    {
        if (length <= 0)
        {
            return;
        }

        Buffer.MemoryCopy(source + sourceOffset,
                          destination + destinationOffset,
                          length,
                          length);
        return;
    }

    private static void Clear(byte* data, int startIndex, int length)
    {
        data = data + startIndex;
        for (int i = 0; i < length; i++)
        {
            *(data + i) = 0;
        }
    }
}
