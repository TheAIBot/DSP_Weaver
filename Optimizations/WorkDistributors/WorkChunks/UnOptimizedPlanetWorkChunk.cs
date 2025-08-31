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
            _planet.powerSystem.GameTick(time, isActive, multithreaded: true, workerThread.threadIndex);
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
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Assembler} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Assembler, workerThread.threadIndex);
            _planet.factorySystem.GameTick(time, isActive);
            DeepProfiler.EndSample(DPEntry.Assembler, workerThread.threadIndex);
            DeepProfiler.BeginSample(DPEntry.Lab, workerThread.threadIndex);
            _planet.factorySystem.GameTickLabProduceMode(time, isActive);
            DeepProfiler.EndSample(DPEntry.Lab, workerThread.threadIndex);
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
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.LabOutput2NextData} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Lab, workerThread.threadIndex);
            _planet.factorySystem.GameTickLabOutputToNext(time, isActive);
            DeepProfiler.EndSample(DPEntry.Lab, workerThread.threadIndex);
        }
        else if (_workType == WorkType.TransportData && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Transport, workerThread.threadIndex);
            _planet.transport.GameTick(time, isActive, multithreaded: false, -1);
            DeepProfiler.EndSample(DPEntry.Transport, workerThread.threadIndex); ;
        }
        else if (_workType == WorkType.InputFromBelt && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerThread.threadIndex);
            _planet.transport.GameTick_InputFromBelt(time);
            DeepProfiler.EndSample(DPEntry.Station, workerThread.threadIndex);
        }
        else if (_workType == WorkType.InserterData)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.InserterData} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Inserter, workerThread.threadIndex);
            _planet.factorySystem.GameTickInserters(time, isActive);
            DeepProfiler.EndSample(DPEntry.Inserter, workerThread.threadIndex);
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
            DeepProfiler.BeginSample(DPEntry.Belt, workerThread.threadIndex);
            _planet.cargoTraffic.CargoPathsGameTickSync();
            DeepProfiler.EndSample(DPEntry.Belt, workerThread.threadIndex);
        }
        else if (_workType == WorkType.Splitter && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Splitter, workerThread.threadIndex);
            _planet.cargoTraffic.SplitterGameTick(time);
            DeepProfiler.EndSample(DPEntry.Splitter, workerThread.threadIndex);
        }
        else if (_workType == WorkType.Monitor && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Monitor, workerThread.threadIndex);
            _planet.cargoTraffic.MonitorGameTick();
            DeepProfiler.EndSample(DPEntry.Monitor, workerThread.threadIndex);
        }
        else if (_workType == WorkType.Spraycoater && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Spraycoater, workerThread.threadIndex);
            _planet.cargoTraffic.SpraycoaterGameTick();
            DeepProfiler.EndSample(DPEntry.Spraycoater, workerThread.threadIndex);
        }
        else if (_workType == WorkType.Piler && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Piler, workerThread.threadIndex);
            _planet.cargoTraffic.PilerGameTick();
            DeepProfiler.EndSample(DPEntry.Piler, workerThread.threadIndex);
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
        else if (_workType == WorkType.PresentCargoPathsData && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.CargoPresent, workerThread.threadIndex);
            _planet.cargoTraffic.PresentCargoPathsSync();
            DeepProfiler.EndSample(DPEntry.CargoPresent, workerThread.threadIndex);
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
