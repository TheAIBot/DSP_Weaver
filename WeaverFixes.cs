using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Weaver.Optimizations.LinearDataAccess;
using Weaver.Optimizations.Statistics;

namespace Weaver;

internal static class ModInfo
{
    public const string Guid = "Weaver";
    public const string Name = "Weaver";
    public const string Version = "1.0.1";
}

internal static class ModDependencies
{
    public const string SampleAndHoldSimId = "starfi5h.plugin.SampleAndHoldSim";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
[BepInDependency(ModDependencies.SampleAndHoldSimId, BepInDependency.DependencyFlags.SoftDependency)]
public class WeaverFixes : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(ModInfo.Name);

    private void Awake()
    {
        var harmony = new Harmony(ModInfo.Guid);
        // These changes parallelize calculating statistics
        harmony.PatchAll(typeof(ProductionStatisticsPatches));
        harmony.PatchAll(typeof(KillStatisticsPatches));
        harmony.PatchAll(typeof(TrafficStatisticsPatches));
        //harmony.PatchAll(typeof(CustomChartsPatches));



        // These are for benchmarking the code
        //harmony.PatchAll(typeof(GameHistoryDataBenchmarkPatches));
        //harmony.PatchAll(typeof(GameStatDataBenchmarkPatches));
        //harmony.PatchAll(typeof(PlayerPackageUtilityBenchmarkPatches));

        //harmony.PatchAll(typeof(ProductionStatisticsBenchmarkPatches));
        //harmony.PatchAll(typeof(KillStatisticsBenchmarkPatches));
        //harmony.PatchAll(typeof(TrafficStatisticsBenchmarkPatches));
        //harmony.PatchAll(typeof(CustomChartsBenchmarkPatches));


        // Optimizing the codes usage of object pools
        //harmony.PatchAll(typeof(ShrinkPools));

        //harmony.PatchAll(typeof(WorkerThreadExecutorBenchmarkPatches));
        //harmony.PatchAll(typeof(MultithreadSystemBenchmarkDisplayPatches));

        //InserterThreadLoadBalance.EnableOptimization(harmony);
        //InserterMultithreadingOptimization.EnableOptimization(harmony);

        // Causes various issues such as
        // * Incorrect production statistics
        // * Incorrect power statistics
        // * Assembler DivideByZeroException
        //LinearInserterDataAccessOptimization.EnableOptimization(harmony);


        OptimizedStarCluster.EnableOptimization(harmony);
        //GraphStatistics.Enable(harmony);
    }
}
