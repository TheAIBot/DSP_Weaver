using System;
using System.Linq;

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
internal struct BeltBuffer
{
    private readonly byte[] _buffer;
    private readonly int _beltSpeed;
    private readonly int _maxOffsetBeforeMove;
    private int _offset;
    private int _updatedActualIndex;
    private int _stoppedItemsActualIndex;

    public readonly int Length => _buffer.Length - _maxOffsetBeforeMove;

    private BeltBuffer(byte[] buffer, int beltSpeed, int offset, int stoppedItemsIndex, int maxOffsetBeforeMove)
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
        return CreateFromExistingBuffer(copyFrom, copyFrom.Length, 1, beltSpeed, offsetUpdatesPerMove);
    }

    public static BeltBuffer CreateFromExistingBuffer(byte[] copyFrom, int bufferLength, int chunkCount, int beltSpeed)
    {
        if (chunkCount == 1)
        {
            const int minOffsetUpdatesPerMove = 10;
            const float maxBufferLengthRelativeToTotalBufferLength = 0.1f;
            int maxUpdatesForBeltLength = (int)(bufferLength * maxBufferLengthRelativeToTotalBufferLength) / beltSpeed;
            int offsetUpdatesPerMove = Math.Max(minOffsetUpdatesPerMove, maxUpdatesForBeltLength);

            int maxOffsetBeforeMove = offsetUpdatesPerMove * beltSpeed;
            byte[] buffer = new byte[bufferLength + maxOffsetBeforeMove];
            Array.Copy(copyFrom, 0, buffer, maxOffsetBeforeMove, bufferLength);

            return new BeltBuffer(buffer, beltSpeed, 0, buffer.Length, maxOffsetBeforeMove); ;
        }
        else
        {
            byte[] buffer = copyFrom.ToArray();

            return new BeltBuffer(buffer, 0, 0, buffer.Length, 0);
        }
    }

    public static BeltBuffer CreateFromExistingBuffer(byte[] copyFrom, int bufferLength, int chunkCount, int beltSpeed, int offsetUpdatesPerMove)
    {
        if (chunkCount == 1)
        {
            int maxOffsetBeforeMove = offsetUpdatesPerMove * beltSpeed;
            byte[] buffer = new byte[bufferLength + maxOffsetBeforeMove];
            Array.Copy(copyFrom, 0, buffer, maxOffsetBeforeMove, bufferLength);

            return new BeltBuffer(buffer, beltSpeed, 0, buffer.Length, maxOffsetBeforeMove); ;
        }
        else
        {
            byte[] buffer = copyFrom.ToArray();

            return new BeltBuffer(buffer, 0, 0, buffer.Length, 0);
        }
    }

    public readonly byte GetBufferValue(int beltIndex)
    {
        int actualIndex = GetActualIndex(beltIndex);
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

    public void SetCargo(int beltIndex, OptimizedCargo optimizedCargo)
    {
        int actualIndex = GetActualIndex(beltIndex + 3);
        _buffer[actualIndex - 3] = (byte)((optimizedCargo.Item & 0b0111_1111) + 1);
        _buffer[actualIndex - 2] = (byte)((optimizedCargo.Item >> 7) + 1);
        _buffer[actualIndex - 1] = (byte)(optimizedCargo.Stack + 1);
        _buffer[actualIndex - 0] = (byte)(optimizedCargo.Inc + 1);
    }

    public void SetCargoWithPadding(int beltIndex, OptimizedCargo optimizedCargo)
    {
        int actualIndex = GetActualIndex(beltIndex + 9);
        _buffer[actualIndex - 9] = 246;
        _buffer[actualIndex - 8] = 247;
        _buffer[actualIndex - 7] = 248;
        _buffer[actualIndex - 6] = 249;
        _buffer[actualIndex - 5] = 250;
        _buffer[actualIndex - 4] = (byte)((optimizedCargo.Item & 0b0111_1111) + 1);
        _buffer[actualIndex - 3] = (byte)((optimizedCargo.Item >> 7) + 1);
        _buffer[actualIndex - 2] = (byte)(optimizedCargo.Stack + 1);
        _buffer[actualIndex - 1] = (byte)(optimizedCargo.Inc + 1);
        _buffer[actualIndex - 0] = byte.MaxValue;
    }

    public OptimizedCargo GetCargo(int beltIndex)
    {
        int actualIndex = GetActualIndex(beltIndex + 3);
        return new OptimizedCargo((short)(_buffer[actualIndex - 3] - 1 + (_buffer[actualIndex - 2] - 1 << 7)),
                                  (byte)(_buffer[actualIndex - 1] - 1),
                                  (byte)(_buffer[actualIndex - 0] - 1));
    }

    public void Copy(int sourceBeltIndex, int destinationBeltIndex, int length)
    {
        int actualSourceIndex = GetActualIndex(sourceBeltIndex);
        int actualDestinationIndex = GetActualIndex(destinationBeltIndex);
        Array.Copy(_buffer, actualSourceIndex, _buffer, actualDestinationIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, Math.Max(actualSourceIndex, actualDestinationIndex) + length - 1);
    }

    public void Clear(int beltIndex, int length)
    {
        int actualIndex = GetActualIndex(beltIndex);
        Array.Clear(_buffer, actualIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, actualIndex + length - 1);
    }

    public void Update()
    {
        UpdateStoppedItems();
        MoveItemsAndResetOffset();
    }

    private void UpdateStoppedItems()
    {
        if (_stoppedItemsActualIndex == _maxOffsetBeforeMove && _updatedActualIndex < _stoppedItemsActualIndex)
        {
            return;
        }

        // If any items have cleared in the stopped section then all items before that item
        // will start moving again. Move those items back to the moving section and update
        // the index of the last stopped item.
        if (_updatedActualIndex >= _stoppedItemsActualIndex)
        {
            Array.Copy(_buffer, _stoppedItemsActualIndex, _buffer, _stoppedItemsActualIndex - _offset, _updatedActualIndex - _stoppedItemsActualIndex + 1);
            Array.Clear(_buffer, _updatedActualIndex - _offset + 1, _offset);
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
            Array.Copy(_buffer, endMovePosition - itemsToMoveCount - freeSpacesFound + 1, _buffer, _stoppedItemsActualIndex - itemsToMoveCount, itemsToMoveCount);
            Array.Clear(_buffer, endMovePosition - itemsToMoveCount - freeSpacesFound + 1, movedCount + freeSpacesFound + _offset);

            endMovePosition -= freeSpacesFound;
            endMovePosition -= itemsToMoveCount;
            movedCount += freeSpacesFound;
            _stoppedItemsActualIndex -= itemsToMoveCount;
        }
    }

    private void MoveItemsAndResetOffset()
    {
        if (_stoppedItemsActualIndex == _maxOffsetBeforeMove)
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
        Array.Copy(_buffer, 0, _buffer, _maxOffsetBeforeMove, _stoppedItemsActualIndex - _maxOffsetBeforeMove);
        Array.Clear(_buffer, 0, _maxOffsetBeforeMove);

        // Items are moved to adjust for previous updates.
        // Offset still needs to be updated so items are moved for this update.
        _offset = _beltSpeed;
    }

    private readonly int GetActualIndex(int beltIndex)
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
}
