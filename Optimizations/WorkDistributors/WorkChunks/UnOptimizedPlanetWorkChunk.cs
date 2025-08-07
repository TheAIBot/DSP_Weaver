using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class UnOptimizedPlanetWorkChunk : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly WorkType _workType;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;
    private WorkStep? _workStep;

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

    public void Execute(WorkerThread workerThread, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool isActive = localPlanet == _planet.planet;
        if (_workType == WorkType.BeforePower)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerThread.threadIndex);
            {
                DeepProfiler.BeginSample(DPEntry.PowerGamma, workerThread.threadIndex);
                _planet.powerSystem?.RequestDysonSpherePower();
                DeepProfiler.EndSample(DPEntry.PowerGamma, workerThread.threadIndex);

                DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerThread.threadIndex);
                _planet.factorySystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 4);
                _planet.cargoTraffic?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 4);
                _planet.transport?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                _planet.defenseSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                _planet.digitalSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                DeepProfiler.EndSample(DPEntry.PowerConsumer, workerThread.threadIndex);
            }
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerThread.threadIndex);
        }
        else if (_workType == WorkType.Power && _planet.powerSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerThread.threadIndex);
            _planet.powerSystem.multithreadPlayerPos = playerPosition;
            _planet.powerSystem?.GameTick(time, isActive, multithreaded: true, workerThread.threadIndex);
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerThread.threadIndex);
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
            DeepProfiler.BeginMajorSample(DPEntry.Lab, workerThread.threadIndex);
            lock (singleThreadedCodeLock)
            {
                _planet.factorySystem!.GameTickLabResearchMode(time, isActive);
            }
            DeepProfiler.EndMajorSample(DPEntry.Lab, workerThread.threadIndex);
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
            DeepProfiler.BeginSample(DPEntry.Station, workerThread.threadIndex);
            _planet.transport.GameTick_InputFromBelt(time);
            DeepProfiler.EndSample(DPEntry.Station, workerThread.threadIndex);
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
            DeepProfiler.BeginSample(DPEntry.Storage, workerThread.threadIndex);
            _planet.factoryStorage.GameTickStorage(time, isActive);
            DeepProfiler.EndSample(DPEntry.Storage, workerThread.threadIndex);

            DeepProfiler.BeginSample(DPEntry.FluidTank, workerThread.threadIndex);
            _planet.factoryStorage.GameTickTank();
            DeepProfiler.EndSample(DPEntry.FluidTank, workerThread.threadIndex);
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
            DeepProfiler.BeginSample(DPEntry.Station, workerThread.threadIndex);
            int stationPilerLevel = GameMain.history.stationPilerLevel;
            _planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
            DeepProfiler.EndSample(DPEntry.Station, workerThread.threadIndex);
        }
        else if (_workType == WorkType.SandboxMode && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerThread.threadIndex);
            lock (singleThreadedCodeLock)
            {
                _planet.transport.GameTick_SandboxMode(workerThread.threadIndex);
            }
            DeepProfiler.EndSample(DPEntry.Station, workerThread.threadIndex);
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
            DeepProfiler.BeginSample(DPEntry.DigitalSystem, workerThread.threadIndex);
            _planet.digitalSystem.GameTick(isActive);
            DeepProfiler.EndSample(DPEntry.DigitalSystem, workerThread.threadIndex);
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
