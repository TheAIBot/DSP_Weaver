using HarmonyLib;

namespace Weaver.Benchmarking;

public class PlayerPackageUtilityBenchmarkPatches
{
    private static readonly TimeThreadedIndexedCollectionStatistic _countTimes = new TimeThreadedIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.Count))]
    private static void Count_Prefix(PlayerPackageUtility __instance)
    {
        _countTimes.EnsureCapacity(1);
        _countTimes.StartThreadSampling();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.Count))]
    private static void Count_Postfix(PlayerPackageUtility __instance)
    {
        _countTimes.EndThreadSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(PlayerPackageUtility)} {nameof(PlayerPackageUtility.Count)} {_countTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}

