using System;

namespace Weaver.Optimizations.Statistics;

internal readonly struct OptimizedItemId
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

    public override readonly bool Equals(object obj)
    {
        if (obj is not OptimizedItemId other)
        {
            return false;
        }

        return ItemIndex == other.ItemIndex &&
               OptimizedItemIndex == other.OptimizedItemIndex;
    }

    public override readonly int GetHashCode()
    {
        var hasCode = new HashCode();
        hasCode.Add(ItemIndex);
        hasCode.Add(OptimizedItemIndex);

        return hasCode.ToHashCode();
    }
}
