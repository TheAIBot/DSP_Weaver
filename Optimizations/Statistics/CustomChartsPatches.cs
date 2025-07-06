using HarmonyLib;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public class CustomChartsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static bool GameTick_Parallelize(CustomCharts __instance)
    {
        // Only enable parallelization if multithreading is enabled.
        // Not sure why one would disable it but hey lets just support it!
        if (!GameMain.multithreadSystem.multithreadSystemEnable)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        StatPlan[] buffer = __instance.statPlans.buffer;
        int cursor = __instance.statPlans.cursor;
        Parallel.For(1, cursor, i =>
        {
            if (buffer[i] != null && buffer[i].id == i)
            {
                buffer[i].GameTick();
            }
        });

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}
