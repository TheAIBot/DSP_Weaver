using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal readonly struct SubFactoryNeeds
{
    private readonly GroupNeeds[] _groupNeeds;
    public readonly short[] Needs;

    public SubFactoryNeeds(GroupNeeds[] groupNeeds, short[] needs)
    {
        _groupNeeds = groupNeeds;
        Needs = needs;
    }

    public readonly GroupNeeds GetGroupNeeds(EntityType entityType)
    {
        return _groupNeeds[(int)entityType];
    }

    public readonly int GetTypedObjectNeedsIndex(TypedObjectIndex typedObjectIndex)
    {
        return _groupNeeds[(int)typedObjectIndex.EntityType].GetObjectNeedsIndex(typedObjectIndex.Index);
    }
}
