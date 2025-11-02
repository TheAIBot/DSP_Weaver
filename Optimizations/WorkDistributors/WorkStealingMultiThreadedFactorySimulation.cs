using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Weaver.Optimizations.Labs;

namespace Weaver.Optimizations.WorkDistributors;

internal static class ThreadLocalData
{
    public static ThreadLocal<int?> ThreadIndex { get; } = new();
}

internal static class WeaverThreadHelper
{
    private static int _value = 0;

    public static int GetParallelism() => Math.Max(1, GameMain.logic.threadController.wantedThreadCount);
    
    // For testing purposes on deadlocks happening more often under different thread counts
    //public static int GetParallelism() => Math.Max(1, _value++ % 32);//Math.Max(1, GameMain.logic.threadController.wantedThreadCount);
}

internal sealed class WeaverThread : IDisposable
{
    private readonly WorkStealingMultiThreadedFactorySimulation _workStealingMultiThreadedFactorySimulation;
    private readonly ManualResetEvent _doWork;
    private readonly ManualResetEvent _workerDone;
    private readonly Thread _thread;
    private WorkExecutor? _workExecutor = null;
    private WorkTaskType _workTaskType;
    private bool _isWorking = true;

    [MemberNotNullWhen(true, nameof(_workExecutor))]
    public bool IsInitialized => _workExecutor != null;

    public static WeaverThread CreateThread(WorkStealingMultiThreadedFactorySimulation workStealingMultiThreadedFactorySimulation)
    {
        var thread = new Thread(static obj =>
        {
            try
            {
                ((WeaverThread)obj).DoWork();
            }
            catch (Exception e)
            {
                WeaverFixes.Logger.LogError(e.Message);
                WeaverFixes.Logger.LogError(e.StackTrace);
            }
        });

        var weaverThread = new WeaverThread(workStealingMultiThreadedFactorySimulation, thread);
        thread.Start(weaverThread);

        return weaverThread;
    }

    private WeaverThread(WorkStealingMultiThreadedFactorySimulation workStealingMultiThreadedFactorySimulation, Thread thread)
    {
        _workStealingMultiThreadedFactorySimulation = workStealingMultiThreadedFactorySimulation;
        _doWork = new ManualResetEvent(false);
        _workerDone = new ManualResetEvent(false);
        _thread = thread;
    }

    public void StartWork(WorkTaskType workTaskType)
    {
        _workTaskType = workTaskType;
        _doWork.Set();
    }

    public void WaitForCompletion()
    {
        _workerDone.WaitOne();
        _workerDone.Reset();
    }

    public void DoWork()
    {
        try
        {
            while (_isWorking)
            {
                _doWork.WaitOne();
                _doWork.Reset();
                if (!_isWorking)
                {
                    break;
                }

                if (!IsInitialized)
                {
                    throw new InvalidOperationException("Weaver thread was not initialized with work.");
                }

                ThreadLocalData.ThreadIndex.Value = _workExecutor.WorkerIndex;
                switch (_workTaskType)
                {
                    case WorkTaskType.FactorySimulation:
                        _workExecutor.ExecuteFactorySimulation(_workStealingMultiThreadedFactorySimulation._localPlanet, _workStealingMultiThreadedFactorySimulation._time, _workStealingMultiThreadedFactorySimulation._playerPosition);
                        break;
                    case WorkTaskType.DefenseSystemTurret:
                        _workExecutor.ExecuteDefenseSystemTurret(_workStealingMultiThreadedFactorySimulation._localPlanet, _workStealingMultiThreadedFactorySimulation._time, _workStealingMultiThreadedFactorySimulation._playerPosition);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown work type: {_workTaskType}");
                }

                _workerDone.Set();
            }
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
            _workerDone.Set();
        }
    }

    public void SetWorkExecutor(WorkExecutor workExecutor)
    {
        _workExecutor = workExecutor;
    }

    public void ClearWorkExecutor()
    {
        _workExecutor = null;
    }

    public void Dispose()
    {
        _isWorking = false;
        _doWork.Set();
        _thread.Join();

        _doWork.Dispose();
        _workerDone.Dispose();
    }
}

internal enum WorkTaskType
{
    FactorySimulation,
    DefenseSystemTurret
}

