using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.StaticData;

internal sealed class ComparableArrayDeduplicator<T> : IComparableArrayDeduplicator
    where T : IEquatable<T>
{
    private readonly HashSet<IList<T>> _arrays = new(new CompareArrayCollections<T>());

    public int TotalBytes { get; private set; }
    public int BytesDeduplicated { get; private set; }

    public ReadonlyArray<T> Deduplicate(IList<T> toDeduplicate, int itemSize)
    {
        int deduplicateSize = itemSize * toDeduplicate.Count;
        TotalBytes += deduplicateSize;

        if (_arrays.TryGetValue(toDeduplicate, out IList<T> deduplicated))
        {
            BytesDeduplicated += deduplicateSize;
            return new ReadonlyArray<T>((T[])deduplicated);
        }

        T[] array;
        if (toDeduplicate is List<T> toDeduplicateList)
        {
            array = toDeduplicateList.ToArray();
        }
        else
        {
            array = (T[])toDeduplicate;
        }

        _arrays.Add(array);
        return new ReadonlyArray<T>(array);
    }

    public void Clear()
    {
        _arrays.Clear();
        TotalBytes = 0;
        BytesDeduplicated = 0;
    }
}
