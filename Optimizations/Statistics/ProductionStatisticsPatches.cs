using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public class ProductionStatisticsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.PrepareTick))]
    private static bool PrepareTick_Parallelize(ProductionStatistics __instance)
    {
        //Logger.LogMessage("Did the thing! 1");

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

        // No idea if i can replace this with Parallel.ForEach
        Parallel.For(0, __instance.gameData.factoryCount, parallelOptions, i => __instance.factoryStatPool[i].PrepareTick());

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.GameTick))]
    private static bool GameTick_Parallelize(ProductionStatistics __instance, long time)
    {
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

        Parallel.For(0, __instance.gameData.factoryCount, i =>
        {
            __instance.factoryStatPool[i].GameTick(time);
            if (!__instance.factoryStatPool[i].itemChanged)
            {
                return;
            }

            try
            {
                __instance.RaiseActionEvent(nameof(ProductionStatistics.onItemChange));
            }
            catch (Exception message)
            {
                // Error from original game code
                WeaverFixes.Logger.LogError(message);
            }
        });
        __instance.extraInfoCalculator.GameTick();

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}
