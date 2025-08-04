using System;

namespace Weaver.Optimizations.Assemblers;

[Flags]
internal enum AssemblerState
{
    Active
        = 0b0000,
    Inactive
        = 0b1000,
    InactiveNoAssembler
        = 0b0001 | Inactive,
    InactiveNoRecipeSet
        = 0b0010 | Inactive,
    InactiveOutputFull
        = 0b0011 | Inactive,
    InactiveInputMissing
        = 0b0100 | Inactive
}
