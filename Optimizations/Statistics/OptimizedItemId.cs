using System;
using System.Runtime.InteropServices;
using Weaver.Extensions;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Statistics;

internal readonly struct OptimizedItemId : IEquatable<OptimizedItemId>, IMemorySize
{
    public readonly short ItemIndex;
    public readonly short OptimizedItemIndex;

    public OptimizedItemId(int itemIndex, int optimizedItemIndex)
    {
        ArgumentOutOfRangeException.ThrowIfOutsideZeroToShortMaxValue(itemIndex);
        ArgumentOutOfRangeException.ThrowIfOutsideZeroToShortMaxValue(optimizedItemIndex);

        ItemIndex = (short)itemIndex;
        OptimizedItemIndex = (short)optimizedItemIndex;
    }

    public unsafe int GetSize() => Marshal.SizeOf<OptimizedItemId>();

    public readonly bool Equals(OptimizedItemId other)
    {
        return ItemIndex == other.ItemIndex &&
               OptimizedItemIndex == other.OptimizedItemIndex;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is OptimizedItemId other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(ItemIndex, OptimizedItemIndex);
    }
}
