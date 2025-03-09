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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            List<Graph> graphs = Graphifier.ToInserterGraphs(factory);
            if (graphs.Count == 0)
            {
                continue;
            }

            Graphifier.CombineSmallGraphs(graphs);

            InserterComponent[] oldInserters = factory.inserterPool;
            List<InserterComponent> newInserters = [];
            newInserters.Add(new InserterComponent() { id = 0 });

            foreach (Graph graph in graphs)
            {
                foreach (Node inserterNode in graph.GetAllNodes()
                                                   .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                                   .OrderBy(x => x.EntityId))
                {
                    InserterComponent inserterCopy = oldInserters[inserterNode.EntityTypeIndex.Index];
                    inserterCopy.id = newInserters.Count;
                    planet.entityPool[inserterCopy.entityId].inserterId = inserterCopy.id;
                    newInserters.Add(inserterCopy);
                }
            }

            factory.SetInserterCapacity(newInserters.Count);
            newInserters.CopyTo(factory.inserterPool);
            factory.inserterCursor = newInserters.Count;
        }
    }
}