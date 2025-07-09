using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedCargo
{
    private uint _value;

    private const uint _itemMask_ = 0b0000_0000_0000_0000_0011_1111_1111_1111;
    private const uint _stackMask = 0b0000_0000_0000_0011_1100_0000_0000_0000;
    private const uint _incMask__ = 0b0000_0000_1111_1100_0000_0000_0000_0000;
    private const int _itemOffset = 0;
    private const int _stackOffset = 14;
    private const int _incOffset = 18;

    public short Item
    {
        get
        {
            return (short)((_value >> _itemOffset) & (_itemMask_ >> _itemOffset));
        }
        set
        {
            _value = (_value & ~_itemMask_) | (uint)(value << _itemOffset);
        }
    }
    public byte Stack
    {
        get
        {
            return (byte)((_value >> _stackOffset) & (_stackMask >> _stackOffset));
        }
        set
        {
            _value = (_value & ~_stackMask) | (uint)(value << _stackOffset);
        }
    }
    public byte Inc
    {
        get
        {
            return (byte)((_value >> _incOffset) & (_incMask__ >> _incOffset));
        }
        set
        {
            _value = (_value & ~_incMask__) | (uint)(value << _incOffset);
        }
    }

    public OptimizedCargo(int value)
    {
        _value = (uint)value;
    }

    public OptimizedCargo(short item, byte stack, byte inc)
    {
        Item = item;
        Stack = stack;
        Inc = inc;
    }

    public static bool operator ==(OptimizedCargo a, OptimizedCargo b)
    {
        return a._value == b._value;
    }

    public static bool operator !=(OptimizedCargo a, OptimizedCargo b)
    {
        return a._value != b._value;
    }

    public int GetValue() => (int)_value;
}
