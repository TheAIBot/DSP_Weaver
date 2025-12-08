using System;

namespace Weaver.Optimizations.Inserters;

[Flags]
internal enum InserterState
{
    Active
        = 0b0000,
    Inactive
        = 0b1000,
    InactiveNoInserter
        = 0b0001 | Inactive,
    InactiveNotCompletelyConnected
        = 0b0010 | Inactive,
    InactivePickFrom
        = 0b0100 | Inactive,
    InactiveInsertInto
        = 0b0101 | Inactive
}
