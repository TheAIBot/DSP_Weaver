using HarmonyLib;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public class KillStatisticsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static bool PrepareTick_Parallelize(KillStatistics __instance)
    {
        //Logger.LogMessage("Did the thing! 2");

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

        Parallel.ForEach(__instance.starKillStatPool, parallelOptions, x => x?.PrepareTick());
        Parallel.ForEach(__instance.factoryKillStatPool, parallelOptions, x => x?.PrepareTick());
        __instance.mechaKillStat?.PrepareTick();

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static bool GameTick_Parallelize(KillStatistics __instance, long time)
    {
        //Logger.LogMessage("Did the thing! 2");

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

        Parallel.ForEach(__instance.starKillStatPool, parallelOptions, x => x?.GameTick(time));
        Parallel.ForEach(__instance.factoryKillStatPool, parallelOptions, x => x?.GameTick(time));
        __instance.mechaKillStat?.GameTick(time);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.AfterTick))]
    private static bool AfterTick_Parallelize(KillStatistics __instance)
    {
        //Logger.LogMessage("Did the thing! 2");

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

        Parallel.ForEach(__instance.starKillStatPool, parallelOptions, x => x?.AfterTick());
        Parallel.ForEach(__instance.factoryKillStatPool, parallelOptions, x => x?.AfterTick());
        __instance.mechaKillStat?.AfterTick();

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}
