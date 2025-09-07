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
    public void Simulate(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
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

        ExecuteSingleThreadedSteps(gameLogic, planetsToUpdate);

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

    private static void ExecuteSingleThreadedSteps(GameLogic gameLogic, PlanetFactory?[] planetsToUpdate)
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
        gameLogic.FactoryBeforeGameTick();

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

    private void OnGameLogicFrame(int iTask, int threadOrdinal, int threadCount)
    {
        bool isMainThread = threadOrdinal == -1;
        bool isWorkerThread = !isMainThread;
        bool multithreadingDisabled = threadCount <= 1;
        bool hasManyWorkerThreads = threadCount >= 2;
        bool flag5 = multithreadingDisabled && isMainThread;
        bool flag6 = hasManyWorkerThreads && isWorkerThread;
        bool flag7 = hasManyWorkerThreads && isMainThread;
        switch (iTask)
        {
            case 51:
                if (!isMainThread)
                {
                }
                break;
            case 101:
                if (!flag5 && !flag6)
                {
                }
                break;
            case 151:
                //if (isMainThread)
                //{
                //    UniverseGameTick();
                //    GalaxyGameTick();
                //}
                break;
            case 301:
                //if (isMainThread)
                //{
                //    PropertySystemGameTick();
                //}
                break;
            case 400:
                //if (multithreadingDisabled)
                //{
                //    StatisticsPrepare();
                //}
                //else if (hasManyWorkerThreads)
                //{
                //    StatisticsPrepare_Parallel(threadOrdinal);
                //}
                //if (isMainThread)
                //{
                //    SpaceSectorPrepare();
                //    LocalFactoryPrepare();
                //}
                break;
            case 501:
                //if (isMainThread)
                //{
                //    LocalPlanetPhysics();
                //    SpaceSectorPhysics();
                //}
                break;
            case 601:
                //if (isMainThread)
                //{
                //    PlayerGameTick();
                //}
                break;
            case 701:
                //if (isMainThread)
                //{
                //    GalacticTransportGameTick();
                //}
                break;
            case 751:
                //if (isMainThread)
                //{
                //    GalacticDigitalGameTick();
                //}
                break;
            case 801:
                //if (isMainThread)
                //{
                //    DysonSphereBeforeGameTick();
                //}
                break;
            case 1001:
                //if (isMainThread)
                //{
                //    OnFactoryBeginProfiler();
                //}
                //if (isMainThread)
                //{
                //    OnFactoryFrameBegin();
                //}
                break;
            case 1100:
                //if (isMainThread)
                //{
                //    FactoryBeforeGameTick();
                //}
                break;
            case 1201:
                if (flag5)
                {
                    FactoryBeforePowerGameTick();
                }
                else if (flag6)
                {
                    FactoryBeforePowerGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 1250:
                if (isMainThread)
                {
                    EnemyGroundPrepare();
                }
                break;
            case 1301:
                if (flag5)
                {
                    FactoryPowerSystemGameTick();
                }
                else if (flag6)
                {
                    FactoryPowerSystemGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 1321:
                if (flag5)
                {
                    FactoryStationInput();
                }
                else if (flag6)
                {
                    FactoryStationInput_Parallel(threadOrdinal);
                }
                break;
            case 1350:
                if (flag5)
                {
                    EnemyGroundCombatGameTick();
                }
                if (flag6)
                {
                    EnemyGroundCombatGameTick_Parallel(threadOrdinal);
                }
                break;
            case 1400:
                if (isMainThread)
                {
                    FactoryConstructionSystemGameTick();
                }
                break;
            case 1601:
                if (flag5)
                {
                    FactorySystemFacilityGameTick();
                }
                else if (flag6)
                {
                    FactorySystemFacilityGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 1651:
                if (flag5)
                {
                    EnemyGroundGameTick();
                }
                break;
            case 1700:
                if (isMainThread)
                {
                    FactorySystemLabResearchGameTick();
                }
                break;
            case 1800:
                if (flag5)
                {
                    FactorySystemLabOutputToNext();
                }
                else if (flag6)
                {
                    FactorySystemLabOutputToNext_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 1751:
                if (flag5)
                {
                    FactoryTransportGameTick();
                }
                else if (flag6)
                {
                    FactoryTransportGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2000:
                if (flag5)
                {
                    FactorySystemInserterGameTick();
                }
                else if (flag6)
                {
                    FactorySystemInserterGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2101:
                if (isMainThread)
                {
                    FactoryStorageGameTick();
                }
                break;
            case 2121:
                if (flag5)
                {
                    FactoryTankGameTick();
                }
                break;
            case 2200:
                if (flag5)
                {
                    FactoryCargoPathGameTick();
                }
                else if (flag6)
                {
                    FactoryCargoPathGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2259:
                if (flag5)
                {
                    FactorySplitterGameTick();
                }
                else if (flag6)
                {
                    FactorySplitterGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2300:
                if (flag5)
                {
                    FactoryCargoTrafficMiscGameTick();
                }
                else if (flag6)
                {
                    FactoryCargoTrafficMiscGameTick_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2401:
                if (flag5)
                {
                    FactoryTransportOutput();
                }
                else if (flag7)
                {
                    FactoryTransportSandboxMode_MultiMain();
                }
                break;
            case 2501:
                if (flag5)
                {
                    FactoryPresentCargo();
                }
                else if (flag6)
                {
                    FactoryPresentCargo_Parallel(threadOrdinal, threadCount);
                }
                break;
            case 2601:
                if (isMainThread)
                {
                    FactoryDigitalSystemGameTick();
                }
                break;
            case 2701:
                if (isMainThread)
                {
                    FactoryRuinGameTick();
                }
                break;
            case 2801:
                if (isMainThread)
                {
                    OnFactoryFrameEnd();
                }
                break;
            case 3001:
                if (isMainThread)
                {
                    CombatGroundSystemGameTick();
                }
                break;
            case 3100:
                if (isMainThread)
                {
                    DefenseGroundSystemGameTick();
                }
                break;
            case 3151:
                if (flag5)
                {
                    DefenseGroundSystemTurretGameTick();
                }
                else if (flag6)
                {
                    DefenseSystemTurretGameTick_Parallel(threadOrdinal, threadCount);
                }
                if (isMainThread)
                {
                    OnFactoryEndProfiler();
                }
                break;
            case 3201:
                if (flag5)
                {
                    TrashSystemGameTick();
                }
                break;
            case 3301:
                if (isMainThread)
                {
                    DysonSphereGameTick();
                }
                break;
            case 3320:
                if (multithreadingDisabled)
                {
                    DysonSwarmGameTick();
                }
                else if (hasManyWorkerThreads)
                {
                    DysonSwarmGameTick_Parallel(threadOrdinal);
                }
                break;
            case 3321:
                if (isMainThread)
                {
                    DysonSwarmGameTickPost();
                }
                break;
            case 3351:
                if (flag5)
                {
                    DysonSphereRocketGameTick();
                }
                else if (flag6)
                {
                    DysonSphereRocketGameTick_Parallel(threadOrdinal);
                }
                break;
            case 3401:
                if (isMainThread)
                {
                    SpaceSectorGameTick();
                }
                break;
            case 3501:
                if (isMainThread)
                {
                    OnFactoryBeginProfiler();
                }
                if (isMainThread)
                {
                    EnemyGroundSystemPostGameTick();
                }
                break;
            case 3601:
                if (isMainThread)
                {
                    CombatGroundSystemPostGameTick();
                }
                break;
            case 3701:
                if (isMainThread)
                {
                    DefenseGroundSystemPostGameTick();
                }
                if (isMainThread)
                {
                    OnFactoryEndProfiler();
                }
                break;
            case 3801:
                if (isMainThread)
                {
                    SpaceSectorPostGameTick();
                }
                break;
            case 3900:
                if (isMainThread)
                {
                    LocalPlanetAudio();
                    SpaceSectorAudio();
                    SpaceSectorAudioPost();
                }
                break;
            case 4001:
                if (multithreadingDisabled)
                {
                    StatisticsGameTick();
                }
                else if (hasManyWorkerThreads)
                {
                    StatisticsGameTick_Parallel(threadOrdinal);
                }
                break;
            case 4100:
                if (isMainThread)
                {
                    WarningSystemGameTick();
                }
                break;
            case 4201:
                if (isMainThread)
                {
                    StatisticsPostGameTick();
                }
                break;
            case 4301:
                if (isMainThread)
                {
                    ScenarioGameTick();
                }
                break;
            case 4401:
                if (isMainThread)
                {
                    CollectPreferences();
                }
                break;
        }
        if (flag7)
        {
            switch (iTask)
            {
                case 1100:
                    ContextCollect_BeforePower_MultiMain(threadCount);
                    break;
                case 1201:
                    ContextCollect_FactoryComponents_MultiMain(threadCount);
                    break;
                case 1250:
                    ContextCollect_EnemyGroundCombat_MultiMain(threadCount);
                    break;
                case 2200:
                    ContextCollect_Rocket_MultiMain(threadCount);
                    break;
                case 1301:
                    FactoryTransport_SandboxGameTick();
                    break;
                case 2000:
                    FactoryTankGameTick();
                    TrashSystemGameTick();
                    EnemyGroundGameTick();
                    break;
            }
        }
    }
}
