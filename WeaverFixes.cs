using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Weaver.Optimizations.ObjectPools;
using Weaver.Optimizations.Statistics;

namespace Weaver;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class WeaverFixes : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_NAME);

    private void Awake()
    {
        // These changes parallelize calculating statistics
        Harmony.CreateAndPatchAll(typeof(ProductionStatisticsPatches));
        Harmony.CreateAndPatchAll(typeof(KillStatisticsPatches));
        //Harmony.CreateAndPatchAll(typeof(TrafficStatisticsPatches));
        //Harmony.CreateAndPatchAll(typeof(CustomChartsPatches));



        // These are for benchmarking the code
        //Harmony.CreateAndPatchAll(typeof(GameHistoryDataBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(GameStatDataBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(PlayerPackageUtilityBenchmarkPatches));

        //Harmony.CreateAndPatchAll(typeof(ProductionStatisticsBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(KillStatisticsBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(TrafficStatisticsBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(CustomChartsBenchmarkPatches));


        // Optimizing the codes usage of object pools
        Harmony.CreateAndPatchAll(typeof(ShrinkPools));


    }
}
