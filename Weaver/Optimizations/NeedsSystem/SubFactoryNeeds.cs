using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal sealed class SubFactoryNeeds
{
    private readonly GroupNeeds[] _groupNeeds;
    public short[] NeedsPatterns { get; }
    public ComponentNeeds[] ComponentsNeeds { get; }

    public SubFactoryNeeds(GroupNeeds[] groupNeeds, short[] needsPatterns, ComponentNeeds[] componentsNeeds)
    {
        _groupNeeds = groupNeeds;
        NeedsPatterns = needsPatterns;
        ComponentsNeeds = componentsNeeds;
    }

    public GroupNeeds GetGroupNeeds(EntityType entityType)
    {
        return _groupNeeds[(int)entityType];
    }

    public int GetTypedObjectNeedsIndex(TypedObjectIndex typedObjectIndex)
    {
        return GetGroupNeeds(typedObjectIndex.EntityType).GetObjectNeedsIndex(typedObjectIndex.Index);
    }
}
