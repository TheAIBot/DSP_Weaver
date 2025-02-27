using HarmonyLib;

namespace Weaver.Benchmarking;

public class ProductionStatisticsBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _gameTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.PrepareTick))]
    private static void Prepare_Prefix(ProductionStatistics __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.PrepareTick))]
    private static void PrepareTick_Postfix(ProductionStatistics __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.GameTick))]
    private static void GameTick_Prefix(ProductionStatistics __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.GameTick))]
    private static void GameTick_Postfix(ProductionStatistics __instance)
    {
        _gameTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.AfterTick))]
    private static void AfterTick_Prefix(ProductionStatistics __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.AfterTick))]
    private static void AfterTick_Postfix(ProductionStatistics __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(ProductionStatistics)} {nameof(ProductionStatistics.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}