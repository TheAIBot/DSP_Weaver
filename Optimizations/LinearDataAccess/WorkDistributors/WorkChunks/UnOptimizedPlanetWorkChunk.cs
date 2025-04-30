using System;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class UnOptimizedPlanetWorkChunk : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly WorkType _workType;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;
    private WorkStep _workStep;

    private UnOptimizedPlanetWorkChunk(PlanetFactory planet, WorkType workType, int workIndex, int maxWorkCount)
    {
        _planet = planet;
        _workType = workType;
        _workIndex = workIndex;
        _maxWorkCount = maxWorkCount;
    }

    public static UnOptimizedPlanetWorkChunk[] CreateDuplicateChunks(PlanetFactory planet, WorkType workType, int count)
    {
        UnOptimizedPlanetWorkChunk[] workChunks = new UnOptimizedPlanetWorkChunk[count];
        for (int i = 0; i < workChunks.Length; i++)
        {
            workChunks[i] = new UnOptimizedPlanetWorkChunk(planet, workType, i, count);
        }

        return workChunks;
    }

    public void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool isActive = localPlanet == _planet.planet;
        workerThreadExecutor.curThreadIdx = _workIndex;
        workerThreadExecutor.usedThreadCnt = _maxWorkCount;
        if (_workType == WorkType.BeforePower)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.beforePowerLocalPlanet = localPlanet;
            workerThreadExecutor.beforePowerFactories = [_planet];
            workerThreadExecutor.beforePowerFactoryCnt = 1;
            workerThreadExecutor.beforePowerTime = time;
            workerThreadExecutor.BeforePowerPartExecute();
            workerTimings.RecordTime(WorkType.BeforePower);
        }
        else if (_workType == WorkType.Power && _planet.powerSystem != null)
        {
            workerTimings.StartTimer();
            _planet.powerSystem.multithreadPlayerPos = playerPosition;
            _planet.powerSystem.GameTick(time, isActive, isMultithreadMode: true);
            workerTimings.RecordTime(WorkType.Power);
        }
        else if (_workType == WorkType.Construction && _planet.constructionSystem != null)
        {
            throw new InvalidOperationException($"The work type {nameof(WorkType.Construction)} is not thread safe.");
        }
        else if (_workType == WorkType.CheckBefore && _planet.factorySystem != null)
        {
            throw new InvalidOperationException($"The work type {nameof(WorkType.CheckBefore)} is not thread safe.");
        }
        else if (_workType == WorkType.Assembler)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.assemblerLocalPlanet = localPlanet;
            workerThreadExecutor.assemblerFactories = [_planet];
            workerThreadExecutor.assemblerFactoryCnt = 1;
            workerThreadExecutor.assemblerTime = time;
            workerThreadExecutor.AssemblerPartExecute();
            workerTimings.RecordTime(WorkType.Assembler);
        }
        else if (_workType == WorkType.LabResearchMode)
        {
            workerTimings.StartTimer();
            lock (singleThreadedCodeLock)
            {
                _planet.factorySystem.GameTickLabResearchMode(time, isActive);
            }
            workerTimings.RecordTime(WorkType.LabResearchMode);
        }
        else if (_workType == WorkType.LabOutput2NextData)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.labOutput2NextLocalPlanet = localPlanet;
            workerThreadExecutor.labOutput2NextFactories = [_planet];
            workerThreadExecutor.labOutput2NextFactoryCnt = 1;
            workerThreadExecutor.labOutput2NextTime = time;
            workerThreadExecutor.LabOutput2NextPartExecute();
            workerTimings.RecordTime(WorkType.LabResearchMode);
        }
        else if (_workType == WorkType.TransportData && _planet.transport != null)
        {
            workerTimings.StartTimer();
            _planet.transport.multithreadPlayerPos = playerPosition;
            workerThreadExecutor.transportLocalPlanet = localPlanet;
            workerThreadExecutor.transportFactories = [_planet];
            workerThreadExecutor.transportFactoryCnt = 1;
            workerThreadExecutor.transportTime = time;
            workerThreadExecutor.TransportPartExecute();
            workerTimings.RecordTime(WorkType.TransportData);
        }
        else if (_workType == WorkType.InputFromBelt && _planet.transport != null)
        {
            workerTimings.StartTimer();
            _planet.transport.GameTick_InputFromBelt(time);
            workerTimings.RecordTime(WorkType.InputFromBelt);
        }
        else if (_workType == WorkType.InserterData)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.inserterLocalPlanet = localPlanet;
            workerThreadExecutor.inserterFactories = [_planet];
            workerThreadExecutor.inserterFactoryCnt = 1;
            workerThreadExecutor.inserterTime = time;
            workerThreadExecutor.InserterPartExecute();
            workerTimings.RecordTime(WorkType.InserterData);
        }
        else if (_workType == WorkType.Storage && _planet.factoryStorage != null)
        {
            workerTimings.StartTimer();
            _planet.factoryStorage.GameTick(time, isActive);
            workerTimings.RecordTime(WorkType.Storage);
        }
        else if (_workType == WorkType.CargoPathsData)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.cargoPathsLocalPlanet = localPlanet;
            workerThreadExecutor.cargoPathsFactories = [_planet];
            workerThreadExecutor.cargoPathsFactoryCnt = 1;
            workerThreadExecutor.cargoPathsTime = time;
            workerThreadExecutor.CargoPathsPartExecute();
            workerTimings.RecordTime(WorkType.CargoPathsData);
        }
        else if (_workType == WorkType.Splitter && _planet.cargoTraffic != null)
        {
            // Game itself takes care of recording time
            _planet.cargoTraffic.SplitterGameTick(time);
        }
        else if (_workType == WorkType.Monitor && _planet.cargoTraffic != null)
        {
            // Game itself takes care of recording time
            _planet.cargoTraffic.MonitorGameTick();
        }
        else if (_workType == WorkType.Spraycoater && _planet.cargoTraffic != null)
        {
            // Game itself takes care of recording time
            _planet.cargoTraffic.SpraycoaterGameTick();
        }
        else if (_workType == WorkType.Piler && _planet.cargoTraffic != null)
        {
            // Game itself takes care of recording time
            _planet.cargoTraffic.PilerGameTick();
        }
        else if (_workType == WorkType.OutputToBelt && _planet.transport != null)
        {
            workerTimings.StartTimer();
            int stationPilerLevel = GameMain.history.stationPilerLevel;
            _planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
            workerTimings.RecordTime(WorkType.OutputToBelt);
        }
        else if (_workType == WorkType.SandboxMode && _planet.transport != null)
        {
            workerTimings.StartTimer();
            lock (singleThreadedCodeLock)
            {
                _planet.transport.GameTick_SandboxMode();
            }
            workerTimings.RecordTime(WorkType.SandboxMode);
        }
        else if (_workType == WorkType.PresentCargoPathsData)
        {
            workerTimings.StartTimer();
            workerThreadExecutor.presentCargoPathsLocalPlanet = localPlanet;
            workerThreadExecutor.presentCargoPathsFactories = [_planet];
            workerThreadExecutor.presentCargoPathsFactoryCnt = 1;
            workerThreadExecutor.presentCargoPathsTime = time;
            workerThreadExecutor.PresentCargoPathsPartExecute();
            workerTimings.RecordTime(WorkType.PresentCargoPathsData);
        }
        else if (_workType == WorkType.Digital && _planet.digitalSystem != null)
        {
            workerTimings.StartTimer();
            _planet.digitalSystem.GameTick(isActive);
            workerTimings.RecordTime(WorkType.Digital);
        }
    }

    public void TieToWorkStep(WorkStep workStep)
    {
        _workStep = workStep;
    }

    public bool Complete()
    {
        if (_workStep == null)
        {
            throw new InvalidOperationException("No work step was assigned.");
        }

        return _workStep.CompleteWorkChunk();
    }

    public void CompleteStep()
    {
        if (_workStep == null)
        {
            throw new InvalidOperationException("No work step was assigned.");
        }

        _workStep.CompleteStep();
    }
}
