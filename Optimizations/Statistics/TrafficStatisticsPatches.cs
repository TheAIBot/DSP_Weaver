using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public class TrafficStatisticsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static bool GameTick_Parallelize(TrafficStatistics __instance, long time)
    {
        //Logger.LogMessage("Did the thing! 3");

        // Only enable parallelization if multithreading is enabled.
        // Not sure why one would disable it but hey lets just support it!
        if (!GameMain.multithreadSystem.multithreadSystemEnable)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        Parallel.ForEach(__instance.starTrafficPool, parallelOptions, x =>
        {
            if (x == null)
            {
                return;
            }
            x.GameTick(time);
            if (x.itemChanged)
            {
                try
                {
                    __instance.RaiseActionEvent(nameof(TrafficStatistics.onItemChange));
                }
                catch (Exception message)
                {
                    // Error from original game code
                    WeaverFixes.Logger.LogError(message);
                }
            }
        });
        Parallel.ForEach(__instance.factoryTrafficPool, parallelOptions, x =>
        {
            if (x == null)
            {
                return;
            }
            x.GameTick(time);
            if (x.itemChanged)
            {
                try
                {
                    __instance.RaiseActionEvent(nameof(TrafficStatistics.onItemChange));
                }
                catch (Exception message2)
                {
                    // Error from original game code
                    WeaverFixes.Logger.LogError(message2);
                }
            }
        });

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}
