using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public sealed class TrafficStatisticsPatches
{
    private static bool[]? _isStarUpdated = null;
    private static bool[]? _isPlanetUpdated = null;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    public static void PrepareTick(TrafficStatistics __instance, long time)
    {
        if (_isStarUpdated == null || __instance.starTrafficPool.Length != _isStarUpdated.Length)
        {
            _isStarUpdated = new bool[__instance.starTrafficPool.Length];
        }

        if (_isPlanetUpdated == null || __instance.factoryTrafficPool.Length != _isPlanetUpdated.Length)
        {
            _isPlanetUpdated = new bool[__instance.factoryTrafficPool.Length];
        }
    }

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

        Parallel.For(0, __instance.starTrafficPool.Length, parallelOptions, i =>
        {
            AstroTrafficStat traffic = __instance.starTrafficPool[i];
            if (traffic == null)
            {
                return;
            }
            traffic.GameTick(time);
            if (traffic.itemChanged)
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
        Parallel.For(0, __instance.factoryTrafficPool.Length, parallelOptions, i =>
        {
            AstroTrafficStat traffic = __instance.factoryTrafficPool[i];
            if (traffic == null)
            {
                return;
            }
            traffic.GameTick(time);
            if (traffic.itemChanged)
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
