using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class UnOptimizedPlanetWorkChunk : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly WorkType _workType;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;

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

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool isActive = localPlanet == _planet.planet;
        if (_workType == WorkType.BeforePower)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
            {
                DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
                _planet.factorySystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 4);
                _planet.cargoTraffic?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 4);
                _planet.transport?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                _planet.defenseSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                _planet.digitalSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                DeepProfiler.EndSample(DPEntry.PowerConsumer, workerIndex);
            }
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
        }
        else if (_workType == WorkType.Power && _planet.powerSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
            _planet.powerSystem.multithreadPlayerPos = playerPosition;
            _planet.powerSystem.GameTick(time, isActive, multithreaded: true, workerIndex);
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
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

            DeepProfiler.BeginSample(DPEntry.Assembler, workerIndex);
            _planet.factorySystem.GameTick(time, isActive);
            DeepProfiler.EndSample(DPEntry.Assembler, workerIndex);
            DeepProfiler.BeginSample(DPEntry.Lab, workerIndex);
            _planet.factorySystem.GameTickLabProduceMode(time, isActive);
            DeepProfiler.EndSample(DPEntry.Lab, workerIndex);
        }
        else if (_workType == WorkType.LabResearchMode)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Lab, workerIndex);
            lock (singleThreadedCodeLock)
            {
                _planet.factorySystem!.GameTickLabResearchMode(time, isActive);
            }
            DeepProfiler.EndMajorSample(DPEntry.Lab, workerIndex);
        }
        else if (_workType == WorkType.LabOutput2NextData)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.LabOutput2NextData} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Lab, workerIndex);
            _planet.factorySystem.GameTickLabOutputToNext(time, isActive);
            DeepProfiler.EndSample(DPEntry.Lab, workerIndex);
        }
        else if (_workType == WorkType.TransportData && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Transport, workerIndex);
            _planet.transport.GameTick(time, isActive, multithreaded: false, -1);
            DeepProfiler.EndSample(DPEntry.Transport, workerIndex); ;
        }
        else if (_workType == WorkType.InputFromBelt && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            _planet.transport.GameTick_InputFromBelt(time);
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.InserterData)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.InserterData} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Inserter, workerIndex);
            _planet.factorySystem.GameTickInserters(time, isActive);
            DeepProfiler.EndSample(DPEntry.Inserter, workerIndex);
        }
        else if (_workType == WorkType.Storage && _planet.factoryStorage != null)
        {
            DeepProfiler.BeginSample(DPEntry.Storage, workerIndex);
            _planet.factoryStorage.GameTickStorage(time, isActive);
            DeepProfiler.EndSample(DPEntry.Storage, workerIndex);

            DeepProfiler.BeginSample(DPEntry.FluidTank, workerIndex);
            _planet.factoryStorage.GameTickTank();
            DeepProfiler.EndSample(DPEntry.FluidTank, workerIndex);
        }
        else if (_workType == WorkType.CargoPathsData)
        {
            DeepProfiler.BeginSample(DPEntry.Belt, workerIndex);
            _planet.cargoTraffic.CargoPathsGameTickSync();
            DeepProfiler.EndSample(DPEntry.Belt, workerIndex);
        }
        else if (_workType == WorkType.Splitter && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Splitter, workerIndex);
            _planet.cargoTraffic.SplitterGameTick(time);
            DeepProfiler.EndSample(DPEntry.Splitter, workerIndex);
        }
        else if (_workType == WorkType.Monitor && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Monitor, workerIndex);
            _planet.cargoTraffic.MonitorGameTick();
            DeepProfiler.EndSample(DPEntry.Monitor, workerIndex);
        }
        else if (_workType == WorkType.Spraycoater && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Spraycoater, workerIndex);
            _planet.cargoTraffic.SpraycoaterGameTick();
            DeepProfiler.EndSample(DPEntry.Spraycoater, workerIndex);
        }
        else if (_workType == WorkType.Piler && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Piler, workerIndex);
            _planet.cargoTraffic.PilerGameTick();
            DeepProfiler.EndSample(DPEntry.Piler, workerIndex);
        }
        else if (_workType == WorkType.OutputToBelt && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            int stationPilerLevel = GameMain.history.stationPilerLevel;
            _planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.SandboxMode && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            lock (singleThreadedCodeLock)
            {
                _planet.transport.GameTick_SandboxMode(workerIndex);
            }
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.PresentCargoPathsData && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.CargoPresent, workerIndex);
            _planet.cargoTraffic.PresentCargoPathsSync();
            DeepProfiler.EndSample(DPEntry.CargoPresent, workerIndex);
        }
        else if (_workType == WorkType.Digital && _planet.digitalSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.DigitalSystem, workerIndex);
            _planet.digitalSystem.GameTick(isActive);
            DeepProfiler.EndSample(DPEntry.DigitalSystem, workerIndex);
        }
    }
}
