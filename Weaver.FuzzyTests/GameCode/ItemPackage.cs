namespace Weaver.FuzzyTests.GameCode;

public struct ItemPackage
{
    public byte stack;

    public short item;

    public int inc;

    public ItemPackage(byte stack, short item, int inc)
    {
        this.stack = stack;
        this.item = item;
        this.inc = inc;
    }
}
