namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal readonly struct InserterConnections
{
    public readonly TypedObjectIndex PickFrom;
    public readonly TypedObjectIndex InsertInto;

    public InserterConnections(TypedObjectIndex pickFrom, TypedObjectIndex insertInto)
    {
        PickFrom = pickFrom;
        InsertInto = insertInto;
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
