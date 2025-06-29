using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Weaver.Optimizations.LinearDataAccess;
using Weaver.Optimizations.Statistics;

namespace Weaver;

internal static class ModInfo
{
    public const string Guid = "Weaver";
    public const string Name = "Weaver";
    public const string Version = "1.3.0";
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
    private static bool _enableDebugOptions = false;

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
        //OptimizedStarCluster.DebugEnableHeavyReOptimization();
        //GraphStatistics.Enable(harmony);
        //GameStatistics.MemoryStatistics.EnableGameStatistics(harmony);
    }

    private static bool _isOptimizeLocalPlanetKeyDown = false;
    private static bool _viewBeltsOnLocalOptimizedPlanet = false;
    private static bool _viewEntityIdsOnLocalPlanet = false;

    public void Update()
    {
        if (!_enableDebugOptions)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.V) && !_isOptimizeLocalPlanetKeyDown)
        {
            OptimizedStarCluster.ForceOptimizeLocalPlanet = !OptimizedStarCluster.ForceOptimizeLocalPlanet;
            _isOptimizeLocalPlanetKeyDown = true;
        }
        else
        {
            _isOptimizeLocalPlanetKeyDown = false;
        }

        if (Input.GetKeyDown(KeyCode.B) && !_viewBeltsOnLocalOptimizedPlanet)
        {
            OptimizedTerrestrialPlanet.ViewBeltsOnLocalOptimizedPlanet = !OptimizedTerrestrialPlanet.ViewBeltsOnLocalOptimizedPlanet;
            _viewBeltsOnLocalOptimizedPlanet = true;
        }
        else
        {
            _viewBeltsOnLocalOptimizedPlanet = false;
        }

        if (Input.GetKeyDown(KeyCode.F10) && !_viewEntityIdsOnLocalPlanet)
        {
            _viewEntityIdsOnLocalPlanet = true;
        }
        else
        {
            _viewEntityIdsOnLocalPlanet = false;
        }
    }

    // from TestConstructSystem
    private void OnGUI()
    {
        if (!_enableDebugOptions)
        {
            return;
        }

        GameData data = GameMain.data;
        if (data == null)
        {
            return;
        }
        PlanetFactory? planetFactory = ((data.localPlanet == null) ? null : data.localPlanet.factory);
        if (planetFactory == null)
        {
            return;
        }
        if (_viewEntityIdsOnLocalPlanet)
        {
            EntityData[] entityPool = planetFactory.entityPool;
            int entityCursor = planetFactory.entityCursor;
            for (int i = 1; i < entityCursor; i++)
            {
                if (entityPool[i].id == i)
                {
                    Vector3 vector = Camera.main.WorldToScreenPoint(entityPool[i].pos);
                    vector.y = (float)Screen.height - vector.y;
                    GUI.Label(new Rect(vector.x, vector.y - 50f, 270f, 450f), i.ToString());
                }
            }
        }
    }
}
