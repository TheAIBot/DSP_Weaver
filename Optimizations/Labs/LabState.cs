using System;

namespace Weaver.Optimizations.Labs;

[Flags]
internal enum LabState
{
    Active
        = 0b00000,
    Inactive
        = 0b01000,
    InactiveNoAssembler
        = 0b00001 | Inactive,
    InactiveNoRecipeSet
        = 0b00010 | Inactive,
    InactiveOutputFull
        = 0b00011 | Inactive,
    InactiveInputMissing
        = 0b00100 | Inactive,
    ResearchMode
        = 0b10000
}
