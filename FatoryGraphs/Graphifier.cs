using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.FatoryGraphs;

internal static class Graphifier
{
    const int minNodePerGraph = 20;
    const int maxCombinedGraphSize = 500;

    public static List<Graph> ToInserterGraphs(FactorySystem factorySystem)
    {
        HashSet<Node> nodes = new HashSet<Node>();
        Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode = new Dictionary<EntityTypeIndex, Node>();

        //WeaverFixes.Logger.LogInfo($"Cursor: {factorySystem.inserterCursor}");

        for (int i = 1; i < factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref factorySystem.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            var inserterNode = new Node(inserter.entityId, new EntityTypeIndex(EntityType.Inserter, i));
            nodes.Add(inserterNode);
            entityTypeIndexToNode.Add(inserterNode.EntityTypeIndex, inserterNode);

            if (inserter.pickTarget != 0)
            {
                EntityTypeIndex pickEntityTypeIndex = GetEntityTypeIndex(inserter.pickTarget, factorySystem);
                Node pickNode;
                if (!entityTypeIndexToNode.TryGetValue(pickEntityTypeIndex, out pickNode))
                {
                    pickNode = new Node(inserter.pickTarget, pickEntityTypeIndex);
                    nodes.Add(pickNode);
                    entityTypeIndexToNode.Add(pickEntityTypeIndex, pickNode);
                }

                inserterNode.ReceivingFrom.Add(pickNode);
                pickNode.SendingTo.Add(inserterNode);
            }

            if (inserter.insertTarget != 0)
            {
                EntityTypeIndex targetEntityTypeIndex = GetEntityTypeIndex(inserter.insertTarget, factorySystem);
                Node targetNode;
                if (!entityTypeIndexToNode.TryGetValue(targetEntityTypeIndex, out targetNode))
                {
                    targetNode = new Node(inserter.insertTarget, targetEntityTypeIndex);
                    nodes.Add(targetNode);
                    entityTypeIndexToNode.Add(targetEntityTypeIndex, targetNode);
                }

                inserterNode.SendingTo.Add(targetNode);
                targetNode.ReceivingFrom.Add(inserterNode);
            }
        }

        //WeaverFixes.Logger.LogInfo($"Nodes: {nodes.Count}");
        //WeaverFixes.Logger.LogInfo($"EntityIdToNode: {entityTypeIndexToNode.Count}");

        List<Graph> graphs = new List<Graph>();
        while (nodes.Count > 0)
        {
            Node firstNode = nodes.First();

            Queue<Node> toGoThrough = new Queue<Node>();
            HashSet<Node> seen = new HashSet<Node>();
            toGoThrough.Enqueue(firstNode);
            seen.Add(firstNode);
            nodes.Remove(firstNode);

            while (toGoThrough.Count > 0)
            {
                Node node = toGoThrough.Dequeue();

                foreach (var connectedNode in node.Nodes)
                {
                    if (!seen.Add(connectedNode))
                    {
                        continue;
                    }

                    toGoThrough.Enqueue(connectedNode);
                    nodes.Remove(connectedNode);
                }
            }

            Graph graph = new Graph();
            foreach (var node in seen)
            {
                //if (node.EntityTypeIndex.EntityType != EntityType.Inserter)
                //{
                //    continue;
                //}
                graph.AddNode(node);
            }

            graphs.Add(graph);
        }


        return graphs;
    }

    public static void SplitLargeGraphs(List<Graph> graphs)
    {
        List<Graph> splitGraphs = new List<Graph>();
        foreach (var largeGraph in graphs.Where(x => x.NodeCount > maxCombinedGraphSize))
        {
            foreach (Node[] nodeChunk in largeGraph.GetAllNodes()
                                                   .OrderBy(x => x.EntityTypeIndex.Index)
                                                   .Chunk(maxCombinedGraphSize))
            {
                Graph splitGraph = new Graph();
                foreach (Node node in nodeChunk)
                {
                    splitGraph.AddNode(node);
                }

                splitGraphs.Add(splitGraph);
            }
        }

        graphs.RemoveAll(x => x.NodeCount > maxCombinedGraphSize);
        graphs.AddRange(splitGraphs);
    }

    public static void CombineSmallGraphs(List<Graph> graphs)
    {
        List<Graph> combinedGraphs = new List<Graph>();
        foreach (Node[] nodeChunk in graphs.Where(x => x.NodeCount < minNodePerGraph)
                                           .SelectMany(x => x.GetAllNodes())
                                           .OrderBy(x => x.EntityTypeIndex.Index)
                                           .Chunk(maxCombinedGraphSize))
        {
            Graph smallerGraphsCombined = new Graph();
            foreach (Node node in nodeChunk)
            {
                smallerGraphsCombined.AddNode(node);
            }

            combinedGraphs.Add(smallerGraphsCombined);
        }

        graphs.RemoveAll(x => x.NodeCount < minNodePerGraph);
        graphs.AddRange(combinedGraphs);
    }

    public static EntityTypeIndex GetEntityTypeIndex(int index, FactorySystem factory)
    {
        ref readonly EntityData entity = ref factory.factory.entityPool[index];
        if (entity.beltId != 0)
        {
            return new EntityTypeIndex(EntityType.Belt, factory.traffic.GetCargoPath(factory.traffic.beltPool[entity.beltId].segPathId).id);
        }
        else if (entity.assemblerId != 0)
        {
            return new EntityTypeIndex(EntityType.Assembler, entity.assemblerId);
        }
        else if (entity.ejectorId != 0)
        {
            return new EntityTypeIndex(EntityType.Ejector, entity.ejectorId);
        }
        else if (entity.siloId != 0)
        {
            return new EntityTypeIndex(EntityType.Silo, entity.siloId);
        }
        else if (entity.labId != 0)
        {
            return new EntityTypeIndex(factory.labPool[entity.labId].researchMode ? EntityType.ResearchingLab : EntityType.ProducingLab, entity.labId);
        }
        else if (entity.storageId != 0)
        {
            return new EntityTypeIndex(EntityType.Storage, entity.storageId);
        }
        else if (entity.stationId != 0)
        {
            return new EntityTypeIndex(EntityType.Station, entity.stationId);
        }
        else if (entity.powerGenId != 0)
        {
            return new EntityTypeIndex(EntityType.PowerGenerator, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new EntityTypeIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new EntityTypeIndex(EntityType.Inserter, entity.inserterId);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}
