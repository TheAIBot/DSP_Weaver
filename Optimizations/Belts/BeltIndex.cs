using System;

namespace Weaver.Optimizations.Belts;

internal readonly struct BeltIndex
{
    private const int NO_BELT_INDEX = -1;
    private readonly int _index;

    public bool HasValue => _index != NO_BELT_INDEX;

    public static readonly BeltIndex NoBelt = new BeltIndex(NO_BELT_INDEX);

    public BeltIndex(int index)
    {
        _index = index; 
    }

    public ref OptimizedCargoPath GetBelt(OptimizedCargoPath[] belts)
    {
        return ref belts[_index];
    }

    public int GetIndex()
    {
        if (!HasValue)
        {
            throw new InvalidOperationException("Attempted to get index of empty belt index.");
        }

        return _index; 
    }

    public static bool operator==(BeltIndex left, BeltIndex right)
    {
        return left._index == right._index;
    }

    public static bool operator !=(BeltIndex left, BeltIndex right)
    {
        return left._index != right._index;
    }

    public override bool Equals(object obj)
    {
        return obj is BeltIndex belt && belt == this;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_index);

        return hashCode.ToHashCode();
    }
}
