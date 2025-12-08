namespace Weaver.Optimizations.Belts;

internal struct OptimizedCargo
{
    public short Item;
    public byte Stack;
    public byte Inc;

    private const uint _itemMask_ = 0b0000_0000_0000_0000_0011_1111_1111_1111;
    private const uint _stackMask = 0b0000_0000_0000_0011_1100_0000_0000_0000;
    private const uint _incMask__ = 0b0000_0000_1111_1100_0000_0000_0000_0000;
    private const int _itemOffset = 0;
    private const int _stackOffset = 14;
    private const int _incOffset = 18;

    public OptimizedCargo(short item, byte stack, byte inc)
    {
        Item = item;
        Stack = stack;
        Inc = inc;
    }
}
