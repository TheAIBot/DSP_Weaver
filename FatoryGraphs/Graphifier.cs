using System;
using System.Collections.Generic;
using System.Linq;

namespace Weaver.FatoryGraphs;

internal static class Graphifier
{
    const int minNodePerGraph = 200;
    const int maxCombinedGraphSize = 1000;

    public static List<Graph> ToGraphs(PlanetFactory planet)
    {
        Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode = new Dictionary<EntityTypeIndex, Node>();
        AddInsertersToGraph(planet, entityTypeIndexToNode);
        AddAssemblersToGraph(planet, entityTypeIndexToNode);
        AddMonitorsToGraph(planet, entityTypeIndexToNode);
        AddSpraycoatersToGraph(planet, entityTypeIndexToNode);
        AddPilersToGraph(planet, entityTypeIndexToNode);
        AddMinersToGraph(planet, entityTypeIndexToNode);
        AddFractionatorsToGraph(planet, entityTypeIndexToNode);
        AddEjectorsToGraph(planet, entityTypeIndexToNode);
        AddSilosToGraph(planet, entityTypeIndexToNode);
        AddLabsToGraph(planet, entityTypeIndexToNode);
        AddStationsToGraph(planet, entityTypeIndexToNode);
        AddDispensersToGraph(planet, entityTypeIndexToNode);
        AddStoragesToGraph(planet, entityTypeIndexToNode);
        AddTanksToGraph(planet, entityTypeIndexToNode);
        AddSplittersToGraph(planet, entityTypeIndexToNode);
        AddBeltsToGraph(planet, entityTypeIndexToNode);

        HashSet<Node> nodes = new(entityTypeIndexToNode.Values);
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


        return graphs.OrderBy(GraphOrder).ToList();
    }

    public static void CombineSmallGraphs(List<Graph> graphs)
    {
        List<Graph> combinedGraphs = new List<Graph>();
        Graph? smallerGraphsCombined = null;
        foreach (Graph smallGraph in graphs.Where(x => x.NodeCount < maxCombinedGraphSize).OrderBy(GraphOrder))
        {
            smallerGraphsCombined ??= new Graph();

            foreach (Node node in smallGraph.GetAllNodes())
            {
                smallerGraphsCombined.AddNode(node);
            }

            if (smallerGraphsCombined.NodeCount >= maxCombinedGraphSize)
            {
                combinedGraphs.Add(smallerGraphsCombined);
                smallerGraphsCombined = null;
            }
        }

        if (smallerGraphsCombined != null)
        {
            combinedGraphs.Add(smallerGraphsCombined);
            smallerGraphsCombined = null;
        }

        graphs.RemoveAll(x => x.NodeCount < maxCombinedGraphSize);
        graphs.AddRange(combinedGraphs);

        var temp = graphs.ToList();
        graphs.Clear();
        graphs.AddRange(temp.OrderBy(GraphOrder));
    }

    private static int GraphOrder(Graph graph)
    {
        Node[] inserterNodes = graph.GetAllNodes().Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter).ToArray();
        if (inserterNodes.Length == 0)
        {
            return int.MaxValue;
        }

