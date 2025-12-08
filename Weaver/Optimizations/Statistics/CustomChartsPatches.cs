using HarmonyLib;
using System.Threading.Tasks;
using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations.Statistics;

public class CustomChartsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomCharts), nameof(CustomCharts.GameTick))]
    private static bool GameTick_Parallelize(CustomCharts __instance)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = WeaverThreadHelper.GetParallelism()
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
