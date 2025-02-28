using HarmonyLib;

namespace Weaver.Benchmarking;

public class GameHistoryDataBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.PrepareTick))]
    private static void PrepareTick_Prefix(GameHistoryData __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.PrepareTick))]
    private static void PrepareTick_Postfix(GameHistoryData __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameHistoryData)} {nameof(GameHistoryData.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.AfterTick))]
    private static void AfterTick_Prefix(GameHistoryData __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.AfterTick))]
    private static void AfterTick_Postfix(GameHistoryData __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameHistoryData)} {nameof(GameHistoryData.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}

