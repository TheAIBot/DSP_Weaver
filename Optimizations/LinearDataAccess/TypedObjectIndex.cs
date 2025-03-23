using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess;

internal readonly struct TypedObjectIndex
{
    private readonly uint _value;

    public readonly EntityType EntityType => (EntityType)(_value >> 24);
    public readonly int Index => (int)(0x00_ff_ff_ff & _value);

    public TypedObjectIndex(EntityType entityType, int index)
    {
        _value = ((uint)entityType << 24) | (uint)index;
    }
}
