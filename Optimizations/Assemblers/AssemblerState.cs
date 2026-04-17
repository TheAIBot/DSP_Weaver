using System;

namespace Weaver.Optimizations.Assemblers;

[Flags]
internal enum AssemblerState : byte
{
    Active
        = 0b0000,
    Inactive
        = 0b0100,
    InactiveOutputFull
        = 0b0001 | Inactive,
    InactiveInputMissing
        = 0b0010 | Inactive
}
