using HarmonyLib;

namespace Weaver.Benchmarking;

public class GameHistoryDataBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.PrepareTick))]
    private static void PrepareTick_Prefix(GameHistoryData __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.PrepareTick))]
    private static void PrepareTick_Postfix(GameHistoryData __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameHistoryData)} {nameof(GameHistoryData.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.AfterTick))]
    private static void AfterTick_Prefix(GameHistoryData __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.AfterTick))]
    private static void AfterTick_Postfix(GameHistoryData __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameHistoryData)} {nameof(GameHistoryData.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}

