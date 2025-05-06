using System;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess;

internal readonly struct TypedObjectIndex
{
    private const int IndexBitCount = 24;
    private const uint IndexBitMask = 0x00_ff_ff_ff;
    private readonly uint _value;

    public readonly EntityType EntityType => (EntityType)(_value >> IndexBitCount);
    public readonly int Index => (int)(IndexBitMask & _value);

    public static readonly TypedObjectIndex Invalid = new(EntityType.None, (int)IndexBitMask);

    public TypedObjectIndex(EntityType entityType, int index)
    {
        if (index < 0 || index > IndexBitMask)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} was outside the range {0} to {IndexBitMask:N0}");
        }

        _value = ((uint)entityType << IndexBitCount) | (IndexBitMask & (uint)index);
    }
}
