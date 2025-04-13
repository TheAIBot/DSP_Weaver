using System;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly WorkerThreadExecutor _workerThreadExecutor;
    private readonly object _singleThreadedCodeLock;
    private readonly HighStopwatch _stopWatch = new();
    private readonly double[] _workTypeTimings;

    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerThreadExecutor = workerThreadExecutor;
        _singleThreadedCodeLock = singleThreadedCodeLock;
        _workTypeTimings = new double[ArrayExtensions.GetEnumValuesEnumerable<WorkType>().Max(x => (int)x) + 1];
    }

    public void Execute(PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        int originalWorkerThreadIndex = _workerThreadExecutor.curThreadIdx;
        int originalWorkerUsedThreadCount = _workerThreadExecutor.usedThreadCnt;
        try
        {
            PlanetWorkManager planetWorkManager = null;
            while (true)
            {
                WorkPlan? workPlan = null;
                if (planetWorkManager != null)
                {
                    workPlan = planetWorkManager.TryGetWork(out var _);
                    if (workPlan == null)
                    {
                        planetWorkManager = null;
                    }
                }

                if (workPlan == null)
                {
                    PlanetWorkPlan? planetWorkPlan = _starClusterWorkManager.TryGetWork();
                    if (planetWorkPlan == null)
                    {
                        break;
                    }

                    planetWorkManager = planetWorkPlan.Value.PlanetWorkManager;
                    workPlan = planetWorkPlan.Value.WorkPlan;
                }

                _stopWatch.Begin();
                bool recordTiming = true;

                bool isActive = localPlanet == planetWorkManager.Planet.planet;
                _workerThreadExecutor.curThreadIdx = workPlan.Value.WorkIndex;
                _workerThreadExecutor.usedThreadCnt = workPlan.Value.WorkParallelism;
                if (workPlan.Value.WorkType == WorkType.BeforePower)
                {
                    _workerThreadExecutor.beforePowerLocalPlanet = localPlanet;
                    _workerThreadExecutor.beforePowerFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.beforePowerFactoryCnt = 1;
                    _workerThreadExecutor.beforePowerTime = time;
                    //_workerThreadExecutor.threadMissionOrders |= 16u;
                    _workerThreadExecutor.BeforePowerPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.Power && planetWorkManager.Planet.powerSystem != null)
                {
                    planetWorkManager.Planet.powerSystem.multithreadPlayerPos = playerPosition;
                    planetWorkManager.Planet.powerSystem.GameTick(time, isActive, isMultithreadMode: true);
                    //_workerThreadExecutor.powerSystemLocalPlanet = localPlanet;
                    //_workerThreadExecutor.powerSystemFactories = [planetWorkManager.Planet];
                    //_workerThreadExecutor.powerSystemFactoryCnt = 1;
                    //_workerThreadExecutor.powerSystemTime = time;
                    //_workerThreadExecutor.threadMissionOrders |= 32u;
                    //_workerThreadExecutor.PowerSystemPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.Construction && planetWorkManager.Planet.constructionSystem != null)
                {
                    //lock (_singleThreadedCodeLock)
                    //{
                    //    planetWorkManager.Planet.constructionSystem.GameTick(time, isActive);
                    //    planetWorkManager.Planet.constructionSystem.ExcuteDeferredTargetChange();
                    //}
                }
                else if (workPlan.Value.WorkType == WorkType.CheckBefore && planetWorkManager.Planet.factorySystem != null)
                {
                    //lock (_singleThreadedCodeLock)
                    //{
                    //    planetWorkManager.Planet.factorySystem.CheckBeforeGameTick();
                    //}
                }
                else if (workPlan.Value.WorkType == WorkType.Assembler)
                {
                    _workerThreadExecutor.assemblerLocalPlanet = localPlanet;
                    _workerThreadExecutor.assemblerFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.assemblerFactoryCnt = 1;
                    _workerThreadExecutor.assemblerTime = time;
                    _workerThreadExecutor.AssemblerPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.LabResearchMode)
                {
                    lock (_singleThreadedCodeLock)
                    {
                        planetWorkManager.Planet.factorySystem.GameTickLabResearchMode(time, isActive);
                    }
                }
                else if (workPlan.Value.WorkType == WorkType.LabOutput2NextData)
                {
                    _workerThreadExecutor.labOutput2NextLocalPlanet = localPlanet;
                    _workerThreadExecutor.labOutput2NextFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.labOutput2NextFactoryCnt = 1;
                    _workerThreadExecutor.labOutput2NextTime = time;
                    _workerThreadExecutor.LabOutput2NextPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.TransportData && planetWorkManager.Planet.transport != null)
                {
                    planetWorkManager.Planet.transport.multithreadPlayerPos = playerPosition;
                    _workerThreadExecutor.transportLocalPlanet = localPlanet;
                    _workerThreadExecutor.transportFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.transportFactoryCnt = 1;
                    _workerThreadExecutor.transportTime = time;
                    _workerThreadExecutor.TransportPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.InputFromBelt && planetWorkManager.Planet.transport != null)
                {
                    planetWorkManager.Planet.transport.GameTick_InputFromBelt(time);
                }
                else if (workPlan.Value.WorkType == WorkType.InserterData)
                {
                    if (planetWorkManager.OptimizedPlanet.Status == OptimizedPlanetStatus.Running)
                    {
                        planetWorkManager.OptimizedPlanet._optimizedBiInserterExecutor.GameTickInserters(planetWorkManager.Planet, planetWorkManager.OptimizedPlanet, time, workPlan.Value.WorkParallelism, workPlan.Value.WorkIndex);
                        planetWorkManager.OptimizedPlanet._optimizedInserterExecutor.GameTickInserters(planetWorkManager.Planet, planetWorkManager.OptimizedPlanet, time, workPlan.Value.WorkParallelism, workPlan.Value.WorkIndex);
                    }
                    else
                    {
                        _workerThreadExecutor.inserterLocalPlanet = localPlanet;
                        _workerThreadExecutor.inserterFactories = [planetWorkManager.Planet];
                        _workerThreadExecutor.inserterFactoryCnt = 1;
                        _workerThreadExecutor.inserterTime = time;
                        _workerThreadExecutor.InserterPartExecute();
                    }
                }
                else if (workPlan.Value.WorkType == WorkType.Storage && planetWorkManager.Planet.factoryStorage != null)
                {
                    planetWorkManager.Planet.factoryStorage.GameTick(time, isActive);
                }
                else if (workPlan.Value.WorkType == WorkType.CargoPathsData)
                {
                    _workerThreadExecutor.cargoPathsLocalPlanet = localPlanet;
                    _workerThreadExecutor.cargoPathsFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.cargoPathsFactoryCnt = 1;
                    _workerThreadExecutor.cargoPathsTime = time;
                    _workerThreadExecutor.CargoPathsPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.Splitter && planetWorkManager.Planet.cargoTraffic != null)
                {
                    recordTiming = false;
                    planetWorkManager.Planet.cargoTraffic.SplitterGameTick(time);
                }
                else if (workPlan.Value.WorkType == WorkType.Monitor && planetWorkManager.Planet.cargoTraffic != null)
                {
                    recordTiming = false;
                    planetWorkManager.Planet.cargoTraffic.MonitorGameTick();
                }
                else if (workPlan.Value.WorkType == WorkType.Spraycoater && planetWorkManager.Planet.cargoTraffic != null)
                {
                    if (planetWorkManager.OptimizedPlanet.Status == OptimizedPlanetStatus.Running)
                    {
                        planetWorkManager.OptimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(planetWorkManager.Planet);
                    }
                    else
                    {
                        recordTiming = false;
                        planetWorkManager.Planet.cargoTraffic.SpraycoaterGameTick();
                    }
                }
                else if (workPlan.Value.WorkType == WorkType.Piler && planetWorkManager.Planet.cargoTraffic != null)
                {
                    recordTiming = false;
                    planetWorkManager.Planet.cargoTraffic.PilerGameTick();
                }
                else if (workPlan.Value.WorkType == WorkType.OutputToBelt && planetWorkManager.Planet.transport != null)
                {
                    int stationPilerLevel = GameMain.history.stationPilerLevel;
                    planetWorkManager.Planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
                }
                else if (workPlan.Value.WorkType == WorkType.SandboxMode && planetWorkManager.Planet.transport != null)
                {
                    lock (_singleThreadedCodeLock)
                    {
                        planetWorkManager.Planet.transport.GameTick_SandboxMode();
                    }
                }
                else if (workPlan.Value.WorkType == WorkType.PresentCargoPathsData)
                {
                    _workerThreadExecutor.presentCargoPathsLocalPlanet = localPlanet;
                    _workerThreadExecutor.presentCargoPathsFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.presentCargoPathsFactoryCnt = 1;
                    _workerThreadExecutor.presentCargoPathsTime = time;
                    _workerThreadExecutor.PresentCargoPathsPartExecute();
                }
                else if (workPlan.Value.WorkType == WorkType.Digital && planetWorkManager.Planet.digitalSystem != null)
                {
                    planetWorkManager.Planet.digitalSystem.GameTick(isActive);
                }

                if (recordTiming)
                {
                    _workTypeTimings[(int)workPlan.Value.WorkType] += _stopWatch.duration;
                }

                planetWorkManager.CompleteWork(workPlan.Value);
            }
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
            throw;
        }
        finally
        {
            _workerThreadExecutor.curThreadIdx = originalWorkerThreadIndex;
            _workerThreadExecutor.usedThreadCnt = originalWorkerUsedThreadCount;
        }
    }

    public double[] GetWorkTypeTimings() => _workTypeTimings;

    public void Reset()
    {
        Array.Clear(_workTypeTimings, 0, _workTypeTimings.Length);
    }
}
