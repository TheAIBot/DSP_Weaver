using HarmonyLib;

namespace Weaver.Benchmarking;

public class TrafficStatisticsBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _gameTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    private static void Prepare_Prefix(TrafficStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(TrafficStatistics __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static void GameTick_Prefix(TrafficStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static void GameTick_Postfix(TrafficStatistics __instance)
    {
        _gameTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.AfterTick))]
    private static void AfterTick_Prefix(TrafficStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.AfterTick))]
    private static void AfterTick_Postfix(TrafficStatistics __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}
