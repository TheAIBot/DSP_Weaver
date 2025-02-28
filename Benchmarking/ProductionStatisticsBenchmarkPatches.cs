using HarmonyLib;

namespace Weaver.Benchmarking;

public class ProductionStatisticsBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _gameTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.PrepareTick))]
    private static void Prepare_Prefix(ProductionStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(ProductionStatistics __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.GameTick))]
    private static void GameTick_Prefix(ProductionStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.GameTick))]
    private static void GameTick_Postfix(ProductionStatistics __instance)
    {
        _gameTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.AfterTick))]
    private static void AfterTick_Prefix(ProductionStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.AfterTick))]
    private static void AfterTick_Postfix(ProductionStatistics __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}