using HarmonyLib;

namespace Weaver.Benchmarking;

public class PlayerPackageUtilityBenchmarkPatches
{
    private static readonly TimeIndexedCollectionStatistic _countTimes = new TimeIndexedCollectionStatistic(200);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.Count))]
    private static void Count_Prefix(PlayerPackageUtility __instance)
    {
        _countTimes.EnsureCapacity(1);
        _countTimes.StartSampling(0);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.Count))]
    private static void Count_Postfix(PlayerPackageUtility __instance)
    {
        _countTimes.EndSampling(0);
        WeaverFixes.Logger.LogMessage($"{nameof(PlayerPackageUtility)} {nameof(PlayerPackageUtility.Count)} {_countTimes.GetAverageTimeInMilliseconds(0):N8}");
    }
}

