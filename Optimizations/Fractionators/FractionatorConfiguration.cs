using System.Runtime.InteropServices;

namespace Weaver.Optimizations.Fractionators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal record struct FractionatorConfiguration(bool IsOutput0,
                                                 bool IsOutput1,
                                                 bool IsOutput2,
                                                 int FluidInputMax,
                                                 int FluidOutputMax,
                                                 int ProductOutputMax);
