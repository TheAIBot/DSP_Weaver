using System;

namespace Weaver.Optimizations.StaticData;

internal readonly struct ReadonlyArray<T>
    where T : IEquatable<T>
{
    private readonly T[] _array;

    public int Length => _array.Length;

    public ref readonly T this[int index] => ref _array[index];

    public ReadonlyArray(T[] array)
    {
        _array = array;
    }

    public bool SequenceEqual(ReadonlyArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (int i = 0; i < Length; i++)
        {
            if (!_array[i].Equals(other._array[i]))
            {
                return false;
            }
        }

        return true;
    }
}
