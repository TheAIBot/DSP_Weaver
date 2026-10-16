using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.StaticData;

internal readonly struct ReadonlyArray<T> : IEquatable<ReadonlyArray<T>>, IMemorySize
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

    public int GetSize()
    {
        const int arrayPointerSizeInBytes = 8;
        if (_array.Length == 0)
        {
            return arrayPointerSizeInBytes;
        }

        if (_array[0] is IMemorySize)
        {
            int size = arrayPointerSizeInBytes;
            for (int i = 0; i < _array.Length; i++)
            {
                size += ((IMemorySize)_array[i]).GetSize();
            }

            return size;
        }

        return arrayPointerSizeInBytes + Marshal.SizeOf<T>() * _array.Length;
    }

    public bool Equals(ReadonlyArray<T> other)
    {
        return SequenceEqual(other);
    }

    public override bool Equals(object obj)
    {
        return obj is ReadonlyArray<T> array && Equals(array);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        for (int i = 0; i < _array.Length; i++)
        {
            hashCode.Add(_array[i].GetHashCode());
        }

        return hashCode.ToHashCode();
    }
}
