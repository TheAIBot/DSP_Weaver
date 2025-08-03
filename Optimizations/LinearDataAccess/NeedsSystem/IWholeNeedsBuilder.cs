using System.Collections.Generic;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.NeedsSystem;

internal interface IWholeNeedsBuilder
{
    List<short> GetNeedsFlat();
    void CompletedGroup(EntityType entityType, GroupNeeds groupNeeds);
}
