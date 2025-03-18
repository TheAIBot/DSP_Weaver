using HarmonyLib;
using System.Collections.Generic;

namespace Weaver.FatoryGraphs;

internal static class GraphValidation
{
    public static void Enable()
    {
        Harmony.CreateAndPatchAll(typeof(GraphValidation));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(GraphValidation)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            CargoTraffic cargoTraffic = planet.cargoTraffic;
            if (factory == null ||
                cargoTraffic == null)
            {
                continue;
            }

            List<Graph> graphs = Graphifier.ToInserterGraphs(factory);
        }
    }
}
