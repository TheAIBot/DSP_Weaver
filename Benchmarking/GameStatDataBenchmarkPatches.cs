using HarmonyLib;

namespace Weaver.Benchmarking;

public class GameStatDataBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _prepareTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _gameTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly TimeIndexedCollectionStatistic _afterTickTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.PrepareTick))]
    private static void Prepare_Prefix(GameStatData __instance)
    {
        _prepareTickTimes.EnsureCapacity(1);
        _prepareTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.PrepareTick))]
    private static void PrepareTick_Postfix(GameStatData __instance)
    {
        _prepareTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.PrepareTick)} {_prepareTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    private static void GameTick_Prefix(GameStatData __instance)
    {
        _gameTickTimes.EnsureCapacity(1);
        _gameTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    private static void GameTick_Postfix(GameStatData __instance)
    {
        _gameTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.GameTick)} {_gameTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.AfterTick))]
    private static void AfterTick_Prefix(GameStatData __instance)
    {
        _afterTickTimes.EnsureCapacity(1);
        _afterTickTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.AfterTick))]
    private static void AfterTick_Postfix(GameStatData __instance)
    {
        _afterTickTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(GameStatData)} {nameof(GameStatData.AfterTick)} {_afterTickTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}