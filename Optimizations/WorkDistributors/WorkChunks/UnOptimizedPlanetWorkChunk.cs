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

    public static SingleWorkLeaf[] CreateDuplicateChunksInWorkLeafs(PlanetFactory planet, WorkType workType, int count)
    {
        SingleWorkLeaf[] workLeafs = new SingleWorkLeaf[count];
        for (int i = 0; i < workLeafs.Length; i++)
        {
            workLeafs[i] = new SingleWorkLeaf(new UnOptimizedPlanetWorkChunk(planet, workType, i, count));
        }

        return workLeafs;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool isActive = localPlanet == _planet.planet;
        if (_workType == WorkType.BeforePower)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
            {
                DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
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
            OptimizedStarCluster.ThreadSafeGameTickLabResearchMode(_planet, time, isActive);
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
            DeepProfiler.BeginSample(DPEntry.Transport, workerIndex, 99L);
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

            ParallelInserterGameTick(workerIndex, time, isActive);
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
            ParallelCargoPathsGameTick(workerIndex);
        }
        else if (_workType == WorkType.Splitter && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Splitter, workerIndex);
            _planet.cargoTraffic.SplitterGameTick(time);
            DeepProfiler.EndSample(DPEntry.Splitter, workerIndex);
        }
        else if (_workType == WorkType.CargoTrafficMisc && _planet.cargoTraffic != null)
        {
            ParallelCargoTrafficMiscGameTick(workerIndex);
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
            ParallelPresentCargoPathsGameTick(workerIndex);
        }
        else if (_workType == WorkType.Digital && _planet.digitalSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.DigitalSystem, workerIndex);
            _planet.digitalSystem.GameTick(isActive);
            DeepProfiler.EndSample(DPEntry.DigitalSystem, workerIndex);
        }
    }

    private void ParallelInserterGameTick(int workerIndex, long time, bool isActive)
    {
        InserterComponent[] inserterPool = _planet.factorySystem.inserterPool;
        PowerSystem powerSystem = _planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        CargoTraffic cargoTraffic = _planet.cargoTraffic;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        int[][] entityNeeds = _planet.entityNeeds;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = _planet.entityPool;
        BeltComponent[] beltPool = _planet.cargoTraffic.beltPool;
        bool isTimeForOffsetCorrection = time % 60 == 0;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.inserterCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.Inserter, workerIndex);
        for (int i = startIndex; i < startIndex + workLength; i++)
        {
            ref InserterComponent component = ref inserterPool[i];
            if (component.id != i)
            {
                continue;
            }

            float power = networkServes[consumerPool[component.pcId].networkId];
            if (isTimeForOffsetCorrection)
            {
                component.InternalOffsetCorrection(entityPool, cargoTraffic, beltPool);
            }
            if (component.bidirectional)
            {
                component.InternalUpdate_Bidirectional(_planet, entityNeeds, entityAnimPool, power, isActive);
            }
            else if (isActive)
            {
                component.InternalUpdate(_planet, entityNeeds, entityAnimPool, power);
            }
            else
            {
                component.InternalUpdateNoAnim(_planet, entityNeeds, power);
            }
            component.SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Inserter, workerIndex);
    }

    private void ParallelCargoPathsGameTick(int workerIndex)
    {
        CargoPath[] pathPool = _planet.cargoTraffic.pathPool;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.cargoTraffic.pathCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.Belt, workerIndex);
        for (int i = startIndex; i < startIndex + workLength; i++)
        {
            CargoPath cargoPath = pathPool[i];
            if (cargoPath == null ||
                cargoPath.id != i)
            {
                continue;
            }

            cargoPath.Update();
        }
        DeepProfiler.EndSample(DPEntry.Belt, workerIndex);
    }

    private void ParallelCargoTrafficMiscGameTick(int workerIndex)
    {
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        DigitalSystem digitalSystem = _planet.digitalSystem;
        EntityData[] entityPool = _planet.entityPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        CargoTraffic cargoTraffic = _planet.cargoTraffic;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;

        if (cargoTraffic.monitorCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Monitor, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.monitorCursor, _maxWorkCount, _workIndex);
            MonitorComponent[] monitorPool = cargoTraffic.monitorPool;
            for (int i = startIndex; i < startIndex + workLength; i++)
            {
                ref MonitorComponent component = ref monitorPool[i];
                if (component.id != i)
                {
                    continue;
                }

                component.InternalUpdate(cargoTraffic, sandboxToolsEnabled, entityPool, digitalSystem, entityAnimPool);
                component.SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Monitor, workerIndex);
        }

        if (cargoTraffic.spraycoaterCursor > 0)
        {
            int[] consumeRegister = GameMain.statistics.production.factoryStatPool[_planet.index].consumeRegister;
            DeepProfiler.BeginSample(DPEntry.Spraycoater, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.spraycoaterCursor, _maxWorkCount, _workIndex);
            SpraycoaterComponent[] spraycoaterPool = cargoTraffic.spraycoaterPool;
            for (int i = startIndex; i < startIndex + workLength; i++)
            {
                ref SpraycoaterComponent component = ref spraycoaterPool[i];
                if (component.id != i)
                {
                    continue;
                }

                component.InternalUpdate(cargoTraffic, entityAnimPool, consumeRegister);
                component.SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Spraycoater, workerIndex);
        }

        if (cargoTraffic.pilerCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Piler, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.pilerCursor, _maxWorkCount, _workIndex);
            PilerComponent[] pilerPool = cargoTraffic.pilerPool;
            for (int i = startIndex; i < startIndex + workLength; i++)
            {
                ref PilerComponent component = ref pilerPool[i];
                if (component.id != i)
                {
                    continue;
                }

                pilerPool[i].InternalUpdate(cargoTraffic, entityAnimPool);
                pilerPool[i].SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Piler, workerIndex);
        }
    }

    private void ParallelPresentCargoPathsGameTick(int workerIndex)
    {
        CargoPath[] pathPool = _planet.cargoTraffic.pathPool;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.cargoTraffic.pathCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.CargoPresent, workerIndex);
        for (int i = startIndex; i < startIndex + workLength; i++)
        {
            CargoPath cargoPath = pathPool[i];
            if (cargoPath == null ||
                cargoPath.id != i)
            {
                continue;
            }

            cargoPath.PresentCargos();
        }
        DeepProfiler.EndSample(DPEntry.CargoPresent, workerIndex);
    }

    private static (int startIndex, int workLength) GetWorkChunkIndices(int totalLength, int maxWorkCount, int workIndex)
    {
        int workChunkLength = ((totalLength + maxWorkCount - 1) / maxWorkCount);
        int startIndex = workChunkLength * workIndex;
        int workLength = Math.Min(workChunkLength, totalLength - startIndex);
        if (workLength <= 0)
        {
            return (0, 0);
        }
        return (startIndex, workLength);
    }
}
