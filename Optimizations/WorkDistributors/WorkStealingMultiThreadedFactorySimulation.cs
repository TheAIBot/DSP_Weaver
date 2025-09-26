using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Weaver.Optimizations.Labs;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkStealingMultiThreadedFactorySimulation
{
    private readonly HighStopwatch _stopWatch = new();
    private readonly object _singleThreadedCodeLock = new();
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private readonly DysonSphereManager _dysonSphereManager;
    private StarClusterWorkManager? _starClusterWorkManager;
    //private WorkExecutor[]? _workExecutors;
    private Thread[]? _threads;
    private ManualResetEvent[]? _workerDone;
    private ManualResetEvent[]? _doWork;
    private bool _done = false;

    private PlanetData? _localPlanet;
    private long _time;
    private UnityEngine.Vector3 _playerPosition;

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
    public void Simulate(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
    {
        int targetThreadCount = GameMain.logic.threadController.wantedThreadCount;
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager();
        }
        if (_threads == null || _threads.Length != targetThreadCount)
        {
            ClearThreads();
            _threads = new Thread[targetThreadCount];
            _doWork = new ManualResetEvent[targetThreadCount];
            _workerDone = new ManualResetEvent[targetThreadCount];
            for (int i = 0; i < _threads.Length; i++)
            {
                var workExecutor = new WorkExecutor(_starClusterWorkManager, i, _singleThreadedCodeLock);

                var workerDone = new ManualResetEvent(false);
                _workerDone[i] = workerDone;

                var dooooWork = new ManualResetEvent(false);
                _doWork[i] = dooooWork;

                var thread = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            dooooWork.WaitOne();
                            dooooWork.Reset();
                            if (_done)
                            {
                                break;
                            }
                            workExecutor.Execute(_localPlanet, _time, _playerPosition);
                            workerDone.Set();
                        }
                    }
                    catch (System.Exception e)
                    {
                        WeaverFixes.Logger.LogError(e.Message);
                        WeaverFixes.Logger.LogError(e.StackTrace);
                    }

                });
                thread.Start();
                _threads[i] = thread;
            }
        }

        _starClusterWorkManager.UpdateListOfPlanets(GameMain.data.factories, targetThreadCount);
        _starClusterWorkManager.Reset();

        //var parallelOptions = new ParallelOptions
        //{
        //    MaxDegreeOfParallelism = targetThreadCount,
        //};

        _stopWatch.Begin();

        ExecutePreFactorySingleThreadedSteps(gameLogic, planetsToUpdate);

        _localPlanet = GameMain.localPlanet;
        _time = GameMain.gameTick;
        _playerPosition = GameMain.mainPlayer.position;

        for (int i = 0; i < _doWork.Length; i++)
        {
            _doWork[i].Set();
        }
        for (int i = 0; i < _workerDone.Length; i++)
        {
            _workerDone[i].WaitOne();
            _workerDone[i].Reset();
        }

        //Parallel.ForEach(_workExecutors, parallelOptions, workExecutor => workExecutor.Execute(localPlanet, time, playerPosition));
        _starClusterResearchManager.UIThreadUnlockResearchedTechnologies(GameMain.history);
        _dysonSphereManager.UIThreadCreateDysonSpheres();

        ExecutePostFactorySingleThreadedSteps(gameLogic, planetsToUpdate);

        double totalTime = _stopWatch.duration;
    }

    public void Clear()
    {
        _starClusterWorkManager?.Dispose();
        _starClusterWorkManager = null;
        if (_threads != null)
        {
            ClearThreads();
        }
    }

    private void ClearThreads()
    {
        if (_threads == null)
        {
            return;
        }

        _done = true;
        for (int i = 0; i < _threads.Length; i++)
        {
            _doWork[i].Set();
            _threads[i].Join();
            _doWork[i].Dispose();
            _workerDone[i].Dispose();
        }
        _done = false;

        _doWork = null;
        _threads = null;
        _workerDone = null;
    }

    public void PrintWorkStatistics()
    {
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager();
        }

        _starClusterWorkManager.UpdateListOfPlanets(GameMain.data.factories, GameMain.logic.threadController.wantedThreadCount);
        StarClusterWorkStatistics starClusterWorkStatistics = _starClusterWorkManager.GetStarClusterStatistics();

        WeaverFixes.Logger.LogInfo($"Planet Count: {starClusterWorkStatistics.PlanetWorkStatistics.Length:N0}");
        WeaverFixes.Logger.LogInfo($"Total work steps: {starClusterWorkStatistics.PlanetWorkStatistics.Sum(x => x.WorkStepsCount):N0}");
        WeaverFixes.Logger.LogInfo($"Total work chunks: {starClusterWorkStatistics.PlanetWorkStatistics.Sum(x => x.TotalWorkChunkCount):N0}");
        WeaverFixes.Logger.LogInfo($"All planets:");
        foreach (PlanetWorkStatistics planetWorkStatistics in starClusterWorkStatistics.PlanetWorkStatistics)
        {
            WeaverFixes.Logger.LogInfo($"\t{planetWorkStatistics}");
        }

        WeaverFixes.Logger.LogInfo("");
        WeaverFixes.Logger.LogInfo("Work chunk types:");
        foreach ((string workName, int count) in _starClusterWorkManager.GetAllWorkChunks().GroupBy(x => x.GetType().Name).Select(x => (x.Key, x.Count())))
        {
            WeaverFixes.Logger.LogInfo($"\t{workName}: {count:10,10}");
        }
    }

    private static void ExecutePreFactorySingleThreadedSteps(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
    {
        // 151
        gameLogic.UniverseGameTick();
        gameLogic.GalaxyGameTick();

        // 301
        gameLogic.PropertySystemGameTick();

        // 400
        gameLogic.StatisticsPrepare();
        gameLogic.SpaceSectorPrepare();
        gameLogic.LocalFactoryPrepare();

        // 501
        gameLogic.LocalPlanetPhysics();
        gameLogic.SpaceSectorPhysics();

        // 601
        gameLogic.PlayerGameTick();

        // 701
        gameLogic.GalacticTransportGameTick();

        // 751
        gameLogic.GalacticDigitalGameTick();

        // 801
        gameLogic.DysonSphereBeforeGameTick();

        // 1001
        gameLogic.OnFactoryBeginProfiler();
        gameLogic.OnFactoryFrameBegin();

        // 1100
        // Incomplete?????
        gameLogic.FactoryBeforeGameTick();

        //for (int i = 0; i < planetsToUpdate.Length; i++)
        //{
        //    PlanetFactory? planet = planetsToUpdate[i];
        //    if (planet?.constructionSystem == null)
        //    {
        //        continue;
        //    }

        //    bool isActive = planet.planet == GameMain.localPlanet;
        //    planet.constructionSystem.GameTick(GameMain.gameTick, isActive);
        //    planet.constructionSystem.ExcuteDeferredTargetChange();
        //}

        //for (int i = 0; i < planetsToUpdate.Length; i++)
        //{
        //    PlanetFactory? planet = planetsToUpdate[i];
        //    if (planet?.factorySystem == null)
        //    {
        //        continue;
        //    }

        //    planet.factorySystem.CheckBeforeGameTick();
        //}

        // 1201
        DeepProfiler.BeginSample(DPEntry.PowerSystem);
        DeepProfiler.BeginSample(DPEntry.PowerGamma);
        for (int i = 0; i < planetsToUpdate.Length; i++)
        {
            PlanetFactory? planet = planetsToUpdate[i];
            planet?.powerSystem.RequestDysonSpherePower();
        }
        DeepProfiler.EndSample(DPEntry.PowerGamma);
        DeepProfiler.EndSample(DPEntry.PowerSystem);

        // 1250
        gameLogic.EnemyGroundPrepare();

        // 1301
        // Nothing

        // 1321
        // Nothing

        // 1350
        // Parallelize here EnemyGroundCombatGameTick_Parallel
    }

    private static void ExecutePostFactorySingleThreadedSteps(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
    {
        // 1400
        // Do or is that 1100?

        // 1601
        // Nothing

        // 1651
        gameLogic.EnemyGroundGameTick();

        // 1700
        // Nothing

        // 1800
        // Nothing

        // 1751
        // Nothing

        // 2000
        // Nothing

        // 2101
        // Nothing

        // 2121
        // Nothing

        // 2200
        // Nothing

        // 2259
        // Nothing

        // 2300
        // Nothing

        // 2401
        // Nothing Or mayby FactoryTransportSandboxMode_MultiMain????

        // 2501
        // Nothing

        // 2601
        // Nothing

        // 2701
        gameLogic.FactoryRuinGameTick();

        // 2801
        gameLogic.OnFactoryFrameEnd();

        // 3001
        gameLogic.CombatGroundSystemGameTick();

        // 3100
        gameLogic.DefenseGroundSystemGameTick();

        // 3151
        // Parallelize here DefenseSystemTurretGameTick_Parallel
        gameLogic.OnFactoryEndProfiler();

        // 3201
        gameLogic.TrashSystemGameTick();

        // 3301
        gameLogic.DysonSphereGameTick();

        // 3320
        // Parallelize here DysonSwarmGameTick_Parallel

        // 3321
        gameLogic.DysonSwarmGameTickPost();

        // 3351
        // Parallelize here DysonSphereRocketGameTick_Parallel

        // 3401
        gameLogic.SpaceSectorGameTick();

        // 3501
        gameLogic.OnFactoryBeginProfiler();
        gameLogic.EnemyGroundSystemPostGameTick();

        // 3601
        gameLogic.CombatGroundSystemPostGameTick();

        // 3701
        gameLogic.DefenseGroundSystemPostGameTick();
        gameLogic.OnFactoryEndProfiler();

        // 3801
        gameLogic.SpaceSectorPostGameTick();

        // 3900
        gameLogic.LocalPlanetAudio();
        gameLogic.SpaceSectorAudio();
        gameLogic.SpaceSectorAudioPost();

        // 4001
        // Parallelize here StatisticsGameTick_Parallel

        // 4100
        gameLogic.WarningSystemGameTick();

        // 4201
        // Look into parallelization?
        gameLogic.StatisticsPostGameTick();

        // 4301
        gameLogic.ScenarioGameTick();

        // 4401
        gameLogic.CollectPreferences();
    }
}
