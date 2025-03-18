using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace Weaver.FatoryGraphs;

internal static class GraphStatistics
{
    public static void Enable()
    {
        Harmony.CreateAndPatchAll(typeof(GraphStatistics));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(GraphStatistics)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            List<Graph> graphs = Graphifier.ToInserterGraphs(factory);

            WeaverFixes.Logger.LogMessage($"Name: {planet.planet.displayName}");
            WeaverFixes.Logger.LogMessage($"\tDistinct Graphs: {graphs.Count}");
            WeaverFixes.Logger.LogMessage($"\tGraph Sizes: Size, Count");
            foreach (IGrouping<int, Graph> item in graphs.GroupBy(x => x.NodeCount))
            {
                WeaverFixes.Logger.LogMessage($"\t\t{item.Key:N0}: {item.Count():N0}");
            }
        }
    }
}
