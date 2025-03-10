using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

internal sealed class Node
{
    public HashSet<Node> ReceivingFrom = [];
    public HashSet<Node> SendingTo = [];
    public IEnumerable<Node> Nodes => ReceivingFrom.Concat(SendingTo);
    public int EntityId { get; }
    public EntityTypeIndex EntityTypeIndex;

    public Node(int entityId, EntityTypeIndex entityTypeIndex)
    {
        EntityId = entityId;
        EntityTypeIndex = entityTypeIndex;
    }
}
