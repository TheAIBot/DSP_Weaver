using System;
using System.Linq;

namespace Weaver.Optimizations.NeedsSystem;

internal readonly struct NeedsPattern : IEquatable<NeedsPattern>
{
    private readonly int[] _pattern;
    private readonly int _length;

    public readonly int[] Pattern => _pattern;

    public NeedsPattern(int[] pattern, int length)
    {
        _pattern = pattern;
        _length = length;
    }

    public readonly bool Equals(NeedsPattern other)
    {
        return _length == other._length &&
               _pattern.SequenceEqual(other._pattern);
    }

    public override readonly bool Equals(object obj)
    {
        return obj is NeedsPattern other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_length);
        for (int i = 0; i < _pattern.Length; i++)
        {
            hashCode.Add(_pattern[i]);
        }

        return hashCode.ToHashCode();
    }
}