internal sealed class WorkStealingMultiThreadedFactorySimulation : IDisposable
{
    private readonly HighStopwatch _stopWatch = new();
    private readonly object _singleThreadedCodeLock = new();
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private readonly DysonSphereManager _dysonSphereManager;
    private readonly UniverseStaticDataBuilder _universeStaticDataBuilder;
    private StarClusterWorkManager? _starClusterWorkManager;
    private WeaverThread[]? _threads;


    public PlanetData? _localPlanet;
    public long _time;
    public UnityEngine.Vector3 _playerPosition;



    public WorkStealingMultiThreadedFactorySimulation(StarClusterResearchManager starClusterResearchManager,
                                                      DysonSphereManager dysonSphereManager,
                                                      UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _starClusterResearchManager = starClusterResearchManager;
        _dysonSphereManager = dysonSphereManager;
        _universeStaticDataBuilder = universeStaticDataBuilder;
    }

    /// <summary>
    /// Takes a list of planets that will be updated this tick. 
    /// Solely exist to support SampleAndHoldSim which manipulates the list of planets every tick.
    /// </summary>
    /// <param name="planetsToUpdate"></param>
    public void Simulate(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
    {
        try
        {
            int targetThreadCount = WeaverThreadHelper.GetParallelism();
            if (_starClusterWorkManager == null)
            {
                _starClusterWorkManager = new StarClusterWorkManager();
            }

            if (_threads == null || _threads.Length != targetThreadCount)
            {
                //WeaverFixes.Logger.LogMessage("Change thread count");
                ThreadLocalData.ThreadIndex.Value = -1; // For main thread which also does work. Will otherwise crash when value does not exist.
                if (_threads == null)
                {
                    _threads = new WeaverThread[targetThreadCount];
                }
                else if (_threads.Length < targetThreadCount)
                {
                    Array.Resize(ref _threads, targetThreadCount);
                }

                //WeaverFixes.Logger.LogMessage($"Updated thread count to: {_threads.Length}");
                for (int i = 0; i < _threads.Length; i++)
                {
                    var workExecutor = new WorkExecutor(_starClusterWorkManager, i, _singleThreadedCodeLock);
                    if (_threads[i] == null)
                    {
                        _threads[i] = WeaverThread.CreateThread(this);
                    }
                }
            }

            // This ensures threads are updated with new work whenever a new save is loaded
            for (int i = 0; i < _threads.Length; i++)
            {
                if (!_threads[i].IsInitialized)
                {
                    var workExecutor = new WorkExecutor(_starClusterWorkManager, i, _singleThreadedCodeLock);
                    _threads[i].SetWorkExecutor(workExecutor);
                }
            }

            //WeaverFixes.Logger.LogMessage("Before scheduling");
            DeepProfiler.BeginSample(DPEntry.Scheduling);
            _starClusterWorkManager.UpdateListOfPlanets(gameLogic, GameMain.data.factories, GameMain.data.dysonSpheres, targetThreadCount);
            _starClusterWorkManager.Reset();
            DeepProfiler.EndSample(DPEntry.Scheduling);
            //WeaverFixes.Logger.LogMessage("After scheduling");

            _stopWatch.Begin();

            _localPlanet = GameMain.localPlanet;
            _time = GameMain.gameTick;
            _playerPosition = GameMain.mainPlayer.position;
            _universeStaticDataBuilder.UpdateStaticDataIfRequired();

            //WeaverFixes.Logger.LogMessage("Before pre single threaded steps");
            ExecutePreFactorySingleThreadedSteps(gameLogic, planetsToUpdate, _localPlanet, _time, targetThreadCount);
            //WeaverFixes.Logger.LogMessage("After pre single threaded steps");

            //WeaverFixes.Logger.LogMessage("Before parallel simulation");
            ExecuteParallel(targetThreadCount, WorkTaskType.FactorySimulation);
            //WeaverFixes.Logger.LogMessage("After parallel simulation");

            //WeaverFixes.Logger.LogMessage("Before ui thread stuff");
            _starClusterResearchManager.UIThreadUnlockResearchedTechnologies(GameMain.history);
            _dysonSphereManager.UIThreadCreateDysonSpheres();
            //WeaverFixes.Logger.LogMessage("After ui thread stuff");

            //WeaverFixes.Logger.LogMessage("Before post single threaded steps");
            ExecutePostFactorySingleThreadedSteps(gameLogic, planetsToUpdate, _time, targetThreadCount);
            //WeaverFixes.Logger.LogMessage("After post single threaded steps");

            double totalTime = _stopWatch.duration;
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
        }
    }

    private void ExecuteParallel(int targetThreadCount, WorkTaskType workTaskType)
    {
        for (int i = 0; i < targetThreadCount; i++)
        {
            _threads[i].StartWork(workTaskType);
        }
        for (int i = 0; i < targetThreadCount; i++)
        {
            _threads[i].WaitForCompletion();
        }
    }

    public void Clear()
    {
        _starClusterWorkManager?.Dispose();
        _starClusterWorkManager = null;

        if (_threads != null)
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i]?.ClearWorkExecutor();
            }
        }
    }

    public void Dispose()
    {
        Clear();
        if (_threads != null)
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i]?.Dispose();
            }
        }
    }

    public void PrintWorkStatistics()
    {
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager();
        }

        _starClusterWorkManager.UpdateListOfPlanets(GameMain.logic, GameMain.data.factories, GameMain.data.dysonSpheres, WeaverThreadHelper.GetParallelism());
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

    private static void ExecutePreFactorySingleThreadedSteps(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate, PlanetData localPlanet, long time, int targetThreadCount)
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
        // Handled by SolarSystemDysonSphereWorkChunk

        // 1001
        gameLogic.OnFactoryBeginProfiler();
        gameLogic.OnFactoryFrameBegin();

        // 1100
        // Incomplete?????
        gameLogic.FactoryBeforeGameTick();

        // 1201
        // Handled by DysonSpherePowerRequest

        // 1250
        //gameLogic.EnemyGroundPrepare();
        if (gameLogic.isCombatMode)
        {
            DeepProfiler.BeginSample(DPEntry.DFGSystem, -1, 99L);
            gameLogic.data.localLoadedPlanetFactory?.LocalizeEnemies();
            DeepProfiler.EndSample(DPEntry.DFGSystem);
        }

        // 1301
        // Nothing

        // 1321
        // Nothing

        // 1350
        if (gameLogic.isCombatMode)
        {
            DeepProfiler.BeginSample(DPEntry.DFGSystem);
            gameLogic.data.localLoadedPlanetFactory?.LocalizeEnemies();
            DeepProfiler.EndSample(DPEntry.DFGSystem);
        }


        // 1400
        //gameLogic.FactoryConstructionSystemGameTick();
        // construction contains updates to compute buffer so it has to be done here for the local planet
        if (localPlanet != null)
        {
            DeepProfiler.BeginSample(DPEntry.Construction);
            PlanetFactory planetFactory = localPlanet.factory;
            planetFactory.constructionSystem.GameTick(time, true);
            planetFactory.constructionSystem.ExcuteDeferredTargetChange();
            DeepProfiler.EndSample(DPEntry.Construction);
        }
    }

    private void ExecutePostFactorySingleThreadedSteps(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate, long time, int targetThreadCount)
    {
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
        DeepProfiler.BeginSample(DPEntry.GroundDefenseSystem);
        foreach (var optimizedPlanet in OptimizedStarCluster.GetAllOptimizedPlanets())
        {
            if (optimizedPlanet is not OptimizedTerrestrialPlanet terrestrialPlanet)
            {
                continue;
            }

            terrestrialPlanet.GameTickDefense(time);
        }
        DeepProfiler.EndSample(DPEntry.GroundDefenseSystem);

        // 3151
        // Parallelize here DefenseSystemTurretGameTick_Parallel
        ExecuteParallel(targetThreadCount, WorkTaskType.DefenseSystemTurret);
        gameLogic.OnFactoryEndProfiler();

        // 3201
        gameLogic.TrashSystemGameTick();

        // 3301
        gameLogic.DysonSphereGameTick();

        // 3320
        // Parallelize here DysonSwarmGameTick_Parallel
        gameLogic.DysonSwarmGameTick();

        // 3321
        gameLogic.DysonSwarmGameTickPost();

        // 3351
        // Parallelize here DysonSphereRocketGameTick_Parallel
        gameLogic.DysonSphereRocketGameTick();

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
        DeepProfiler.BeginSample(DPEntry.Statistics);
        if (!DSPGame.IsMenuDemo)
        {
            GameMain.data.statistics.GameTickStats(time);
        }
        DeepProfiler.EndSample(DPEntry.Statistics);

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
