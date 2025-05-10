using HarmonyLib;
using System.Runtime.InteropServices;
using UnityEngine;

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
        long totalCargoMemoryUsage = 0;
        long totalCargoPathPositionsMemoryUsage = 0;
        long totalCargoPathRotationsMemoryUsage = 0;
        long totalBeltStructsMemoryUsage = 0;
        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            totalCargoMemoryUsage += factory.traffic.container.cargoPool.Length * Marshal.SizeOf<Cargo>();
            totalBeltStructsMemoryUsage += factory.traffic.beltPool.Length * Marshal.SizeOf<BeltComponent>();
            foreach (var cargoPath in factory.traffic.pathPool)
            {
                if (cargoPath == null)
                {
                    continue;
                }

                totalCargoPathPositionsMemoryUsage += cargoPath.pointPos.Length * Marshal.SizeOf<Vector3>();
                totalCargoPathRotationsMemoryUsage += cargoPath.pointRot.Length * Marshal.SizeOf<Quaternion>();
            }
        }

        long totalBeltMemoryUsage = totalCargoMemoryUsage + totalCargoPathPositionsMemoryUsage + totalCargoPathRotationsMemoryUsage + totalBeltStructsMemoryUsage;
        WeaverFixes.Logger.LogMessage($"Total cargo memory: {totalCargoMemoryUsage:N0}");
        WeaverFixes.Logger.LogMessage($"Total cargo path position memory: {totalCargoPathPositionsMemoryUsage:N0}");
        WeaverFixes.Logger.LogMessage($"Total cargo path rotation memory: {totalCargoPathRotationsMemoryUsage:N0}");
        WeaverFixes.Logger.LogMessage($"Total belt struct memory: {totalBeltStructsMemoryUsage:N0}");
        WeaverFixes.Logger.LogMessage($"Total belt memory: {totalBeltMemoryUsage:N0}");
    }
}
