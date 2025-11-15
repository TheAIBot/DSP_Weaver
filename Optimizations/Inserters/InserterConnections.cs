using System;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.Inserters;

internal readonly struct InserterConnections : IEquatable<InserterConnections>, IMemorySize
{
    public readonly TypedObjectIndex PickFrom;
    public readonly TypedObjectIndex InsertInto;

    public InserterConnections(TypedObjectIndex pickFrom, TypedObjectIndex insertInto)
    {
        PickFrom = pickFrom;
        InsertInto = insertInto;
    }

    public int GetSize() => Marshal.SizeOf<InserterConnections>();

    public bool Equals(InserterConnections other)
    {
        return PickFrom == other.PickFrom &&
               InsertInto == other.InsertInto;
    }

    public override bool Equals(object obj)
    {
        return obj is InserterConnections other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PickFrom, InsertInto);
    }

    public override string ToString()
    {
        return $"""
            InserterConnections
            \t{nameof(PickFrom)}: {PickFrom}
            \t{nameof(InsertInto)}: {InsertInto}
            """;
    }
}
