using HarmonyLib;

namespace Weaver.Benchmarking;

public class CustomChartsBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _gameTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.PrepareTick))]
    private static void Prepare_Prefix(CustomCharts __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.PrepareTick))]
    private static void PrepareTick_Postfix(CustomCharts __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(CustomCharts)} {nameof(CustomCharts.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static void GameTick_Prefix(CustomCharts __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static void GameTick_Postfix(CustomCharts __instance)
    {
        _gameTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(CustomCharts)} {nameof(CustomCharts.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.AfterTick))]
    private static void AfterTick_Prefix(CustomCharts __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.AfterTick))]
    private static void AfterTick_Postfix(CustomCharts __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogInfo($"{nameof(CustomCharts)} {nameof(CustomCharts.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}
