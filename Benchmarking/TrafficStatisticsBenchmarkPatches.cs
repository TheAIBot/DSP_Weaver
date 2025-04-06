using HarmonyLib;

namespace Weaver.Benchmarking;

public class TrafficStatisticsBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _gameTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    private static void Prepare_Prefix(TrafficStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(TrafficStatistics __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static void GameTick_Prefix(TrafficStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static void GameTick_Postfix(TrafficStatistics __instance)
    {
        _gameTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.AfterTick))]
    private static void AfterTick_Prefix(TrafficStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.AfterTick))]
    private static void AfterTick_Postfix(TrafficStatistics __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(TrafficStatistics)} {nameof(TrafficStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}
