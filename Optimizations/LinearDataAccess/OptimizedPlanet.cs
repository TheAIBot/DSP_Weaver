using System;
using System.Collections.Generic;
using System.Threading;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Fractionators;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.Miners;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Spraycoaters;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

namespace Weaver.Optimizations.LinearDataAccess;

internal interface IWorkChunk
{
    void Execute(WorkerTimings workerTimings, long time);
    void TieToWorkStep(WorkStep workStep);
    bool Complete();

    void CompleteStep();
}

internal sealed class PlanetWideBeforePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWideBeforePower(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.BeforePowerStep(time);
        workerTimings.RecordTime(WorkType.BeforePower);
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

internal sealed class SubFactoryBeforePower : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedPowerSystem _optimizedPowerSystem;
    private readonly OptimizedSubFactory _subFactory;
    private WorkStep _workStep;

    public SubFactoryBeforePower(PlanetFactory planet,
                                 OptimizedPowerSystem optimizedPowerSystem,
                                 OptimizedSubFactory subFactory)
    {
        _planet = planet;
        _optimizedPowerSystem = optimizedPowerSystem;
        _subFactory = subFactory;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPowerSystem.BeforePower(_planet, _subFactory);
        workerTimings.RecordTime(WorkType.BeforePower);
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

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWidePower(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.PowerStep(time);
        workerTimings.RecordTime(WorkType.Power);
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

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;
    private WorkStep _workStep;

    public SubFactoryGameTick(OptimizedSubFactory subFactory)
    {
        _subFactory = subFactory;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        _subFactory.GameTick(workerTimings, time);
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

internal sealed class PlanetWideTransport : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWideTransport(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.TransportStep(time);
        workerTimings.RecordTime(WorkType.TransportData);
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

internal sealed class PlanetWideDigitalSystem : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWideDigitalSystem(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.DigitalSystemStep();
        workerTimings.RecordTime(WorkType.Digital);
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

internal sealed class WorkStep : IDisposable
{
    private readonly ManualResetEventSlim _waitForCompletion;
    private readonly IWorkChunk[] _workChunks;
    private int _scheduledCount;
    private int _completedCount;

    public WorkStep(IWorkChunk[] workChunks)
    {
        _waitForCompletion = new(false);
        _workChunks = workChunks;
        _scheduledCount = 0;
        _completedCount = 0;

        foreach (IWorkChunk workChunk in workChunks)
        {
            workChunk.TieToWorkStep(this);
        }
    }

    public IWorkChunk? TryGetWork(out bool canNoLongerProvideWork)
    {
        if (_scheduledCount >= _workChunks.Length)
        {
            canNoLongerProvideWork = true;
            return null;
        }

        int workChunkIndex = Interlocked.Increment(ref _scheduledCount) - 1;
        if (workChunkIndex >= _workChunks.Length)
        {
            canNoLongerProvideWork = true;
            return null;
        }

        canNoLongerProvideWork = false;
        return _workChunks[workChunkIndex];
    }

    public void WaitForCompletion()
    {
        _waitForCompletion.Wait();
    }

    public bool CompleteWorkChunk()
    {
        int completedWorkChunks = Interlocked.Increment(ref _completedCount);
        if (completedWorkChunks > _workChunks.Length)
        {
            throw new InvalidOperationException("");
        }

        return completedWorkChunks == _workChunks.Length;
    }

    public void CompleteStep()
    {
        _waitForCompletion.Set();
    }

    public void Reset()
    {
        _waitForCompletion.Reset();
        _scheduledCount = 0;
        _completedCount = 0;
    }

    public void Dispose()
    {
        _waitForCompletion.Dispose();
    }
}

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

internal sealed class OptimizedSubFactory
{
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public AssemblerExecutor _assemblerExecutor;

    public MinerExecutor _minerExecutor;

    public EjectorExecutor _ejectorExecutor;

    public SiloExecutor _siloExecutor;

    //private NetworkIdAndState<LabState>[] _labProduceNetworkIdAndStates;
    public ProducingLabExecutor _producingLabExecutor;
    public NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    public OptimizedProducingLab[] _optimizedProducingLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public Dictionary<int, int> _producingLabIdToOptimizedIndex;

    public ResearchingLabExecutor _researchingLabExecutor;
    public NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    public OptimizedResearchingLab[] _optimizedResearchingLabs;
    public Dictionary<int, int> _researchingLabIdToOptimizedIndex;

    public MonitorExecutor _monitorExecutor;
    public SpraycoaterExecutor _spraycoaterExecutor;
    public PilerExecutor _pilerExecutor;

    public FractionatorExecutor _fractionatorExecutor;

    public StationExecutor _stationExecutor;
    public DispenserExecutor _dispenserExecutor;

    public TankExecutor _tankExecutor;

    public BeltExecutor _beltExecutor;
    public SplitterExecutor _splitterExecutor;

    public OptimizedSubFactory(PlanetFactory planet, StarClusterResearchManager starClusterResearchManager)
    {
        _planet = planet;
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void Save()
    {
        _optimizedBiInserterExecutor.Save(_planet);
        _optimizedInserterExecutor.Save(_planet);
        _assemblerExecutor.Save(_planet);
        _producingLabExecutor.Save(_planet);
        _researchingLabExecutor.Save(_planet);
        _spraycoaterExecutor.Save(_planet);
        _fractionatorExecutor.Save(_planet);
    }

    public void Initialize(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        optimizedPowerSystemBuilder.AddSubFactory(this);

        InitializeAssemblers(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeMiners(subFactoryGraph);
        InitializeEjectors(subFactoryGraph);
        InitializeSilos(subFactoryGraph);
        InitializeLabAssemblers(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeResearchingLabs(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeInserters(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeMonitors(subFactoryGraph);
        InitializeSpraycoaters(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializePilers(subFactoryGraph);
        InitializeFractionators(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeStations(subFactoryGraph);
        InitializeDispensers(subFactoryGraph);
        InitializeTanks(subFactoryGraph);
        InitializeBelts(subFactoryGraph);
        InitializeSplitters(subFactoryGraph);
    }

    private void InitializeInserters(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedBiInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder());

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder());
    }

    private void InitializeAssemblers(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
    }

    private void InitializeMiners(Graph subFactoryGraph)
    {
        _minerExecutor = new MinerExecutor();
        _minerExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeEjectors(Graph subFactoryGraph)
    {
        _ejectorExecutor = new EjectorExecutor();
        _ejectorExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeSilos(Graph subFactoryGraph)
    {
        _siloExecutor = new SiloExecutor();
        _siloExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeLabAssemblers(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor(_starClusterResearchManager);
        _researchingLabExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeMonitors(Graph subFactoryGraph)
    {
        _monitorExecutor = new MonitorExecutor();
        _monitorExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeSpraycoaters(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(_planet, this, subFactoryGraph, optimizedPowerSystemBuilder);
    }

    private void InitializePilers(Graph subFactoryGraph)
    {
        _pilerExecutor = new PilerExecutor();
        _pilerExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeFractionators(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _fractionatorExecutor = new FractionatorExecutor();
        _fractionatorExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
    }

    private void InitializeStations(Graph subFactoryGraph)
    {
        _stationExecutor = new StationExecutor();
        _stationExecutor.Initialize(subFactoryGraph);
    }

    private void InitializeDispensers(Graph subFactoryGraph)
    {
        _dispenserExecutor = new DispenserExecutor();
        _dispenserExecutor.Initialize(subFactoryGraph);
    }

    private void InitializeTanks(Graph subFactoryGraph)
    {
        _tankExecutor = new TankExecutor();
        _tankExecutor.Initialize(subFactoryGraph);
    }

    private void InitializeBelts(Graph subFactoryGraph)
    {
        _beltExecutor = new BeltExecutor();
        _beltExecutor.Initialize(subFactoryGraph);
    }

    private void InitializeSplitters(Graph subFactoryGraph)
    {
        _splitterExecutor = new SplitterExecutor();
        _splitterExecutor.Initialize(subFactoryGraph);
    }

    public void GameTick(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _minerExecutor.GameTick(_planet);
        _assemblerExecutor.GameTick(_planet);
        _fractionatorExecutor.GameTick(_planet);
        _ejectorExecutor.GameTick(_planet, time);
        _siloExecutor.GameTick(_planet);
        _producingLabExecutor.GameTickLabProduceMode(_planet);
        _producingLabExecutor.GameTickLabOutputToNext();
        workerTimings.RecordTime(WorkType.Assembler);

        workerTimings.StartTimer();
        _researchingLabExecutor.GameTickLabResearchMode(_planet);
        _researchingLabExecutor.GameTickLabOutputToNext();
        workerTimings.RecordTime(WorkType.LabResearchMode);

        workerTimings.StartTimer();
        _stationExecutor.InputFromBelt(_planet, time);
        workerTimings.RecordTime(WorkType.InputFromBelt);

        workerTimings.StartTimer();
        _optimizedBiInserterExecutor.GameTickInserters(_planet);
        _optimizedInserterExecutor.GameTickInserters(_planet);
        workerTimings.RecordTime(WorkType.InserterData);

        workerTimings.StartTimer();
        // Storage has no logic on planets the player isn't on which is why it is omitted
        _tankExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.Storage);

        workerTimings.StartTimer();
        _beltExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.CargoPathsData);

        workerTimings.StartTimer();
        _splitterExecutor.GameTick(_planet, time);
        workerTimings.RecordTime(WorkType.Splitter);

        workerTimings.StartTimer();
        _monitorExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.Monitor);

        workerTimings.StartTimer();
        _spraycoaterExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.Spraycoater);

        workerTimings.StartTimer();
        _pilerExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.Piler);

        workerTimings.StartTimer();
        _stationExecutor.OutputToBelt(_planet, time);
        workerTimings.RecordTime(WorkType.OutputToBelt);

        workerTimings.StartTimer();
        _stationExecutor.SandboxMode(_planet);
        _dispenserExecutor.SandboxMode(_planet);
        workerTimings.RecordTime(WorkType.SandboxMode);
    }

    public TypedObjectIndex GetAsGranularTypedObjectIndex(int index, PlanetFactory planet)
    {
        ref readonly EntityData entity = ref planet.entityPool[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            if (_assemblerExecutor._assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
            {
                return new TypedObjectIndex(EntityType.Assembler, optimizedAssemblerIndex);
            }

            if (_assemblerExecutor._unOptimizedAssemblerIds.Contains(entity.assemblerId))
            {
                return TypedObjectIndex.Invalid;
            }

            throw new InvalidOperationException("Failed to convert assembler id into optimized assembler id.");
        }
        else if (entity.ejectorId != 0)
        {
            return new TypedObjectIndex(EntityType.Ejector, entity.ejectorId);
        }
        else if (entity.siloId != 0)
        {
            return new TypedObjectIndex(EntityType.Silo, entity.siloId);
        }
        else if (entity.labId != 0)
        {
            if (planet.factorySystem.labPool[entity.labId].researchMode)
            {
                if (_researchingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    return new TypedObjectIndex(EntityType.ResearchingLab, optimizedLabIndex);
                }

                if (_researchingLabExecutor._unOptimizedLabIds.Contains(entity.labId))
                {
                    return TypedObjectIndex.Invalid;
                }

                throw new InvalidOperationException("Failed to convert researching lab id into optimized lab id.");

            }
            else
            {
                if (_producingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    return new TypedObjectIndex(EntityType.ProducingLab, optimizedLabIndex);
                }

                if (_producingLabExecutor._unOptimizedLabIds.Contains(entity.labId))
                {
                    return TypedObjectIndex.Invalid;
                }

                throw new InvalidOperationException("Failed to convert producing lab id into optimized lab id.");
            }
        }
        else if (entity.storageId != 0)
        {
            return new TypedObjectIndex(EntityType.Storage, entity.storageId);
        }
        else if (entity.stationId != 0)
        {
            return new TypedObjectIndex(EntityType.Station, entity.stationId);
        }
        else if (entity.powerGenId != 0)
        {
            return new TypedObjectIndex(EntityType.PowerGenerator, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new TypedObjectIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new TypedObjectIndex(EntityType.Inserter, entity.inserterId);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }

    public static int[]? GetEntityNeeds(PlanetFactory planet, int entityIndex)
    {
        ref readonly EntityData entity = ref planet.entityPool[entityIndex];
        if (entity.beltId != 0)
        {
            return null;
        }
        else if (entity.assemblerId != 0)
        {
            int[] needs = planet.factorySystem.assemblerPool[entity.assemblerId].needs;
            if (needs == null)
            {
                throw new InvalidOperationException("Need must not be null for assembler.");
            }

            return needs;
        }
        else if (entity.ejectorId != 0)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[entity.ejectorId];
            int[] needs = ejector.needs;
            if (needs == null)
            {
                ejector.needs = new int[6];
                planet.entityNeeds[ejector.entityId] = ejector.needs;
                needs = ejector.needs;
            }

            return needs;
        }
        else if (entity.siloId != 0)
        {
            ref SiloComponent silo = ref planet.factorySystem.siloPool[entity.siloId];
            int[] needs = silo.needs;
            if (needs == null)
            {
                silo.needs = new int[6];
                planet.entityNeeds[silo.entityId] = silo.needs;
                needs = silo.needs;
            }

            return needs;
        }
        else if (entity.labId != 0)
        {
            int[] needs = planet.factorySystem.labPool[entity.labId].needs;
            if (needs == null)
            {
                throw new InvalidOperationException("Need must not be null for lab.");
            }

            return needs;
        }
        else if (entity.storageId != 0)
        {
            return null;
        }
        else if (entity.stationId != 0)
        {
            int[] needs = planet.transport.stationPool[entity.stationId].needs;
            if (needs == null)
            {
                throw new InvalidOperationException("Need must not be null for station.");
            }

            return needs;
        }
        else if (entity.powerGenId != 0)
        {
            return null;
        }
        else if (entity.splitterId != 0)
        {
            return null;
        }
        else if (entity.inserterId != 0)
        {
            return null;
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}