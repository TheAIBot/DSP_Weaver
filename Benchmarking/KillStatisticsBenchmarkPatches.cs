using HarmonyLib;

namespace Weaver.Benchmarking;

public class KillStatisticsBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _gameTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static void Prepare_Prefix(KillStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(KillStatistics __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(KillStatistics)} {nameof(KillStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static void GameTick_Prefix(KillStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static void GameTick_Postfix(KillStatistics __instance)
    {
        _gameTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(KillStatistics)} {nameof(KillStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.AfterTick))]
    private static void AfterTick_Prefix(KillStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.AfterTick))]
    private static void AfterTick_Postfix(KillStatistics __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(KillStatistics)} {nameof(KillStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}
