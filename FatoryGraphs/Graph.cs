using System.Collections.Generic;

namespace Weaver.FatoryGraphs;

internal sealed class Graph
{
    private readonly Dictionary<int, Node> _idToNode = [];

    public int NodeCount => _idToNode.Count;

    public void AddNode(Node node)
    {
        _idToNode.Add(node.EntityId, node);
    }

    public IEnumerable<Node> GetAllNodes() => _idToNode.Values;
}
