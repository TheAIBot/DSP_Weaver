using System.Collections.Generic;
using System.Linq;

namespace Weaver.FatoryGraphs;

internal sealed class Node
{
    public HashSet<Node> ReceivingFrom = [];
    public HashSet<Node> SendingTo = [];
    public IEnumerable<Node> Nodes => ReceivingFrom.Concat(SendingTo);
    public EntityTypeIndex EntityTypeIndex;

    public Node(EntityTypeIndex entityTypeIndex)
    {
        EntityTypeIndex = entityTypeIndex;
    }
}
