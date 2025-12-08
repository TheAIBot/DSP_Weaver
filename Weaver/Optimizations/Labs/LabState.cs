using System;

namespace Weaver.Optimizations.Labs;

[Flags]
internal enum LabState : byte
{
    Active
        = 0b00000,
    Inactive
        = 0b00100,
    InactiveOutputFull
        = 0b00001 | Inactive,
    InactiveInputMissing
        = 0b00010 | Inactive,
}
