using System.Collections.Generic;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

internal sealed class Node
{
    public HashSet<Node> Nodes { get; } = [];
    public int EntityId { get; }
    public EntityTypeIndex EntityTypeIndex;

    public Node(int entityId, EntityTypeIndex entityTypeIndex)
    {
        EntityId = entityId;
        EntityTypeIndex = entityTypeIndex;
    }
}
