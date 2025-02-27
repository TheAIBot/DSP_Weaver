using HarmonyLib;

namespace Weaver.Benchmarking;

public class CustomChartsBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _gameTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.PrepareTick))]
    private static void Prepare_Prefix(CustomCharts __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.PrepareTick))]
    private static void PrepareTick_Postfix(CustomCharts __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(CustomCharts)} {nameof(CustomCharts.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static void GameTick_Prefix(CustomCharts __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static void GameTick_Postfix(CustomCharts __instance)
    {
        _gameTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(CustomCharts)} {nameof(CustomCharts.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.AfterTick))]
    private static void AfterTick_Prefix(CustomCharts __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.AfterTick))]
    private static void AfterTick_Postfix(CustomCharts __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(CustomCharts)} {nameof(CustomCharts.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}