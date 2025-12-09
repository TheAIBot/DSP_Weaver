using System;

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
        int maxOffsetBeforeMove = offsetUpdatesPerMove * beltSpeed;
        var buffer = new byte[copyFrom.Length + maxOffsetBeforeMove];
        Array.Copy(copyFrom, 0, buffer, maxOffsetBeforeMove, copyFrom.Length);

        int stoppedItemsActualIndex = Array.LastIndexOf(buffer, (byte)0) + 1;

        var beltBuffer = new BeltBuffer(buffer, beltSpeed, 0, stoppedItemsActualIndex, maxOffsetBeforeMove);

        return beltBuffer;
    }

    public readonly byte GetBufferValue(int beltIndex)
    {
        return _buffer[GetActualIndex(beltIndex)];
    }

    public void SetBufferValue(int beltIndex, byte value)
    {
        int actualIndex = GetActualIndex(beltIndex);
        _buffer[actualIndex] = value;
        _updatedActualIndex = Math.Max(_updatedActualIndex, actualIndex);
    }

    public void Clear(int beltIndex, int length)
    {
        int actualIndex = GetActualIndex(beltIndex);
        Array.Clear(_buffer, actualIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, actualIndex + length - 1);
    }

    public void Copy(int sourceBeltIndex, int destinationBeltIndex, int length)
    {
        int actualSourceIndex = GetActualIndex(sourceBeltIndex);
        int actualDestinationIndex = GetActualIndex(destinationBeltIndex);
        Array.Copy(_buffer, actualSourceIndex, _buffer, actualDestinationIndex, length);
        _updatedActualIndex = Math.Max(_updatedActualIndex, Math.Max(actualSourceIndex, actualDestinationIndex) + length - 1);
    }

    public void Update()
    {
        UpdateStoppedItems();
        MoveItemsAndResetOffset();
    }

    private void UpdateStoppedItems()
    {
        if (_stoppedItemsActualIndex == _maxOffsetBeforeMove)
        {
            return;
        }

        // If any items have cleared in the stopped section then all items before that item
        // will start moving again. Move those items back to the moving section and update
        // the index of the last stopped item.
        if (_updatedActualIndex >= _stoppedItemsActualIndex)
        {
            Array.Copy(_buffer, _stoppedItemsActualIndex, _buffer, _stoppedItemsActualIndex - _offset, _updatedActualIndex - _stoppedItemsActualIndex + 1);
            Array.Clear(_buffer, _updatedActualIndex - _offset, _offset);
            _stoppedItemsActualIndex = _updatedActualIndex + 1;
        }

        _updatedActualIndex = -1;

        int endMovePosition = _stoppedItemsActualIndex - 1;
        int movedCount = 0;
        while (movedCount < _beltSpeed)
        {
            int freeSpacesFound = 0;
            for (int i = 0; i < endMovePosition; i++)
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

            int lengthToMove = 0;
            for (int i = 0; i < endMovePosition - freeSpacesFound; i++)
            {
                if (_buffer[endMovePosition - freeSpacesFound - i] == 0)
                {
                    break;
                }

                lengthToMove++;
            }

            if (lengthToMove == 0)
            {
                break;
            }
            // endMovePosition can be further back than _stoppedItemsActualIndex if partial moves are done.
            // This is because endMovePosition moves freeSpacesFound back on each partial move while 
            // _stoppedItemsActualIndex represents where the items should be moved to.
            Array.Copy(_buffer, endMovePosition - lengthToMove - freeSpacesFound + 1, _buffer, _stoppedItemsActualIndex - lengthToMove, lengthToMove);
            Array.Clear(_buffer, endMovePosition - lengthToMove - freeSpacesFound + 1, movedCount + freeSpacesFound);

            endMovePosition -= freeSpacesFound;
            endMovePosition -= lengthToMove;
            movedCount += freeSpacesFound;
            _stoppedItemsActualIndex -= lengthToMove;
        }
    }

    private void MoveItemsAndResetOffset()
    {
        if (_stoppedItemsActualIndex == _maxOffsetBeforeMove)
        {
            return;
        }

        _offset += _beltSpeed;

        if (_offset <= _maxOffsetBeforeMove)
        {
            return;
        }

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
