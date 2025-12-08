using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Belts;

internal readonly struct BeltIndex : IEquatable<BeltIndex>, IMemorySize
{
    private const int NO_BELT_INDEX = -1;
    private readonly int _index;

    public bool HasValue => _index != NO_BELT_INDEX;

    public static readonly BeltIndex NoBelt = new BeltIndex(NO_BELT_INDEX);

    public BeltIndex(int index)
    {
        _index = index; 
    }

    public readonly ref OptimizedCargoPath GetBelt(OptimizedCargoPath[] belts)
    {
        return ref belts[_index];
    }

    public readonly int GetIndex()
    {
        if (!HasValue)
        {
            throw new InvalidOperationException("Attempted to get index of empty belt index.");
        }

        return _index; 
    }

    public readonly int GetSize() => Marshal.SizeOf<BeltIndex>();

    public static bool operator==(BeltIndex left, BeltIndex right)
    {
        return left._index == right._index;
    }

    public static bool operator !=(BeltIndex left, BeltIndex right)
    {
        return left._index != right._index;
    }

    public readonly bool Equals(BeltIndex other)
    {
        return this == other;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is BeltIndex other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_index);

        return hashCode.ToHashCode();
    }
}
