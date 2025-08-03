using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.NeedsSystem;

internal sealed class SubFactoryNeedsBuilder : IWholeNeedsBuilder
{
    private readonly List<short> _needsFlat = [];
    private readonly GroupNeeds[] _groupNeeds = new GroupNeeds[ArrayExtensions.GetEnumValuesEnumerable<EntityType>().Max(x => (int)x) + 1];

    public GroupNeedsBuilder CreateGroupNeedsBuilder(EntityType entityType)
    {
        return new GroupNeedsBuilder(this, entityType);
    }

    List<short> IWholeNeedsBuilder.GetNeedsFlat() => _needsFlat;

    void IWholeNeedsBuilder.CompletedGroup(EntityType entityType, GroupNeeds groupNeeds)
    {
        _groupNeeds[(int)entityType] = groupNeeds;
    }

    public SubFactoryNeeds Build()
    {
        return new SubFactoryNeeds(_groupNeeds, _needsFlat.ToArray());
    }
}
