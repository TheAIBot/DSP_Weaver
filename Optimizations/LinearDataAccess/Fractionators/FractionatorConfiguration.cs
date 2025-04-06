using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

[StructLayout(LayoutKind.Auto)]
internal record struct FractionatorConfiguration(bool IsOutput0,
                                                 bool IsOutput1,
                                                 bool IsOutput2,
                                                 int FluidInputMax,
                                                 int FluidOutputMax,
                                                 int ProductOutputMax);
