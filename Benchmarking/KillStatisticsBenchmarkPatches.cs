using HarmonyLib;

namespace Weaver.Benchmarking;

public class KillStatisticsBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _gameTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static void Prepare_Prefix(KillStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(KillStatistics __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(KillStatistics)} {nameof(KillStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static void GameTick_Prefix(KillStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static void GameTick_Postfix(KillStatistics __instance)
    {
        _gameTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(KillStatistics)} {nameof(KillStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.AfterTick))]
    private static void AfterTick_Prefix(KillStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.AfterTick))]
    private static void AfterTick_Postfix(KillStatistics __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(KillStatistics)} {nameof(KillStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}
