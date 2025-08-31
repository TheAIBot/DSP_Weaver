using System.Linq;
using System.Threading.Tasks;
using Weaver.Optimizations.Labs;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkStealingMultiThreadedFactorySimulation
{
    private readonly HighStopwatch _stopWatch = new();
    private readonly object _singleThreadedCodeLock = new();
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private readonly DysonSphereManager _dysonSphereManager;
    private StarClusterWorkManager? _starClusterWorkManager;
    private WorkExecutor[]? _workExecutors;

    public WorkStealingMultiThreadedFactorySimulation(StarClusterResearchManager starClusterResearchManager,
                                                      DysonSphereManager dysonSphereManager)
    {
        _starClusterResearchManager = starClusterResearchManager;
        _dysonSphereManager = dysonSphereManager;
    }

    /// <summary>
    /// Takes a list of planets that will be updated this tick. 
    /// Solely exist to support SampleAndHoldSim which manipulates the list of planets every tick.
    /// </summary>
    /// <param name="planetsToUpdate"></param>
    public void Simulate(PlanetFactory?[] planetsToUpdate)
    {
        int targetThreadCount = GameMain.logic.threadController.wantedThreadCount;
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager();
        }
        if (_workExecutors == null || _workExecutors.Length != targetThreadCount)
        {
            _workExecutors = new WorkExecutor[targetThreadCount];
            for (int i = 0; i < _workExecutors.Length; i++)
            {
                _workExecutors[i] = new WorkExecutor(_starClusterWorkManager, GameMain.logic.threadController.threadManager.workerThreads[i], _singleThreadedCodeLock);
            }
        }

        _starClusterWorkManager.UpdateListOfPlanets(GameMain.data.factories, planetsToUpdate, targetThreadCount);
        _starClusterWorkManager.Reset();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = targetThreadCount,
        };

        _stopWatch.Begin();

        ExecuteSingleThreadedSteps(planetsToUpdate);

        PlanetData? localPlanet = GameMain.localPlanet;
        long time = GameMain.gameTick;
        UnityEngine.Vector3 playerPosition = GameMain.mainPlayer.position;

        Parallel.ForEach(_workExecutors, parallelOptions, workExecutor => workExecutor.Execute(localPlanet, time, playerPosition));
        _starClusterResearchManager.UIThreadUnlockResearchedTechnologies(GameMain.history);
        _dysonSphereManager.UIThreadCreateDysonSpheres();

        double totalTime = _stopWatch.duration;
    }

    public void Clear()
    {
        _starClusterWorkManager = null;
        _workExecutors = null;
    }

    public void PrintWorkStatistics()
    {
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager();
        }

        _starClusterWorkManager.UpdateListOfPlanets(GameMain.data.factories, GameMain.data.factories, GameMain.logic.threadController.wantedThreadCount);
        StarClusterWorkStatistics starClusterWorkStatistics = _starClusterWorkManager.GetStartClusterStatistics();

        WeaverFixes.Logger.LogInfo($"Planet Count: {starClusterWorkStatistics.PlanetWorkStatistics.Length:N0}");
        WeaverFixes.Logger.LogInfo($"Total work steps: {starClusterWorkStatistics.PlanetWorkStatistics.Sum(x => x.WorkStepsCount):N0}");
        WeaverFixes.Logger.LogInfo($"Total work chunks: {starClusterWorkStatistics.PlanetWorkStatistics.Sum(x => x.TotalWorkChunkCount):N0}");
        WeaverFixes.Logger.LogInfo($"All planets:");
        foreach (PlanetWorkStatistics planetWorkStatistics in starClusterWorkStatistics.PlanetWorkStatistics)
        {
            WeaverFixes.Logger.LogInfo($"\t{planetWorkStatistics}");
        }
    }

    private static void ExecuteSingleThreadedSteps(PlanetFactory?[] planetsToUpdate)
    {
        for (int i = 0; i < planetsToUpdate.Length; i++)
        {
            PlanetFactory? planet = planetsToUpdate[i];
            if (planet?.constructionSystem == null)
            {
                continue;
            }

            bool isActive = planet.planet == GameMain.localPlanet;
            planet.constructionSystem.GameTick(GameMain.gameTick, isActive);
            planet.constructionSystem.ExcuteDeferredTargetChange();
        }

        //for (int i = 0; i < planetsToUpdate.Length; i++)
        //{
        //    PlanetFactory? planet = planetsToUpdate[i];
        //    if (planet?.factorySystem == null)
        //    {
        //        continue;
        //    }

        //    planet.factorySystem.CheckBeforeGameTick();
        //}
    }
        {
            PlanetFactory? planet = planetsToUpdate[i];
            if (planet?.factorySystem == null)
            {
                continue;
            }

            planet.factorySystem.CheckBeforeGameTick();
        }
    }
}
