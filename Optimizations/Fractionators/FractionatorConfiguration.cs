using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.Fractionators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal readonly struct FractionatorConfiguration : IEquatable<FractionatorConfiguration>
{
    public readonly bool IsOutput0;
    public readonly bool IsOutput1;
    public readonly bool IsOutput2;
    public readonly int FluidInputMax;
    public readonly int FluidOutputMax;
    public readonly int ProductOutputMax;

    public FractionatorConfiguration(bool isOutput0, 
                                     bool isOutput1, 
                                     bool isOutput2, 
                                     int fluidInputMax, 
                                     int fluidOutputMax, 
                                     int productOutputMax)
    {
        IsOutput0 = isOutput0;
        IsOutput1 = isOutput1;
        IsOutput2 = isOutput2;
        FluidInputMax = fluidInputMax;
        FluidOutputMax = fluidOutputMax;
        ProductOutputMax = productOutputMax;
    }

    public bool Equals(FractionatorConfiguration other)
    {
        return IsOutput0 == other.IsOutput0 &&
               IsOutput1 == other.IsOutput1 &&
               IsOutput2 == other.IsOutput2 &&
               FluidInputMax == other.FluidInputMax &&
               FluidOutputMax == other.FluidOutputMax &&
               ProductOutputMax == other.ProductOutputMax;
    }

    public override bool Equals(object obj)
    {
        return obj is FractionatorConfiguration configuration && Equals(configuration);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(IsOutput0);
        hashCode.Add(IsOutput1);
        hashCode.Add(IsOutput2);
        hashCode.Add(FluidInputMax);
        hashCode.Add(FluidOutputMax);
        hashCode.Add(ProductOutputMax);

        return hashCode.ToHashCode();
    }
}
