using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private OptimizedSubFactory[] _subFactories;
    private OptimizedPowerSystem _optimizedPowerSystem;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    private WorkStep[] _workSteps;
    private int _workStepsParallelism;

    public OptimizedPlanet(PlanetFactory planet, StarClusterResearchManager starClusterResearchManager)
    {
        _planet = planet;
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void Save()
    {
        foreach (var subFactory in _subFactories)
        {
            subFactory.Save();
        }

        Status = OptimizedPlanetStatus.Stopped;

        _workSteps = null;
        _workStepsParallelism = -1;
    }

    public void Initialize()
    {
        List<Graph> subFactoryGraphs = Graphifier.ToGraphs(_planet);
        Graphifier.CombineSmallGraphs(subFactoryGraphs);

        WeaverFixes.Logger.LogMessage($"Sub Factory count: {subFactoryGraphs.Count}");

        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(_planet.powerSystem);

        _subFactories = new OptimizedSubFactory[subFactoryGraphs.Count];
        for (int i = 0; i < _subFactories.Length; i++)
        {
            _subFactories[i] = new OptimizedSubFactory(_planet, _starClusterResearchManager);
            _subFactories[i].Initialize(subFactoryGraphs[i], optimizedPowerSystemBuilder);
        }

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();

        Status = OptimizedPlanetStatus.Running;

        _workSteps = null;
        _workStepsParallelism = -1;
    }

    public WorkStep[] GetMultithreadedWork(int maxParallelism)
    {
        if (_workStepsParallelism != maxParallelism)
        {
            _workSteps = CreateMultithreadedWork(maxParallelism);
            _workStepsParallelism = maxParallelism;
        }

        return _workSteps;
    }

    private WorkStep[] CreateMultithreadedWork(int maxParallelism)
    {
        if (Status == OptimizedPlanetStatus.Stopped)
        {
            // Temp while i figure out another issue
            return [];
            //throw new InvalidOperationException("Does currently not support simulating non optimized planets.");
            //return CreateParallelWorkForNonRunningOptimizedPlanet(maxParallelism);
        }

        if (_subFactories.Length == 0)
        {
            return [];
        }

        List<WorkStep> workSteps = [];

        List<IWorkChunk> beforePowerChunks = [];
        beforePowerChunks.Add(new PlanetWideBeforePower(this));
        foreach (var subFactory in _subFactories)
        {
            beforePowerChunks.Add(new SubFactoryBeforePower(_planet, _optimizedPowerSystem, subFactory));
        }
        workSteps.Add(new WorkStep(beforePowerChunks.ToArray()));

        workSteps.Add(new WorkStep([new PlanetWidePower(this)]));

        List<IWorkChunk> gameTickChunks = [];
        foreach (var subFactory in _subFactories)
        {
            gameTickChunks.Add(new SubFactoryGameTick(subFactory));
        }
        workSteps.Add(new WorkStep(gameTickChunks.ToArray()));

        workSteps.Add(new WorkStep([new PlanetWideTransport(this)]));

        workSteps.Add(new WorkStep([new PlanetWideDigitalSystem(this)]));

        return workSteps.ToArray();
    }

    public void BeforePowerStep(long time)
    {
        _planet.transport.GameTickBeforePower(time, false);
        _planet.defenseSystem.GameTickBeforePower(time, false);
        _planet.digitalSystem.GameTickBeforePower(time, false);
    }

    public void PowerStep(long time)
    {
        _optimizedPowerSystem.GameTick(_planet, time);
    }

    public void TransportStep(long time)
    {
        _planet.transport.GameTick(time, false, false);
    }

    public void DigitalSystemStep()
    {
        _planet.digitalSystem.GameTick(false);
    }

    private WorkTracker[] CreateParallelWorkForNonRunningOptimizedPlanet(int maxParallelism)
    {
        List<WorkTracker> work = [];

        int minerCount = _planet.factorySystem.minerCursor;
        int assemblerCount = _planet.factorySystem.assemblerCursor;
        int fractionatorCount = _planet.factorySystem.fractionatorCursor;
        int ejectorCount = _planet.factorySystem.ejectorCursor;
        int siloCount = _planet.factorySystem.siloCursor;

        int monitorCount = _planet.cargoTraffic.monitorCursor;
        int spraycoaterCount = _planet.cargoTraffic.spraycoaterCursor;
        int pilerCount = _planet.cargoTraffic.pilerCursor;
        int splitterCount = _planet.cargoTraffic.splitterCursor;
        int cargoPathCount = _planet.cargoTraffic.pathCursor;

        int stationCount = _planet.transport.stationCursor;
        int dispenserCount = _planet.transport.dispenserCursor;
        int transportEntities = stationCount + dispenserCount;

        int turretCount = _planet.defenseSystem.turrets.cursor;
        int fieldGeneratorCount = _planet.defenseSystem.fieldGenerators.cursor;
        int battleBaseCount = _planet.defenseSystem.battleBases.cursor;

        int markerCount = _planet.digitalSystem.markers.cursor;

        int inserterCount = _planet.factorySystem.inserterCursor;

        int producingLabCount = _planet.factorySystem.labCursor;
        int researchingLabCount = _planet.factorySystem.labCursor;
        int labCount = _planet.factorySystem.labCursor;

        int storageCount = _planet.factoryStorage.storageCursor;
        int tankCount = _planet.factoryStorage.tankCursor;

        int totalEntities = minerCount +
                            assemblerCount +
                            fractionatorCount +
                            ejectorCount +
                            siloCount +
                            monitorCount +
                            spraycoaterCount +
                            pilerCount +
                            stationCount +
                            dispenserCount +
                            turretCount +
                            fieldGeneratorCount +
                            battleBaseCount +
                            markerCount;

        const int minimumWorkPerCore = 5_000;
        int beforePowerWorkCount = ((totalEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        beforePowerWorkCount = Math.Min(beforePowerWorkCount, maxParallelism);
        if (beforePowerWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.BeforePower, beforePowerWorkCount));
        }

        int powerNetworkCount = _planet.powerSystem.netCursor;
        if (powerNetworkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Power, 1));
        }

        if (true)
        {
            work.Add(new WorkTracker(WorkType.Construction, 1));
        }

        if (true)
        {
            work.Add(new WorkTracker(WorkType.CheckBefore, 1));
        }


        int assemblerStepEntityCount = minerCount +
                                       assemblerCount +
                                       fractionatorCount +
                                       ejectorCount +
                                       siloCount +
                                       producingLabCount;
        int assemblerWorkCount = ((assemblerStepEntityCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        assemblerWorkCount = Math.Min(assemblerWorkCount, maxParallelism);
        if (assemblerWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Assembler, assemblerWorkCount));
        }

        if (researchingLabCount > 0)
        {
            work.Add(new WorkTracker(WorkType.LabResearchMode, 1));
        }

        int labOutput2NextWorkCount = ((labCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        labOutput2NextWorkCount = Math.Min(labOutput2NextWorkCount, maxParallelism);
        if (labOutput2NextWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.LabOutput2NextData, labOutput2NextWorkCount));
        }

        if (transportEntities > 0)
        {
            work.Add(new WorkTracker(WorkType.TransportData, 1));
        }

        if (stationCount > 0)
        {
            work.Add(new WorkTracker(WorkType.InputFromBelt, 1));
        }

        int inserterWorkCount = ((inserterCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        inserterWorkCount = Math.Min(inserterWorkCount, maxParallelism);
        if (inserterWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.InserterData, inserterWorkCount));
        }

        if ((storageCount + tankCount) > 0)
        {
            work.Add(new WorkTracker(WorkType.Storage, 1));
        }

        int cargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        cargoPathsWorkCount = Math.Min(cargoPathsWorkCount, maxParallelism);
        if (cargoPathsWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.CargoPathsData, cargoPathsWorkCount));
        }

        if (splitterCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Splitter, 1));
        }


        if (monitorCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Monitor, 1));
        }

        if (spraycoaterCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Spraycoater, 1));
        }

        if (pilerCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Piler, 1));
        }

        if (stationCount > 0)
        {
            work.Add(new WorkTracker(WorkType.OutputToBelt, 1));
        }

        int sandboxModeWorkCount = ((transportEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        sandboxModeWorkCount = Math.Min(sandboxModeWorkCount, maxParallelism);
        if (GameMain.sandboxToolsEnabled && sandboxModeWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.SandboxMode, sandboxModeWorkCount));
        }

        int presentCargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        presentCargoPathsWorkCount = Math.Min(presentCargoPathsWorkCount, maxParallelism);
        if (presentCargoPathsWorkCount > 0)
        {
            work.Add(new WorkTracker(WorkType.PresentCargoPathsData, presentCargoPathsWorkCount));
        }

        if (markerCount > 0)
        {
            work.Add(new WorkTracker(WorkType.Digital, 1));
        }

        return work.ToArray();
    }
}