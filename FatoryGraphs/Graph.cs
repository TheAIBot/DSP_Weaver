using System.Collections.Generic;

namespace Weaver.FatoryGraphs;

internal sealed class Graph
{
    private readonly Dictionary<EntityTypeIndex, Node> _entityTypeIndexToNode = [];

    public int NodeCount => _entityTypeIndexToNode.Count;

    public void AddNode(Node node)
    {
        _entityTypeIndexToNode.Add(node.EntityTypeIndex, node);
    }

    public IEnumerable<Node> GetAllNodes() => _entityTypeIndexToNode.Values;
}
