using System.Collections.Generic;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal interface IWholeNeedsBuilder
{
    List<short> GetNeedsFlat();
    void CompletedGroup(EntityType entityType, GroupNeeds groupNeeds);
}
