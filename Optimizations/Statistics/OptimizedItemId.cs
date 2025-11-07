using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.Statistics;

internal readonly struct OptimizedItemId : IEquatable<OptimizedItemId>
{
    public readonly short ItemIndex;
    public readonly short OptimizedItemIndex;

    public OptimizedItemId(int itemIndex, int optimizedItemIndex)
    {
        if (itemIndex > short.MaxValue || itemIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(itemIndex)} first in a short is not correct.");
        }
        if (optimizedItemIndex > short.MaxValue || optimizedItemIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(optimizedItemIndex)} first in a short is not correct.");
        }

        ItemIndex = (short)itemIndex;
        OptimizedItemIndex = (short)optimizedItemIndex;
    }

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
