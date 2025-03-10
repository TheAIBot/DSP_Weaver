using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

public class LinearInserterDataAccessOptimization
{
    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(LinearInserterDataAccessOptimization));
    }

    [HarmonyPriority(2)] // need to be executed before InserterMultithreadingOptimization
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            PowerSystem power = planet.powerSystem;
            if (factory == null || power == null)
            {
                continue;
            }

            CompactInserters(planet, factory);
            InserterLinearAccessToAssemblers(planet, factory);
            //InserterLinearAccessToPowerConsumers(planet, factory, planet.powerSystem);
        }
    }

    private static void CompactInserters(PlanetFactory planet, FactorySystem factory)
    {
        List<Graph> graphs = Graphifier.ToInserterGraphs(factory);
        if (graphs.Count == 0)
        {
            return;
        }

        Graphifier.CombineSmallGraphs(graphs);

        InserterComponent[] oldInserters = factory.inserterPool;
        List<InserterComponent> newInserters = [];
        newInserters.Add(new InserterComponent() { id = 0 });

        foreach (Graph graph in graphs)
        {
            foreach (Node inserterNode in graph.GetAllNodes()
                                               .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                               .OrderBy(x => (int)x.EntityTypeIndex.EntityType)
                                               .ThenBy(x => x.EntityId))
            {
                InserterComponent inserterCopy = oldInserters[inserterNode.EntityTypeIndex.Index];
                inserterCopy.id = newInserters.Count;
                planet.entityPool[inserterCopy.entityId].inserterId = inserterCopy.id;
                newInserters.Add(inserterCopy);
            }
        }

        factory.SetInserterCapacity(newInserters.Count);
        newInserters.CopyTo(factory.inserterPool);
        factory.inserterCursor = factory.inserterPool.Length;
        factory.inserterRecycleCursor = 0;

    }

    private static void InserterLinearAccessToAssemblers(PlanetFactory planet, FactorySystem factory)
    {
        List<Graph> graphs = Graphifier.ToInserterGraphs(factory);

        AssemblerComponent[] oldAssemblers = factory.assemblerPool;
        List<AssemblerComponent> newAssemblers = [];
        newAssemblers.Add(new AssemblerComponent() { id = 0 });
        HashSet<int> seenAssemblerIDs = [];

        foreach (var inserterNode in graphs.SelectMany(x => x.GetAllNodes())
                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                           .OrderBy(x => x.EntityTypeIndex.Index))
        {
            ref readonly InserterComponent inserter = ref factory.inserterPool[inserterNode.EntityTypeIndex.Index];
            if (inserter.pickTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.pickTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                if (!seenAssemblerIDs.Add(inserter.pickTarget))
                {
                    continue;
                }

                AssemblerComponent assemblerCopy = oldAssemblers[oldAssemblerId];
                assemblerCopy.id = newAssemblers.Count;
                planet.entityPool[inserter.pickTarget].assemblerId = assemblerCopy.id;
                newAssemblers.Add(assemblerCopy);
            }

            if (inserter.insertTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.insertTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                if (!seenAssemblerIDs.Add(inserter.insertTarget))
                {
                    continue;
                }

                AssemblerComponent assemblerCopy = oldAssemblers[oldAssemblerId];
                assemblerCopy.id = newAssemblers.Count;
                planet.entityPool[inserter.insertTarget].assemblerId = assemblerCopy.id;
                newAssemblers.Add(assemblerCopy);
            }
        }

        factory.SetAssemblerCapacity(newAssemblers.Count);
        newAssemblers.CopyTo(factory.assemblerPool);
        factory.assemblerCursor = factory.assemblerPool.Length;
        factory.assemblerRecycleCursor = 0;
    }

    private static void InserterLinearAccessToPowerConsumers(PlanetFactory planet, FactorySystem factory, PowerSystem power)
    {
        List<Graph> graphs = Graphifier.ToInserterGraphs(factory);

        HashSet<int> inserterPowerConsumerIndexes = new HashSet<int>(graphs.SelectMany(x => x.GetAllNodes())
                                                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                                                           .Select(x => factory.inserterPool[x.EntityTypeIndex.Index].pcId));

        PowerConsumerComponent[] oldPowerConsumers = power.consumerPool;
        List<PowerConsumerComponent> newPowerConsumers = [];
        newPowerConsumers.Add(new PowerConsumerComponent() { id = 0 });

        for (int i = 1; i < power.consumerCursor; i++)
        {
            if (inserterPowerConsumerIndexes.Contains(i))
            {
                newPowerConsumers.Add(default);
                continue;
            }

            newPowerConsumers.Add(oldPowerConsumers[i]);
        }

        Dictionary<int, int> oldToNewPowerConsumerIndex = [];
        foreach (int inserterPowerConsumerIndex in inserterPowerConsumerIndexes.OrderBy(x => x))
        {
            oldToNewPowerConsumerIndex.Add(inserterPowerConsumerIndex, newPowerConsumers.Count);

            PowerConsumerComponent powerConsumerCopy = oldPowerConsumers[inserterPowerConsumerIndex];
            powerConsumerCopy.id = newPowerConsumers.Count;
            planet.entityPool[powerConsumerCopy.entityId].powerConId = powerConsumerCopy.id;
            newPowerConsumers.Add(powerConsumerCopy);
        }

        power.SetConsumerCapacity(newPowerConsumers.Count);
        newPowerConsumers.CopyTo(power.consumerPool);
        power.consumerCursor = power.consumerPool.Length;
        power.consumerRecycleCursor = 0;

        for (int i = 1; i < factory.inserterPool.Length; i++)
        {
            ref InserterComponent inserter = ref factory.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            inserter.pcId = oldToNewPowerConsumerIndex[inserter.pcId];
        }

        for (int networkIndex = 1; networkIndex < power.netCursor; networkIndex++)
        {
            if (power.netPool[networkIndex] == null || power.netPool[networkIndex].id == 0)
            {
                continue;
            }

            PowerNetwork network = power.netPool[networkIndex];

            foreach (PowerNetworkStructures.Node node in network.nodes)
            {
                for (int i = 0; i < node.consumers.Count; i++)
                {
                    if (oldToNewPowerConsumerIndex.TryGetValue(node.consumers[i], out int newConsumerIndex))
                    {
                        node.consumers[i] = newConsumerIndex;
                    }
                }
                node.consumers.Sort();
            }

            for (int i = 0; i < network.consumers.Count; i++)
            {
                if (oldToNewPowerConsumerIndex.TryGetValue(network.consumers[i], out int newConsumerIndex))
                {
                    network.consumers[i] = newConsumerIndex;
                }
            }
            network.consumers.Sort();
        }
    }
}