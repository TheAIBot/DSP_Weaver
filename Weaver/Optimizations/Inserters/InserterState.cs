using System;

namespace Weaver.Optimizations.Inserters;

[Flags]
internal enum InserterState : byte
{
    Active
        = 0b0000,
    Inactive
        = 0b0100,
    InactivePickFrom
        = 0b0001 | Inactive,
    InactiveInsertInto
        = 0b0010 | Inactive
}
