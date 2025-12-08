using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.StaticData;

internal sealed class CompareArrayCollections<T> : IEqualityComparer<IList<T>>
    where T : IEquatable<T>
{
    public bool Equals(IList<T> x, IList<T> y)
    {
        if (x.Count != y.Count)
        {
            return false;
        }

        for (int i = 0; i < x.Count; i++)
        {
            if (!x[i].Equals(y[i]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(IList<T> value)
    {
        var hashCode = new HashCode();
        hashCode.Add(value.Count);
        for (int i = 0; i < value.Count; i++)
        {
            hashCode.Add(value[i]);
        }

        return hashCode.ToHashCode();
    }
}