        return inserterNodes.Min(x => x.EntityTypeIndex.Index);
    }

    private static void AddInsertersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            var inserterNode = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Inserter, i));

            if (inserter.pickTarget != 0)
            {
                EntityTypeIndex pickEntityTypeIndex = GetEntityTypeIndex(inserter.pickTarget, planet.factorySystem);
                ConnectReceiveFrom(entityTypeIndexToNode, inserterNode, pickEntityTypeIndex);
            }

            if (inserter.insertTarget != 0)
            {
                EntityTypeIndex targetEntityTypeIndex = GetEntityTypeIndex(inserter.insertTarget, planet.factorySystem);
                ConnectSendTo(entityTypeIndexToNode, inserterNode, targetEntityTypeIndex);
            }
        }
    }

    private static void AddAssemblersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.assemblerCursor; i++)
        {
            ref AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Assembler, i));
        }
    }

    private static void AddMonitorsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.monitorCursor; i++)
        {
            ref readonly MonitorComponent component = ref planet.cargoTraffic.monitorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Monitor, i));

            if (component.targetBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.targetBeltId, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt);
            }
        }
    }

    private static void AddSpraycoatersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.spraycoaterCursor; i++)
        {
            ref readonly SpraycoaterComponent component = ref planet.cargoTraffic.spraycoaterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.SprayCoater, i));

            if (component.incBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.incBeltId, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt);
            }

            if (component.cargoBeltId > 0)
            {
                EntityTypeIndex cargoBelt = GetBeltSegmentTypeIndex(component.cargoBeltId, planet);
                ConnectSendTo(entityTypeIndexToNode, node, cargoBelt);
            }
        }
    }

    private static void AddPilersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.pilerCursor; i++)
        {
            ref readonly PilerComponent component = ref planet.cargoTraffic.pilerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Piler, i));

            if (component.inputBeltId > 0)
            {
                EntityTypeIndex incBelt = GetBeltSegmentTypeIndex(component.inputBeltId, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt);
            }

            if (component.outputBeltId > 0)
            {
                EntityTypeIndex cargoBelt = GetBeltSegmentTypeIndex(component.outputBeltId, planet);
                ConnectSendTo(entityTypeIndexToNode, node, cargoBelt);
            }
        }
    }

    private static void AddMinersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent component = ref planet.factorySystem.minerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Miner, i));

            if (component.insertTarget > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetEntityTypeIndex(component.insertTarget, planet.factorySystem);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }

            // It is weird but the miner entity contains the station id it sends its mined ore to.
            int minerStationId = planet.entityPool[component.entityId].stationId;
            if (minerStationId > 0)
            {
                EntityTypeIndex minerStation = new EntityTypeIndex(EntityType.Station, minerStationId);
                ConnectSendTo(entityTypeIndexToNode, node, minerStation);
            }

            // Veins will also connect things together since miners should not update
            // veins or vein groups in parallel.
            for (int veinIndex = 0; veinIndex < component.veinCount; veinIndex++)
            {
                int veinGroupIndex = planet.veinPool[component.veins[veinIndex]].groupIndex;
                EntityTypeIndex veinGroup = new EntityTypeIndex(EntityType.VeinGroup, veinGroupIndex);
                ConnectReceiveFrom(entityTypeIndexToNode, node, veinGroup);
            }
        }
    }

    private static void AddFractionatorsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.fractionatorCursor; i++)
        {
            ref readonly FractionatorComponent component = ref planet.factorySystem.fractionatorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Fractionator, i));

            if (component.belt0 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt0, planet);
                if (component.isOutput0)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt1 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt1, planet);
                if (component.isOutput1)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt2 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt2, planet);
                if (component.isOutput2)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddEjectorsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref readonly EjectorComponent component = ref planet.factorySystem.ejectorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Ejector, i));
        }
    }

    private static void AddSilosToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.siloCursor; i++)
        {
            ref readonly SiloComponent component = ref planet.factorySystem.siloPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Silo, i));
        }
    }

    private static void AddLabsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            ref readonly LabComponent component = ref planet.factorySystem.labPool[i];
            if (component.id != i || component.pcId == 0 || component.researchMode)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.ProducingLab, i));

            if (component.nextLabId > 0)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.ProducingLab, component.nextLabId);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }

        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            ref readonly LabComponent component = ref planet.factorySystem.labPool[i];
            if (component.id != i || component.pcId == 0 || !component.researchMode)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.ResearchingLab, i));

            if (component.nextLabId > 0)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.ResearchingLab, component.nextLabId);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void AddStationsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.transport.stationCursor; i++)
        {
            StationComponent component = planet.transport.stationPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Station, i));

            foreach (SlotData slot in component.slots)
            {
                if (slot.beltId == 0)
                {
                    continue;
                }

                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(slot.beltId, planet);
                if (slot.dir == IODir.Output)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else if (slot.dir == IODir.Input)
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddDispensersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.transport.dispenserCursor; i++)
        {
            DispenserComponent component = planet.transport.dispenserPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Dispenser, i));

            if (component.storage != null)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.Dispenser, component.storage.id);

                if (component.playerMode == EPlayerDeliveryMode.Supply ||
                    component.playerMode == EPlayerDeliveryMode.Both ||
                    component.storageMode == EStorageDeliveryMode.Supply)
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }

                if (component.playerMode == EPlayerDeliveryMode.Recycle ||
                    component.playerMode == EPlayerDeliveryMode.Both ||
                    component.storageMode == EStorageDeliveryMode.Demand)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddStoragesToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factoryStorage.storageCursor; i++)
        {
            StorageComponent component = planet.factoryStorage.storagePool[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Storage, i));

            if (component.previousStorage != null && component.previousStorage.id != 0)
            {
                EntityTypeIndex connectedStorage = new EntityTypeIndex(EntityType.Storage, component.previousStorage.id);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedStorage);
            }

            if (component.nextStorage != null && component.nextStorage.id != 0)
            {
                EntityTypeIndex connectedStorage = new EntityTypeIndex(EntityType.Storage, component.nextStorage.id);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedStorage);
            }
        }
    }

    private static void AddTanksToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.factoryStorage.tankCursor; i++)
        {
            TankComponent component = planet.factoryStorage.tankPool[i];
            if (component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Tank, i));

            if (component.lastTankId != 0 && planet.factoryStorage.tankPool[component.lastTankId].id == component.lastTankId)
            {
                EntityTypeIndex connectedStorage = new EntityTypeIndex(EntityType.Tank, component.lastTankId);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedStorage);
            }

            if (component.nextTankId != 0 && planet.factoryStorage.tankPool[component.nextTankId].id == component.nextTankId)
            {
                EntityTypeIndex connectedStorage = new EntityTypeIndex(EntityType.Tank, component.nextTankId);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedStorage);
            }

            if (component.belt0 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt0, planet);
                if (component.isOutput0)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt1 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt1, planet);
                if (component.isOutput1)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt2 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt2, planet);
                if (component.isOutput2)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
            if (component.belt3 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.belt3, planet);
                if (component.isOutput3)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
                }
            }
        }
    }

    private static void AddSplittersToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.splitterCursor; i++)
        {
            SplitterComponent component = planet.cargoTraffic.splitterPool[i];
            if (component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Splitter, i));

            if (component.input0 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.input0, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.input1 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.input1, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.input2 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.input2, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.input3 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.input3, planet);
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex);
            }

            if (component.output0 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.output0, planet);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.output1 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.output1, planet);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.output2 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.output2, planet);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
            if (component.output3 > 0)
            {
                EntityTypeIndex connectedEntityIndex = GetBeltSegmentTypeIndex(component.output3, planet);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void AddBeltsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.cargoTraffic.pathCursor; i++)
        {
            CargoPath component = planet.cargoTraffic.pathPool[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Belt, i));

            if (component.outputPath != null)
            {
                EntityTypeIndex connectedEntityIndex = new EntityTypeIndex(EntityType.Belt, component.outputPath.id);
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void BiDirectionConnection(Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, Node a, EntityTypeIndex b)
    {
        ConnectReceiveFrom(entityTypeIndexToNode, a, b);
        ConnectSendTo(entityTypeIndexToNode, a, b);
    }

    private static void ConnectReceiveFrom(Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, Node receiverNode, EntityTypeIndex senderTypeIndex)
    {
        Node senderNode = GetOrCreateNode(entityTypeIndexToNode, senderTypeIndex);
        receiverNode.ReceivingFrom.Add(senderNode);
        senderNode.SendingTo.Add(receiverNode);
    }

    private static void ConnectSendTo(Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, Node senderNode, EntityTypeIndex receiverTypeIndex)
    {
        Node receiverNode = GetOrCreateNode(entityTypeIndexToNode, receiverTypeIndex);
        senderNode.SendingTo.Add(receiverNode);
        receiverNode.ReceivingFrom.Add(senderNode);
    }

    private static Node GetOrCreateNode(Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode, EntityTypeIndex entityTypeIndex)
    {
        if (!entityTypeIndexToNode.TryGetValue(entityTypeIndex, out Node node))
        {
            node = new Node(entityTypeIndex);
            entityTypeIndexToNode.Add(entityTypeIndex, node);
        }

        return node;
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
