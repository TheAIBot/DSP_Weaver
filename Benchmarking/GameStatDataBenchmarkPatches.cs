using HarmonyLib;

namespace Weaver.Benchmarking;

public class GameStatDataBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _prepareTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _gameTickTimes = new TimeThreadedIndexedCollectionStatistic(200);
    private static readonly TimeThreadedIndexedCollectionStatistic _afterTickTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.PrepareTick))]
    private static void Prepare_Prefix(GameStatData __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.PrepareTick))]
    private static void PrepareTick_Postfix(GameStatData __instance)
    {
        _prepareTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    private static void GameTick_Prefix(GameStatData __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    private static void GameTick_Postfix(GameStatData __instance)
    {
        _gameTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.AfterTick))]
    private static void AfterTick_Prefix(GameStatData __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.AfterTick))]
    private static void AfterTick_Postfix(GameStatData __instance)
    {
        _afterTickTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}