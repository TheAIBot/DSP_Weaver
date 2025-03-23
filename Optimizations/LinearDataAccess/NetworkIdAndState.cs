using System;

namespace Weaver.Optimizations.LinearDataAccess;

internal struct NetworkIdAndState<T> where T : Enum
{
    private uint _value;

    public int State
    {
        get { return (int)(_value >> 24); }
        set { _value = (_value & 0x00_ff_ff_ff) | ((uint)value << 24); }
    }
    public readonly int Index => (int)(0x00_ff_ff_ff & _value);

    public NetworkIdAndState(int state, int index)
    {
        _value = ((uint)state << 24) | (uint)index;
    }
}
