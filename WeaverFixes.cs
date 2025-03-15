using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Weaver.Optimizations.LoadBalance;
using Weaver.Optimizations.Statistics;

namespace Weaver;

internal static class ModInfo
{
    public const string Guid = "Weaver";
    public const string Name = "Weaver";
    public const string Version = "0.0.1";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
public class WeaverFixes : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(ModInfo.Name);

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
        //Harmony.CreateAndPatchAll(typeof(ShrinkPools));

        //Harmony.CreateAndPatchAll(typeof(WorkerThreadExecutorBenchmarkPatches));
        //Harmony.CreateAndPatchAll(typeof(MultithreadSystemBenchmarkDisplayPatches));

        //InserterThreadLoadBalance.EnableOptimization();
        //InserterMultithreadingOptimization.EnableOptimization();
        LinearInserterDataAccessOptimization.EnableOptimization();
        OptimizedInserters.EnableOptimization();
    }
}
