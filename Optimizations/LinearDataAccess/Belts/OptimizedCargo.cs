using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedCargo
{
    public short item;
    public byte stack;
    public byte inc;

    public OptimizedCargo(short item, byte stack, byte inc)
    {
        this.item = item;
        this.stack = stack;
        this.inc = inc;
    }
}
