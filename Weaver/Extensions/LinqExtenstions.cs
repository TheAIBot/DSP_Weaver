using System;
using System.Collections.Generic;

namespace Weaver.Extensions;

internal static class LinqExtenstions
{
    public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> enumerable, int chunkSize)
    {
        List<T> chunk = new List<T>();
        IEnumerator<T> enumerator = enumerable.GetEnumerator();
        while (enumerator.MoveNext())
        {
            chunk.Add(enumerator.Current);
            if (chunk.Count == chunkSize)
            {
                yield return chunk.ToArray();
                chunk.Clear();
            }
        }

        if (chunk.Count != 0)
        {
            yield return chunk.ToArray();
        }
    }

    public static IEnumerable<T> GetEnumValuesEnumerable<T>()
    {
        foreach (T enumValue in Enum.GetValues(typeof(T)))
        {
            yield return enumValue;
        }
    }

    public static T MinBy<T>(this IEnumerable<T> enumerable, Func<T, int> comparatorSelector)
    {
        T? minItem = default;
        int? minValue = null;
        foreach (var item in enumerable)
        {
            int comparatorValue = comparatorSelector(item);
            if (minValue == null || comparatorValue < minValue.Value)
            {
                minItem = item;
                minValue = comparatorValue;
            }
        }

        if (!minValue.HasValue)
        {
            throw new InvalidOperationException("No data to enumerate.");
        }

        // minItem is not not when minValue is not null
        return minItem!;
    }
}