﻿using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.FatoryGraphs;

internal static class Graphifier
{
    private const int NO_ENTITY_ID_YET = -1;
    const int minNodePerGraph = 20;
    const int maxCombinedGraphSize = 2000;

    public static List<Graph> ToGraphs(FactorySystem factorySystem)
    {
        HashSet<Node> nodes = new HashSet<Node>();
        Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode = new Dictionary<EntityTypeIndex, Node>();

        AddInsertersToGraph(factorySystem, nodes, entityTypeIndexToNode);
        AddAssemblersToGraph(factorySystem, nodes, entityTypeIndexToNode);
        AddMonitorsToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddSpraycoatersToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddPilersToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddMinersToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddFractionatorsToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddEjectorsToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddSilosToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddLabsToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddStationsToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);
        AddDispensersToGraph(factorySystem.factory, nodes, entityTypeIndexToNode);

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
                graph.AddNode(node);
            }

            graphs.Add(graph);
        }


        return graphs;
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

    private static void AddInsertersToGraph(FactorySystem factory, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < factory.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref factory.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            var inserterNode = new Node(inserter.entityId, new EntityTypeIndex(EntityType.Inserter, i));
            nodes.Add(inserterNode);
            entityTypeIndexToNode.Add(inserterNode.EntityTypeIndex, inserterNode);

            if (inserter.pickTarget != 0)
            {
                EntityTypeIndex pickEntityTypeIndex = GetEntityTypeIndex(inserter.pickTarget, factory);
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
                EntityTypeIndex targetEntityTypeIndex = GetEntityTypeIndex(inserter.insertTarget, factory);
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
    }

    private static void AddAssemblersToGraph(FactorySystem factory, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            ref AssemblerComponent assembler = ref factory.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            var node = new Node(assembler.entityId, new EntityTypeIndex(EntityType.Assembler, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);
        }
    }

    private static void AddMonitorsToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.monitorCursor; i++)
        {
            ref readonly MonitorComponent component = ref planet.cargoTraffic.monitorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Monitor, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.targetBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.targetBeltId, planet);
                ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, incBelt);
            }
        }
    }

    private static void AddSpraycoatersToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.spraycoaterCursor; i++)
        {
            ref readonly SpraycoaterComponent component = ref planet.cargoTraffic.spraycoaterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.SprayCoater, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.incBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.incBeltId, planet);
                ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, incBelt);
            }

            if (component.cargoBeltId > 0)
            {
                EntityTypeIndex cargoBelt = GetBeltSegmentTypeIndex(component.cargoBeltId, planet);
                ConnectSendTo(nodes, entityTypeIndexToNode, node, cargoBelt);
            }
        }
    }

    private static void AddPilersToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.pilerCursor; i++)
        {
            ref readonly PilerComponent component = ref planet.cargoTraffic.pilerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Piler, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.inputBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.inputBeltId, planet);
                ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, incBelt);
            }

            if (component.outputBeltId > 0)
            {
                EntityTypeIndex cargoBelt = GetBeltSegmentTypeIndex(component.outputBeltId, planet);
                ConnectSendTo(nodes, entityTypeIndexToNode, node, cargoBelt);
            }
        }
    }

    private static void AddMinersToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent component = ref planet.factorySystem.minerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Miner, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.insertTarget > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(component.insertTarget, planet.factorySystem);
                ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void AddFractionatorsToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.fractionatorCursor; i++)
        {
            ref readonly FractionatorComponent component = ref planet.factorySystem.fractionatorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Fractionator, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.belt0 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(component.belt0, planet.factorySystem);
                if (component.isOutput0)
                {
                    ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt1 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(component.belt1, planet.factorySystem);
                if (component.isOutput1)
                {
                    ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt2 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(component.belt2, planet.factorySystem);
                if (component.isOutput2)
                {
                    ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddEjectorsToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref readonly EjectorComponent component = ref planet.factorySystem.ejectorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Ejector, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);
        }
    }

    private static void AddSilosToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.siloCursor; i++)
        {
            ref readonly SiloComponent component = ref planet.factorySystem.siloPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Silo, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);
        }
    }

    private static void AddLabsToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            ref readonly LabComponent component = ref planet.factorySystem.labPool[i];
            if (component.id != i || component.pcId == 0 || component.researchMode)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.ProducingLab, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.nextLabId > 0)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.ProducingLab, component.nextLabId);
                ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }

        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            ref readonly LabComponent component = ref planet.factorySystem.labPool[i];
            if (component.id != i || component.pcId == 0 || !component.researchMode)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.ResearchingLab, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.nextLabId > 0)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.ResearchingLab, component.nextLabId);
                ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void AddStationsToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.transport.stationCursor; i++)
        {
            StationComponent component = planet.transport.stationPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Station, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            foreach (SlotData slot in component.slots)
            {
                if (slot.beltId == 0)
                {
                    continue;
                }

                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(slot.beltId, planet.factorySystem);
                if (slot.dir == IODir.Output)
                {
                    ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else if (slot.dir == IODir.Input)
                {
                    ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddDispensersToGraph(PlanetFactory planet, HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.transport.dispenserCursor; i++)
        {
            DispenserComponent component = planet.transport.dispenserPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = new Node(component.entityId, new EntityTypeIndex(EntityType.Dispenser, i));
            nodes.Add(node);
            entityTypeIndexToNode.Add(node.EntityTypeIndex, node);

            if (component.storage != null)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.Dispenser, component.storage.id);

                if (component.playerMode == EPlayerDeliveryMode.Supply ||
                    component.playerMode == EPlayerDeliveryMode.Both ||
                    component.storageMode == EStorageDeliveryMode.Supply)
                {
                    ConnectReceiveFrom(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }

                if (component.playerMode == EPlayerDeliveryMode.Recycle ||
                    component.playerMode == EPlayerDeliveryMode.Both ||
                    component.storageMode == EStorageDeliveryMode.Demand)
                {
                    ConnectSendTo(nodes, entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void ConnectReceiveFrom(HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, Node receiverNode, EntityTypeIndex senderTypeIndex)
    {
        Node senderNode;
        if (!entityTypeIndexToNode.TryGetValue(senderTypeIndex, out senderNode))
        {
            senderNode = new Node(NO_ENTITY_ID_YET, senderTypeIndex);
            nodes.Add(senderNode);
            entityTypeIndexToNode.Add(senderTypeIndex, senderNode);
        }

        receiverNode.ReceivingFrom.Add(senderNode);
        senderNode.SendingTo.Add(receiverNode);
    }

    private static void ConnectSendTo(HashSet<Node> nodes, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, Node senderNode, EntityTypeIndex receiverTypeIndex)
    {
        Node receiverNode;
        if (!entityTypeIndexToNode.TryGetValue(receiverTypeIndex, out receiverNode))
        {
            receiverNode = new Node(NO_ENTITY_ID_YET, receiverTypeIndex);
            nodes.Add(receiverNode);
            entityTypeIndexToNode.Add(receiverTypeIndex, receiverNode);
        }

        senderNode.SendingTo.Add(receiverNode);
        receiverNode.ReceivingFrom.Add(senderNode);
    }

    public static EntityTypeIndex GetEntityTypeIndex(int index, FactorySystem factory)
    {
        ref readonly EntityData entity = ref factory.factory.entityPool[index];
        if (entity.beltId != 0)
        {
            return GetBeltSegmentTypeIndex(entity.beltId, factory.factory);
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

    private static EntityTypeIndex GetBeltSegmentTypeIndex(int beltId, PlanetFactory planet)
    {
        return new EntityTypeIndex(EntityType.Belt, planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[beltId].segPathId).id);
    }
}
