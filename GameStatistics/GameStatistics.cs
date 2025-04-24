using HarmonyLib;
using System.Runtime.InteropServices;

namespace Weaver.GameStatistics;

internal static class MemoryStatistics
{
    public static void EnableGameStatistics(Harmony harmony)
    {
        harmony.PatchAll(typeof(MemoryStatistics));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        long totalCargoCount = 0;
        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            totalCargoCount += factory.traffic.container.cargoPool.Length;
        }

        WeaverFixes.Logger.LogMessage($"Total cargo memory: {totalCargoCount * Marshal.SizeOf<Cargo>():N0}");
    }
}
