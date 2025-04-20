using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Weaver.FatoryGraphs;

internal static class GraphStatistics
{
    public static void Enable(Harmony harmony)
    {
        harmony.PatchAll(typeof(GraphStatistics));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogInfo($"Initializing {nameof(GraphStatistics)}");

        List<Graph> allGraphs = [];

        Dictionary<int, int> gradeToCount = [];

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            List<Graph> graphs = Graphifier.ToGraphs(planet);
            allGraphs.AddRange(graphs);

            WeaverFixes.Logger.LogInfo($"Name: {planet.planet.displayName}");
            WeaverFixes.Logger.LogInfo($"\tDistinct Graphs: {graphs.Count}");
            WeaverFixes.Logger.LogInfo($"\tGraph Sizes: Size, Count");
            foreach (IGrouping<int, Graph> item in graphs.GroupBy(x => x.NodeCount))
            {
                WeaverFixes.Logger.LogInfo($"\t\t{item.Key:N0}: {item.Count():N0}");
            }

            for (int inserterIndex = 1; inserterIndex < factory.inserterCursor; inserterIndex++)
            {
                ref readonly InserterComponent inserter = ref factory.inserterPool[inserterIndex];
                if (inserter.id != inserterIndex)
                {
                    continue;
                }

                if (gradeToCount.TryGetValue(inserter.grade, out int count))
                {
                    gradeToCount[inserter.grade]++;
                }
                else
                {
                    gradeToCount.Add(inserter.grade, 1);
                }
            }
        }

        WeaverFixes.Logger.LogInfo($"Entity type counts");
        foreach (EntityType entityType in Enum.GetValues(typeof(EntityType)))
        {
            int entityTypeCount = allGraphs.SelectMany(x => x.GetAllNodes())
                                           .Where(x => x.EntityTypeIndex.EntityType == entityType)
                                           .Count();
            if (entityType == EntityType.Inserter)
            {
                WeaverFixes.Logger.LogInfo($"\t{entityType}: {entityTypeCount:N0}");
                foreach (KeyValuePair<int, int> inserterGradeCount in gradeToCount.OrderBy(x => x.Key))
                {
                    WeaverFixes.Logger.LogInfo($"\t\t{inserterGradeCount.Key}: {inserterGradeCount.Value:N0}");
                }
            }
            else
            {
                WeaverFixes.Logger.LogInfo($"\t{entityType}: {entityTypeCount:N0}");
            }
        }
    }
}
