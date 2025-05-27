using System;

namespace Weaver.Optimizations.LinearDataAccess;

internal struct NetworkIdAndState<T> where T : Enum
{
    private const int IndexBitCount = 24;
    private const uint IndexBitMask = 0x00_ff_ff_ff;
    private const uint StateTypeMask = 0x00_00_00_ff;
    private uint _value;

    public int State
    {
        readonly get { return (int)(_value >> IndexBitCount); }
        set { _value = (_value & IndexBitMask) | ((uint)value << IndexBitCount); }
    }
    public readonly int Index => (int)(IndexBitMask & _value);

    public NetworkIdAndState(int state, int index)
    {
        if ((uint)state < 0 || (uint)state > StateTypeMask)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{state} was outside the range {0} to {StateTypeMask:N0}");
        }
        if (index < 0 || index > IndexBitMask)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} was outside the range {0} to {IndexBitMask:N0}");
        }

        _value = ((uint)state << IndexBitCount) | (IndexBitMask & (uint)index);
    }

    public override string ToString()
    {
        return $"""
            NetworkIdAndState<{typeof(T).Name}>
            \t{nameof(State)}: {Enum.GetName(typeof(T), State)}
            \t{nameof(Index)}: {Index:N0}
            """;
    }
}
