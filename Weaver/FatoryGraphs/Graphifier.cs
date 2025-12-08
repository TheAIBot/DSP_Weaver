using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        AddTurretsToGraph(planet, entityTypeIndexToNode);
        AddPowerExchangers(planet, entityTypeIndexToNode);
        AddPowerGenerators(planet, entityTypeIndexToNode);

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
                if (!TryGetEntityTypeIndex(inserter.pickTarget, planet.factorySystem, out EntityTypeIndex? pickEntityTypeIndex))
                {
                    entityTypeIndexToNode.Remove(inserterNode.EntityTypeIndex);
                    continue;
                }

                if (pickEntityTypeIndex.Value.EntityType != EntityType.FuelPowerGenerator)
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, inserterNode, pickEntityTypeIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, inserterNode, new EntityTypeIndex(pickEntityTypeIndex.Value.EntityType, inserter.pickOffset));
                }
            }

            if (inserter.insertTarget != 0)
            {
                if (!TryGetEntityTypeIndex(inserter.insertTarget, planet.factorySystem, out EntityTypeIndex? targetEntityTypeIndex))
                {
                    entityTypeIndexToNode.Remove(inserterNode.EntityTypeIndex);
                    continue;
                }
                ConnectSendTo(entityTypeIndexToNode, inserterNode, targetEntityTypeIndex.Value);
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

            if (TryGetBeltSegmentTypeIndex(component.targetBeltId, planet, out EntityTypeIndex? incBelt))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt.Value);
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

            if (TryGetBeltSegmentTypeIndex(component.incBeltId, planet, out EntityTypeIndex? incBelt))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt.Value);
            }

            if (TryGetBeltSegmentTypeIndex(component.cargoBeltId, planet, out EntityTypeIndex? cargoBelt))
            {
                ConnectSendTo(entityTypeIndexToNode, node, cargoBelt.Value);
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

            if (TryGetBeltSegmentTypeIndex(component.inputBeltId, planet, out EntityTypeIndex? incBelt))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, incBelt.Value);
            }

            if (TryGetBeltSegmentTypeIndex(component.outputBeltId, planet, out EntityTypeIndex? cargoBelt))
            {
                ConnectSendTo(entityTypeIndexToNode, node, cargoBelt.Value);
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
                if (!TryGetEntityTypeIndex(component.insertTarget, planet.factorySystem, out EntityTypeIndex? connectedEntityIndex))
                {
                    entityTypeIndexToNode.Remove(node.EntityTypeIndex);
                    continue;
                }
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
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

            if (TryGetBeltSegmentTypeIndex(component.belt0, planet, out EntityTypeIndex? connectedEntityIndex))
            {
                if (component.isOutput0)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt1, planet, out connectedEntityIndex))
            {
                if (component.isOutput1)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt2, planet, out connectedEntityIndex))
            {
                if (component.isOutput2)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
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
                BiDirectionConnection(entityTypeIndexToNode, node, connectedEntityIndex);
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
                BiDirectionConnection(entityTypeIndexToNode, node, connectedEntityIndex);
            }
        }
    }

    private static void AddStationsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.transport.stationCursor; i++)
        {
            StationComponent component = planet.transport.stationPool[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Station, i));

            foreach (SlotData slot in component.slots)
            {
                if (!TryGetBeltSegmentTypeIndex(slot.beltId, planet, out EntityTypeIndex? connectedEntityIndex))
                {
                    continue;
                }

                if (slot.dir == IODir.Output)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else if (slot.dir == IODir.Input)
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
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
                EntityTypeIndex connectedTank = new EntityTypeIndex(EntityType.Tank, component.lastTankId);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedTank);
            }

            if (component.nextTankId != 0 && planet.factoryStorage.tankPool[component.nextTankId].id == component.nextTankId)
            {
                EntityTypeIndex connectedTank = new EntityTypeIndex(EntityType.Tank, component.nextTankId);
                BiDirectionConnection(entityTypeIndexToNode, node, connectedTank);
            }

            if (TryGetBeltSegmentTypeIndex(component.belt0, planet, out EntityTypeIndex? connectedEntityIndex))
            {
                if (component.isOutput0)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt1, planet, out connectedEntityIndex))
            {
                if (component.isOutput1)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt2, planet, out connectedEntityIndex))
            {
                if (component.isOutput2)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt3, planet, out connectedEntityIndex))
            {
                if (component.isOutput3)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
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

            if (TryGetBeltSegmentTypeIndex(component.input0, planet, out EntityTypeIndex? connectedEntityIndex))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.input1, planet, out connectedEntityIndex))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.input2, planet, out connectedEntityIndex))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.input3, planet, out connectedEntityIndex))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }

            if (TryGetBeltSegmentTypeIndex(component.output0, planet, out connectedEntityIndex))
            {
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.output1, planet, out connectedEntityIndex))
            {
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.output2, planet, out connectedEntityIndex))
            {
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
            if (TryGetBeltSegmentTypeIndex(component.output3, planet, out connectedEntityIndex))
            {
                ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
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

    private static void AddTurretsToGraph(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.defenseSystem.turrets.cursor; i++)
        {
            ref readonly TurretComponent component = ref planet.defenseSystem.turrets.buffer[i];
            if (component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.Turret, i));

            if (TryGetBeltSegmentTypeIndex(component.targetBeltId, planet, out EntityTypeIndex? connectedEntityIndex))
            {
                ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
            }
        }
    }

    private static void AddPowerExchangers(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.powerSystem.excCursor; i++)
        {
            ref readonly PowerExchangerComponent component = ref planet.powerSystem.excPool[i];
            if (component.id != i)
            {
                continue;
            }

            var node = GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(EntityType.PowerExchanger, i));

            if (TryGetBeltSegmentTypeIndex(component.belt0, planet, out EntityTypeIndex? connectedEntityIndex))
            {
                if (component.isOutput0)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt1, planet, out connectedEntityIndex))
            {
                if (component.isOutput1)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt2, planet, out connectedEntityIndex))
            {
                if (component.isOutput2)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
            if (TryGetBeltSegmentTypeIndex(component.belt3, planet, out connectedEntityIndex))
            {
                if (component.isOutput3)
                {
                    ConnectSendTo(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
                else
                {
                    ConnectReceiveFrom(entityTypeIndexToNode, node, connectedEntityIndex.Value);
                }
            }
        }
    }

    private static void AddPowerGenerators(PlanetFactory planet, Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode)
    {
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            ref readonly PowerGeneratorComponent component = ref planet.powerSystem.genPool[i];
            if (component.id != i)
            {
                continue;
            }

            bool isFuelGenerator = !component.wind && !component.photovoltaic && !component.gamma && !component.geothermal;
            EntityType powerGeneratorType = isFuelGenerator ? EntityType.FuelPowerGenerator : EntityType.PowerGenerator;
            GetOrCreateNode(entityTypeIndexToNode, new EntityTypeIndex(powerGeneratorType, i));
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

    /// <summary>
    /// Not all mods seems to play nice with the data stored in <see cref="EntityData"/>
    /// so this here will return false if the data in EntityData is in an invalid state.
    /// This allows weaver to just ignore entities that are left in an invalid state.
    /// </summary>
    public static bool TryGetEntityTypeIndex(int index, FactorySystem factory, [NotNullWhen(true)] out EntityTypeIndex? entityTypeIndex)
    {
        ref readonly EntityData entity = ref factory.factory.entityPool[index];
        if (TryGetBeltSegmentTypeIndex(entity.beltId, factory.factory, out EntityTypeIndex? beltEntityTypeIndex))
        {
            entityTypeIndex = beltEntityTypeIndex.Value;
            return true;
        }
        else if (entity.assemblerId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Assembler, entity.assemblerId);
            return true;
        }
        else if (entity.ejectorId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Ejector, entity.ejectorId);
            return true;
        }
        else if (entity.siloId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Silo, entity.siloId);
            return true;
        }
        else if (entity.labId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(factory.labPool[entity.labId].researchMode ? EntityType.ResearchingLab : EntityType.ProducingLab, entity.labId);
            return true;
        }
        else if (entity.storageId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Storage, entity.storageId);
            return true;
        }
        else if (entity.stationId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Station, entity.stationId);
            return true;
        }
        else if (entity.powerGenId != 0)
        {
            ref readonly PowerGeneratorComponent component = ref factory.factory.powerSystem.genPool[entity.powerGenId];
            bool isFuelGenerator = !component.wind && !component.photovoltaic && !component.gamma && !component.geothermal;
            EntityType powerGeneratorType = isFuelGenerator ? EntityType.FuelPowerGenerator : EntityType.PowerGenerator;
            entityTypeIndex = new EntityTypeIndex(powerGeneratorType, entity.powerGenId);
            return true;
        }
        else if (entity.splitterId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Splitter, entity.splitterId);
            return true;
        }
        else if (entity.inserterId != 0)
        {
            entityTypeIndex = new EntityTypeIndex(EntityType.Inserter, entity.inserterId);
            return true;
        }

        entityTypeIndex = default;
        return false;
    }

    private static bool TryGetBeltSegmentTypeIndex(int beltId, PlanetFactory planet, [NotNullWhen(true)] out EntityTypeIndex? entityTypeIndex)
    {
        if (beltId <= 0)
        {
            entityTypeIndex = null;
            return false;
        }

        CargoPath? cargoPath = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[beltId].segPathId);
        if (cargoPath == null)
        {
            entityTypeIndex = null;
            return false;
        }

        entityTypeIndex = new EntityTypeIndex(EntityType.Belt, cargoPath.id);
        return true;
    }
}
