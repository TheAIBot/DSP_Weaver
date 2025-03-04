using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Weaver.Benchmarking;

namespace Weaver.Optimizations.LoadBalance;

internal static class Graphifier
{
    public static List<Graph> ToInserterGraphs(FactorySystem factorySystem)
    {
        HashSet<Node> nodes = new HashSet<Node>();
        Dictionary<EntityTypeIndex, Node> entityTypeIndexToNode = new Dictionary<EntityTypeIndex, Node>();

        //WeaverFixes.Logger.LogMessage($"Cursor: {factorySystem.inserterCursor}");

        for (int i = 1; i < factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref factorySystem.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            var inserterNode = new Node(inserter.entityId, new EntityTypeIndex(EntityType.Inserter, i));
            nodes.Add(inserterNode);
            entityTypeIndexToNode.Add(new EntityTypeIndex(EntityType.Inserter, i), inserterNode);

            if (inserter.pickTarget != 0)
            {
                EntityTypeIndex pickEntityTypeIndex = GetEntityTypeIndex(inserter.pickTarget, factorySystem.factory.entityPool);
                Node? pickNode;
                if (!entityTypeIndexToNode.TryGetValue(pickEntityTypeIndex, out pickNode))
                {
                    pickNode = new Node(inserter.pickTarget, pickEntityTypeIndex);
                    nodes.Add(pickNode);
                    entityTypeIndexToNode.Add(pickEntityTypeIndex, pickNode);
                }

                inserterNode.Nodes.Add(pickNode);
                pickNode.Nodes.Add(inserterNode);
            }

            if (inserter.insertTarget != 0)
            {
                EntityTypeIndex targetEntityTypeIndex = GetEntityTypeIndex(inserter.insertTarget, factorySystem.factory.entityPool);
                Node? targetNode;
                if (!entityTypeIndexToNode.TryGetValue(targetEntityTypeIndex, out targetNode))
                {
                    targetNode = new Node(inserter.insertTarget, targetEntityTypeIndex);
                    nodes.Add(targetNode);
                    entityTypeIndexToNode.Add(targetEntityTypeIndex, targetNode);
                }

                inserterNode.Nodes.Add(targetNode);
                targetNode.Nodes.Add(inserterNode);
            }
        }

        //WeaverFixes.Logger.LogMessage($"Nodes: {nodes.Count}");
        //WeaverFixes.Logger.LogMessage($"EntityIdToNode: {entityTypeIndexToNode.Count}");

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

    private static EntityTypeIndex GetEntityTypeIndex(int index, EntityData[] entities)
    {
        ref readonly EntityData entity = ref entities[index];
        if (entity.beltId != 0)
        {
            return new EntityTypeIndex(EntityType.Belt, index);
        }
        else if (entity.assemblerId != 0)
        {
            return new EntityTypeIndex(EntityType.Assembler, index);
        }
        else if (entity.ejectorId != 0)
        {
            return new EntityTypeIndex(EntityType.Ejector, index);
        }
        else if (entity.siloId != 0)
        {
            return new EntityTypeIndex(EntityType.Silo, index);
        }
        else if (entity.labId != 0)
        {
            return new EntityTypeIndex(EntityType.Lab, index);
        }
        else if (entity.storageId != 0)
        {
            return new EntityTypeIndex(EntityType.Storage, index);
        }
        else if (entity.stationId != 0)
        {
            return new EntityTypeIndex(EntityType.Station, index);
        }
        else if (entity.powerGenId != 0)
        {
            return new EntityTypeIndex(EntityType.PowerGenerator, index);
        }
        else if (entity.splitterId != 0)
        {
            return new EntityTypeIndex(EntityType.Splitter, index);
        }
        else if (entity.inserterId != 0)
        {
            return new EntityTypeIndex(EntityType.Inserter, index);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}

internal record struct EntityTypeIndex(EntityType EntityType, int Index);

interface IExecutableGraphAction
{
    void Execute(long time, PlanetFactory factory, int[] indexes);
}

internal struct InserterExecutableGraphAction : IExecutableGraphAction
{
    public void Execute(long time, PlanetFactory factory, int[] indexes)
    {
        bool isActive = factory.planet == GameMain.localPlanet;

        InserterComponent[] inserterPool = factory.factorySystem.inserterPool;
        CargoTraffic traffic = factory.factorySystem.traffic;
        PowerSystem powerSystem = factory.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = factory.entityAnimPool;
        int[][] entityNeeds = factory.entityNeeds;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = factory.entityPool;
        BeltComponent[] beltPool = factory.cargoTraffic.beltPool;
        byte b = (byte)GameMain.history.inserterStackCountObsolete;
        byte b2 = (byte)GameMain.history.inserterStackInput;
        byte stackOutput = (byte)GameMain.history.inserterStackOutput;
        bool inserterBidirectional = GameMain.history.inserterBidirectional;
        int delay = ((b > 1) ? 110000 : 0);
        int delay2 = ((b2 > 1) ? 40000 : 0);
        bool flag = time % 60 == 0;
        if (isActive)
        {
            foreach (int poolIndex in indexes)
            {
                ref InserterComponent reference = ref inserterPool[poolIndex];
                if (flag)
                {
                    reference.InternalOffsetCorrection(entityPool, traffic, beltPool);
                    if (reference.grade == 3)
                    {
                        reference.delay = delay;
                        reference.stackInput = b;
                        reference.stackOutput = 1;
                        reference.bidirectional = false;
                    }
                    else if (reference.grade == 4)
                    {
                        reference.delay = delay2;
                        reference.stackInput = b2;
                        reference.stackOutput = stackOutput;
                        reference.bidirectional = inserterBidirectional;
                    }
                    else
                    {
                        reference.delay = 0;
                        reference.stackInput = 1;
                        reference.stackOutput = 1;
                        reference.bidirectional = false;
                    }
                }
                float power = networkServes[consumerPool[reference.pcId].networkId];
                if (reference.bidirectional)
                {
                    reference.InternalUpdate_Bidirectional(factory, entityNeeds, entityAnimPool, power, isActive);
                }
                else
                {
                    reference.InternalUpdate(factory, entityNeeds, entityAnimPool, power);
                }
            }
            return;
        }
        foreach (int poolIndex in indexes)
        {
            ref InserterComponent reference2 = ref inserterPool[poolIndex];
            if (flag)
            {
                if (reference2.grade == 3)
                {
                    reference2.delay = delay;
                    reference2.stackInput = b;
                    reference2.stackOutput = 1;
                    reference2.bidirectional = false;
                }
                else if (reference2.grade == 4)
                {
                    reference2.delay = delay2;
                    reference2.stackInput = b2;
                    reference2.stackOutput = stackOutput;
                    reference2.bidirectional = inserterBidirectional;
                }
                else
                {
                    reference2.delay = 0;
                    reference2.stackInput = 1;
                    reference2.stackOutput = 1;
                    reference2.bidirectional = false;
                }
            }
            float power2 = networkServes[consumerPool[reference2.pcId].networkId];
            if (reference2.bidirectional)
            {
                reference2.InternalUpdate_Bidirectional(factory, entityNeeds, entityAnimPool, power2, isActive);
            }
            else
            {
                reference2.InternalUpdateNoAnim(factory, entityNeeds, power2);
            }
        }
    }
}

internal sealed class ExecutableGraph<TExecuteAction>
    where TExecuteAction : IExecutableGraphAction
{
    private readonly PlanetFactory _planet;
    private readonly Graph _graph;
    private readonly EntityType _entityType;
    private readonly TExecuteAction _action;
    private int[]? _indexes = null;

    public ExecutableGraph(PlanetFactory planet, Graph graph, EntityType entityType, TExecuteAction action)
    {
        _planet = planet;
        _graph = graph;
        _entityType = entityType;
        _action = action;
    }

    public void Prepare()
    {
        if (_indexes != null)
        {
            return;
        }

        _indexes = _graph.GetAllNodes()
                         .Where(x => x.EntityTypeIndex.EntityType == _entityType)
                         .Select(x => x.EntityTypeIndex.Index)
                         .OrderBy(x => x)
                         .ToArray();

        //WeaverFixes.Logger.LogMessage($"{_indexes.Length} indexes");
    }

    /// <summary>
    /// Original from FactorySystem.GameTickInserters
    /// </summary>
    public void Execute(long time)
    {
        if (_indexes == null)
        {
            throw new InvalidOperationException($"{nameof(_indexes)} is null.");
        }


        _action.Execute(time, _planet, _indexes);
    }
}

internal sealed class Graph
{
    private readonly Dictionary<int, Node> _idToNode = [];

    public void AddNode(Node node)
    {
        _idToNode.Add(node.EntityId, node);
    }

    public IEnumerable<Node> GetAllNodes() => _idToNode.Values;
}

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

internal enum EntityType
{
    Belt,
    Assembler,
    Ejector,
    Silo,
    Lab,
    Storage,
    Station,
    PowerGenerator,
    Splitter,
    Inserter
}

public class InserterMultithreadingOptimization
{
    private static readonly TimeIndexedCollectionStatistic _inserterTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly List<ExecutableGraph<InserterExecutableGraphAction>> _inserterExecutables = [];
    private static long? _gameTime;

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(InserterMultithreadingOptimization));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        _inserterExecutables.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            ;
            foreach (var graph in Graphifier.ToInserterGraphs(factory))
            {
                var executableGraph = new ExecutableGraph<InserterExecutableGraphAction>(planet, graph, EntityType.Inserter, new InserterExecutableGraphAction());
                _inserterExecutables.Add(executableGraph);
            }
        }

        WeaverFixes.Logger.LogMessage($"Created {_inserterExecutables.Count} executable graphs");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PrepareInserterData))]
    private static bool PrepareInserterData_Prefix(MultithreadSystem __instance, long _time)
    {
        _gameTime = _time;
        __instance.missionOrders |= (uint)MissionOrderType.Inserter;

        foreach (var executableGraph in _inserterExecutables)
        {
            executableGraph.Prepare();
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Schedule))]
    private static bool Schedule_Prefix(MultithreadSystem __instance)
    {
        return __instance.missionOrders == (uint)MissionOrderType.Inserter ? HarmonyConstants.SKIP_ORIGINAL_METHOD : HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Complete))]
    private static bool Complete_Prefix(MultithreadSystem __instance)
    {
        if (__instance.missionOrders != (uint)MissionOrderType.Inserter)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        if (_gameTime == null)
        {
            throw new InvalidOperationException($"{nameof(_gameTime)} is null.");
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        _inserterTickTimes.EnsureCapacity(1);
        _inserterTickTimes.StartSampling(0);
        Parallel.ForEach(_inserterExecutables, parallelOptions, static executableGraph => executableGraph.Execute(_gameTime.Value));
        _inserterTickTimes.EndSampling(0);

        WeaverFixes.Logger.LogMessage($"Inserter tick {_inserterTickTimes.GetAverageTimeInMilliseconds(0):N2}");

        __instance.isRevAllThreadCompleteSignal = true;
        __instance.missionOrders = 0u;

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}

internal static class ArrayExtensions
{
    public static int Sum(this int[] array)
    {
        int sum = 0;
        for (int i = 0; i < array.Length; i++)
        {
            sum += array[i];
        }

        return sum;
    }
}