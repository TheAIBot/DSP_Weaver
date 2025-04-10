using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Fractionators;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Spraycoaters;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private readonly PlanetFactory _planet;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public AssemblerExecutor _assemblerExecutor;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

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

    public SpraycoaterExecutor _spraycoaterExecutor;

    public FractionatorExecutor _fractionatorExecutor;

    public OptimizedPowerSystem _optimizedPowerSystem;

    public OptimizedPlanet(PlanetFactory planet)
    {
        _planet = planet;
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

        Status = OptimizedPlanetStatus.Stopped;
    }

    public void Initialize()
    {
        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(_planet.powerSystem);

        InitializeAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeMiners(_planet);
        InitializeEjectors(_planet);
        InitializeLabAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeResearchingLabs(_planet, optimizedPowerSystemBuilder);
        InitializeInserters(_planet, optimizedPowerSystemBuilder);
        InitializeSpraycoaters(_planet, optimizedPowerSystemBuilder);
        InitializeFractionators(_planet, optimizedPowerSystemBuilder);

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();

        Status = OptimizedPlanetStatus.Running;
    }

    private void InitializeInserters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedBiInserterExecutor.Initialize(planet, this, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder());

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedInserterExecutor.Initialize(planet, this, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder());
    }

    private void InitializeAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(planet, optimizedPowerSystemBuilder);
    }

    private void InitializeMiners(PlanetFactory planet)
    {
        int[] minerNetworkIds = new int[planet.factorySystem.minerCursor];

        for (int i = 0; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[i];
            if (miner.id != i)
            {
                continue;
            }

            minerNetworkIds[i] = planet.powerSystem.consumerPool[miner.pcId].networkId;

        }

        _minerNetworkIds = minerNetworkIds;
    }

    private void InitializeEjectors(PlanetFactory planet)
    {
        int[] ejectorNetworkIds = new int[planet.factorySystem.ejectorCursor];

        for (int i = 0; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[i];
            if (ejector.id != i)
            {
                continue;
            }

            ejectorNetworkIds[i] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }

    private void InitializeLabAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor();
        _researchingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeSpraycoaters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(planet, optimizedPowerSystemBuilder);
    }

    private void InitializeFractionators(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _fractionatorExecutor = new FractionatorExecutor();
        _fractionatorExecutor.Initialize(planet, optimizedPowerSystemBuilder);
    }

    public void GameTick(PlanetFactory planet, long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        EntityData[] entityPool = planet.entityPool;
        VeinData[] veinPool = planet.veinPool;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SignData[] entitySignPool = planet.entitySignPool;
        int[][] entityNeeds = planet.entityNeeds;
        FactorySystem factorySystem = planet.factorySystem;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        AstroData[] astroPoses = null;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            float num2;
            float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
            if (num2 < 5f / 12f)
            {
                num2 = 5f / 12f;
            }
            float num4 = history.miningCostRate;
            float miningSpeedScale = history.miningSpeedScale;
            float num5 = history.miningCostRate * 0.40111667f / num2;
            if (num3 > 99.5f)
            {
                num4 = 0f;
                num5 = 0f;
            }

            int[] minerNetworkIds = _minerNetworkIds;
            int num6 = MinerComponent.InsufficientWarningThresAmount(num3, num4);
            for (int i = _start; i < _end; i++)
            {
                if (factorySystem.minerPool[i].id != i)
                {
                    continue;
                }

                float num7 = networkServes[minerNetworkIds[i]];
                factorySystem.minerPool[i].InternalUpdate(planet, veinPool, num7, (factorySystem.minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
            }
        }

        _assemblerExecutor.GameTick(planet, time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        _fractionatorExecutor.GameTick(planet, time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        lock (factorySystem.ejectorPool)
        {
            if (factorySystem.ejectorCursor - factorySystem.ejectorRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSwarm swarm = null;
        if (factorySystem.factory.dysonSphere != null)
        {
            swarm = factorySystem.factory.dysonSphere.swarm;
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.ejectorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            int[] ejectorNetworkIds = _ejectorNetworkIds;
            for (int m = _start; m < _end; m++)
            {
                if (factorySystem.ejectorPool[m].id == m)
                {
                    float power3 = networkServes[ejectorNetworkIds[m]];
                    factorySystem.ejectorPool[m].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);
                }
            }
        }
        lock (factorySystem.siloPool)
        {
            if (factorySystem.siloCursor - factorySystem.siloRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        bool flag3 = dysonSphere != null && dysonSphere.autoNodeCount > 0;
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.siloCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            return;
        }
        for (int n = _start; n < _end; n++)
        {
            if (factorySystem.siloPool[n].id == n)
            {
                int entityId6 = factorySystem.siloPool[n].entityId;
                uint num12 = 0u;
                float power4 = networkServes[consumerPool[factorySystem.siloPool[n].pcId].networkId];
                num12 = factorySystem.siloPool[n].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
                entityAnimPool[entityId6].state = num12;
                entityNeeds[entityId6] = factorySystem.siloPool[n].needs;
                if (entitySignPool[entityId6].signType == 0 || entitySignPool[entityId6].signType > 3)
                {
                    entitySignPool[entityId6].signType = ((!flag3) ? 9u : 0u);
                }
            }
        }
    }

    public static void ParallelSpraycoaterLogic()
    {
        PerformanceMonitor.BeginSample(ECpuWorkEntry.Belt);

        PlanetFactory? localFactory = GameMain.localPlanet?.factory;
        Parallel.For(0, GameMain.data.factoryCount, i =>
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet.cargoTraffic == null)
            {
                return;
            }

            OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(planet);
            if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
            {
                planet.cargoTraffic.SpraycoaterGameTick();
                return;
            }

            optimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(planet);
        });

        PerformanceMonitor.EndSample(ECpuWorkEntry.Belt);
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
            if (!_assemblerExecutor._assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
            {
                if (_assemblerExecutor._unOptimizedAssemblerIds.Contains(entity.assemblerId))
                {
                    return new TypedObjectIndex(EntityType.None, -1);
                }

                throw new InvalidOperationException("Failed to convert assembler id into optimized assembler id.");
            }

            return new TypedObjectIndex(EntityType.Assembler, optimizedAssemblerIndex);
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
                if (!_researchingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    if (_researchingLabExecutor._unOptimizedLabIds.Contains(entity.labId))
                    {
                        return new TypedObjectIndex(EntityType.None, -1);
                    }

                    throw new InvalidOperationException("Failed to convert researching lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ResearchingLab, optimizedLabIndex);
            }
            else
            {
                if (!_producingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    if (_producingLabExecutor._unOptimizedLabIds.Contains(entity.labId))
                    {
                        return new TypedObjectIndex(EntityType.None, -1);
                    }

                    throw new InvalidOperationException("Failed to convert producing lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ProducingLab, optimizedLabIndex);
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
            return planet.factorySystem.assemblerPool[entity.assemblerId].needs;
        }
        else if (entity.ejectorId != 0)
        {
            return planet.factorySystem.ejectorPool[entity.ejectorId].needs;
        }
        else if (entity.siloId != 0)
        {
            return planet.factorySystem.siloPool[entity.siloId].needs;
        }
        else if (entity.labId != 0)
        {
            return planet.factorySystem.labPool[entity.labId].needs;
        }
        else if (entity.storageId != 0)
        {
            return null;
        }
        else if (entity.stationId != 0)
        {
            return planet.transport.stationPool[entity.stationId].needs;
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

    public WorkTracker[] GetMultithreadedWork(int maxParallelism)
    {
        List<WorkTracker> work = [];

        int minerCount = _planet.factorySystem.minerCursor;
        int assemblerCount = Status == OptimizedPlanetStatus.Running ? _assemblerExecutor.AssemblerCount : _planet.factorySystem.assemblerCursor;
        int fractionatorCount = Status == OptimizedPlanetStatus.Running ? _fractionatorExecutor.FractionatorCount : _planet.factorySystem.fractionatorCursor;
        int ejectorCount = _planet.factorySystem.ejectorCursor;
        int siloCount = _planet.factorySystem.siloCursor;

        int monitorCount = _planet.cargoTraffic.monitorCursor;
        int spraycoaterCount = Status == OptimizedPlanetStatus.Running ? _spraycoaterExecutor.SpraycoaterCount : _planet.cargoTraffic.spraycoaterCursor;
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

        int inserterCount = Status == OptimizedPlanetStatus.Running ? (_optimizedBiInserterExecutor.InserterCount + _optimizedInserterExecutor.InserterCount) : _planet.factorySystem.inserterCursor;

        int producingLabCount = Status == OptimizedPlanetStatus.Running ? _producingLabExecutor.ProducingLabCount : _planet.factorySystem.labCursor;
        int researchingLabCount = Status == OptimizedPlanetStatus.Running ? _researchingLabExecutor.ResearchingLabCount : _planet.factorySystem.labCursor;
        int labCount = Status == OptimizedPlanetStatus.Running ? (producingLabCount + researchingLabCount) : _planet.factorySystem.labCursor;

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

        const int minimumWorkPerCore = 1_000;
        int beforePowerWorkCount = ((totalEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        beforePowerWorkCount = Math.Min(beforePowerWorkCount, maxParallelism);
        work.Add(new WorkTracker(beforePowerWorkCount));

        int powerNetworkCount = _planet.powerSystem.netCursor;
        work.Add(new WorkTracker(powerNetworkCount > 0 ? 1 : 0));

        work.Add(new WorkTracker(1));

        work.Add(new WorkTracker(1));

        int assemblerStepEntityCount = minerCount +
                                       assemblerCount +
                                       fractionatorCount +
                                       ejectorCount +
                                       siloCount +
                                       producingLabCount;
        int assemblerWorkCount = ((assemblerStepEntityCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        assemblerWorkCount = Math.Min(assemblerWorkCount, maxParallelism);
        work.Add(new WorkTracker(assemblerWorkCount));

        work.Add(new WorkTracker(researchingLabCount > 0 ? 1 : 0));

        int labOutput2NextWorkCount = ((labCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        labOutput2NextWorkCount = Math.Min(labOutput2NextWorkCount, maxParallelism);
        work.Add(new WorkTracker(labOutput2NextWorkCount));

        int transportWorkCount = ((transportEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        transportWorkCount = Math.Min(transportWorkCount, maxParallelism);
        work.Add(new WorkTracker(transportWorkCount));

        work.Add(new WorkTracker(stationCount > 0 ? 1 : 0));

        int inserterWorkCount = ((inserterCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        inserterWorkCount = Math.Min(inserterWorkCount, maxParallelism);
        work.Add(new WorkTracker(inserterWorkCount));

        work.Add(new WorkTracker((storageCount + tankCount) > 0 ? 1 : 0));

        int cargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        cargoPathsWorkCount = Math.Min(cargoPathsWorkCount, maxParallelism);
        work.Add(new WorkTracker(cargoPathsWorkCount));

        work.Add(new WorkTracker(splitterCount > 0 ? 1 : 0));

        work.Add(new WorkTracker(monitorCount > 0 ? 1 : 0));

        work.Add(new WorkTracker(spraycoaterCount > 0 ? 1 : 0));

        work.Add(new WorkTracker(pilerCount > 0 ? 1 : 0));
        //int splitterWorkCount = ((splitterCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        //splitterWorkCount = Math.Min(splitterWorkCount, maxParallelism);
        //work.Add(new WorkTracker(splitterWorkCount));

        //int monitorWorkCount = ((monitorCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        //monitorWorkCount = Math.Min(monitorWorkCount, maxParallelism);
        //work.Add(new WorkTracker(monitorWorkCount));

        //int spraycoaterWorkCount = ((spraycoaterCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        //spraycoaterWorkCount = Math.Min(spraycoaterWorkCount, maxParallelism);
        //work.Add(new WorkTracker(spraycoaterWorkCount));

        //int pilerWorkCount = ((pilerCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        //pilerWorkCount = Math.Min(pilerWorkCount, maxParallelism);
        //work.Add(new WorkTracker(pilerWorkCount));

        work.Add(new WorkTracker(stationCount > 0 ? 1 : 0));

        int sandboxModeWorkCount = ((transportEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        sandboxModeWorkCount = Math.Min(sandboxModeWorkCount, maxParallelism);
        work.Add(new WorkTracker(GameMain.sandboxToolsEnabled ? sandboxModeWorkCount : 0));

        int presentCargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        presentCargoPathsWorkCount = Math.Min(presentCargoPathsWorkCount, maxParallelism);
        work.Add(new WorkTracker(Status == OptimizedPlanetStatus.Stopped ? presentCargoPathsWorkCount : 0));

        work.Add(new WorkTracker(markerCount > 0 ? 1 : 0));

        return work.ToArray();
    }
}

internal enum WorkType
{
    BeforePower = 0,
    Power,
    Construction,
    CheckBefore,
    Assembler,
    LabResearchMode,
    LabOutput2NextData,
    TransportData,
    InputFromBelt,
    InserterData,
    Storage,
    CargoPathsData,
    Splitter,
    Monitor,
    Spraycoater,
    Piler,
    OutputToBelt,
    SandboxMode,
    PresentCargoPathsData,
    Digital,
    Done
}

internal record struct WorkPlan(WorkType WorkType, int WorkIndex, int WorkParallelism);
internal record struct PlanetWorkPlan(PlanetWorkManager PlanetWorkManager, WorkPlan WorkPlan);

internal struct WorkTracker : IDisposable
{
    public int ScheduledCount;
    public int CompletedCount;
    public int MaxWorkCount;
    public readonly ManualResetEvent WaitForCompletion;

    public WorkTracker(int maxWorkCount)
    {
        ScheduledCount = 0;
        CompletedCount = 0;
        MaxWorkCount = maxWorkCount;
        WaitForCompletion = new(false);
    }

    public void Reset()
    {
        ScheduledCount = 0;
        CompletedCount = 0;
        WaitForCompletion.Reset();
    }

    public void Dispose()
    {
        WaitForCompletion.Dispose();
    }
}

internal sealed class PlanetWorkManager
{
    private WorkTracker[] _workTrackers;
    private int _currentWorkType;

    public PlanetFactory Planet { get; }
    public OptimizedPlanet OptimizedPlanet { get; }

    public PlanetWorkManager(PlanetFactory planet, OptimizedPlanet optimizedPlanet, int parallelism)
    {
        _workTrackers = optimizedPlanet.GetMultithreadedWork(parallelism);

        Planet = planet;
        OptimizedPlanet = optimizedPlanet;
    }

    public void SetMaxWorkParallelism(int parallelism)
    {
        for (int i = 0; i < _workTrackers.Length; i++)
        {
            _workTrackers[i].Dispose();
        }

        OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(Planet);
        _workTrackers = optimizedPlanet.GetMultithreadedWork(parallelism);
    }

    public WorkPlan? TryGetWork(out bool canScheduleMoreWork)
    {
        WorkType currentWorkType = (WorkType)_currentWorkType;
        if (currentWorkType == WorkType.Done)
        {
            canScheduleMoreWork = false;
            return null;
        }

        WorkTracker workTracker = _workTrackers[(int)currentWorkType];
        if (workTracker.ScheduledCount >= workTracker.MaxWorkCount)
        {
            canScheduleMoreWork = currentWorkType + 1 < WorkType.Done;
            return null;
        }

        int workIndex = Interlocked.Increment(ref _workTrackers[(int)currentWorkType].ScheduledCount) - 1;
        if (workIndex >= workTracker.MaxWorkCount)
        {
            canScheduleMoreWork = currentWorkType + 1 < WorkType.Done;
            return null;
        }

        canScheduleMoreWork = true;
        return new WorkPlan(currentWorkType, workIndex, workTracker.MaxWorkCount);
    }

    public WorkPlan? TryWaitForWork()
    {
        WorkType currentWorkType = (WorkType)_currentWorkType;
        if (currentWorkType == WorkType.Done)
        {
            return null;
        }

        WorkType nextWorkType = currentWorkType + 1;
        if (nextWorkType == WorkType.Done)
        {
            return null;
        }

        WorkTracker workTracker = _workTrackers[(int)nextWorkType];
        if (workTracker.ScheduledCount >= workTracker.MaxWorkCount)
        {
            return null;
        }

        int workIndex = Interlocked.Increment(ref _workTrackers[(int)nextWorkType].ScheduledCount) - 1;
        if (workIndex >= workTracker.MaxWorkCount)
        {
            return null;
        }

        _workTrackers[(int)currentWorkType].WaitForCompletion.WaitOne();
        return new WorkPlan(nextWorkType, workIndex, workTracker.MaxWorkCount);
    }

    public void CompleteWork(WorkPlan workPlan)
    {
        ref WorkTracker workTracker = ref _workTrackers[(int)workPlan.WorkType];
        int currentCount = Interlocked.Increment(ref workTracker.CompletedCount);
        if (currentCount == workTracker.MaxWorkCount)
        {
            int nextWorkOffset = 1;
            while (nextWorkOffset + _currentWorkType < _workTrackers.Length && _workTrackers[_currentWorkType + nextWorkOffset].MaxWorkCount == 0)
            {
                nextWorkOffset++;
            }

            _currentWorkType = Interlocked.Add(ref _currentWorkType, nextWorkOffset);
            workTracker.WaitForCompletion.Set();
        }
        else if (currentCount > workTracker.MaxWorkCount)
        {
            throw new InvalidOperationException($"Completed more work for {workPlan.WorkType} than the max {workTracker.MaxWorkCount}");
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _workTrackers.Length; i++)
        {
            _workTrackers[i].Reset();
        }
        _currentWorkType = (int)WorkType.BeforePower;
    }
}

internal sealed class StarClusterWorkManager
{
    private PlanetWorkManager[] _planetWorkManagers;
    private PlanetWorkManager[] _allPlanetWorkManagers;
    private int _planetsWithWorkScheduledCount;
    private int _planetsNotCompletedCount;

    public int Parallelism { get; private set; }

    public StarClusterWorkManager(PlanetFactory[] allPlanets, int parallelism)
    {
        _planetWorkManagers = new PlanetWorkManager[allPlanets.Length];
        List<PlanetWorkManager> allPlanetWorkManagers = [];
        Parallelism = parallelism;

        for (int i = 0; i < allPlanets.Length; i++)
        {
            if (allPlanets[i] == null)
            {
                continue;
            }

            OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(allPlanets[i]);
            allPlanetWorkManagers.Add(new PlanetWorkManager(allPlanets[i], optimizedPlanet, parallelism));
        }

        _planetWorkManagers = allPlanetWorkManagers.ToArray();
        _allPlanetWorkManagers = allPlanetWorkManagers.ToArray();

        Reset();
    }

    public void SetMaxWorkParallelism(int parallelism)
    {
        Parallelism = parallelism;

        for (int i = 0; i < _planetWorkManagers.Length; i++)
        {
            _planetWorkManagers[i].SetMaxWorkParallelism(parallelism);
        }
    }

    public PlanetWorkPlan? TryGetWork()
    {
        int planetsNotCompletedCount = _planetsNotCompletedCount;
        while (_planetsWithWorkScheduledCount < planetsNotCompletedCount)
        {
            int planetIndex = Interlocked.Increment(ref _planetsWithWorkScheduledCount) - 1;
            if (planetIndex >= planetsNotCompletedCount)
            {
                break;
            }

            PlanetWorkPlan? planetWorkPlan = TryGetWork(planetIndex);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //while (true)
        //{
        bool hasIncompleteWork = false;
        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            if (_planetWorkManagers[i] == null)
            {
                continue;
            }
            hasIncompleteWork = true;

            PlanetWorkPlan? planetWorkPlan = TryGetWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //if (!hasIncompleteWork)
        //{
        //    break;
        //}

        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            PlanetWorkPlan? planetWorkPlan = TryWaitForWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }
        //}

        return null;
    }

    public void Reset()
    {
        _allPlanetWorkManagers.CopyTo(_planetWorkManagers, 0);
        for (int i = 0; i < _planetWorkManagers.Length; i++)
        {
            _planetWorkManagers[i].Reset();
        }

        _planetsWithWorkScheduledCount = 0;
        _planetsNotCompletedCount = _planetWorkManagers.Length;
    }

    private PlanetWorkPlan? TryGetWork(int planetIndex)
    {
        PlanetWorkManager? planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryGetWork(out bool canScheduleMoreWork);
        if (!canScheduleMoreWork)
        {
            int lastPlanetNotCompletedIndex = Interlocked.Decrement(ref _planetsNotCompletedCount);
            if (lastPlanetNotCompletedIndex > 0)
            {
                _planetWorkManagers[planetIndex] = _planetWorkManagers[lastPlanetNotCompletedIndex];
            }
        }
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }

    private PlanetWorkPlan? TryWaitForWork(int planetIndex)
    {
        PlanetWorkManager? planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryWaitForWork();
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }
}

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly WorkerThreadExecutor _workerThreadExecutor;
    private readonly object _singleThreadedCodeLock;

    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerThreadExecutor = workerThreadExecutor;
        _singleThreadedCodeLock = singleThreadedCodeLock;
    }

    public void Execute(PlanetData? localPlanet, long time)
    {
        int originalWorkerThreadIndex = _workerThreadExecutor.curThreadIdx;
        int originalWorkerUsedThreadCount = _workerThreadExecutor.usedThreadCnt;
        try
        {
            PlanetWorkManager? planetWorkManager = null;
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
                    planetWorkManager.Planet.powerSystem.multithreadPlayerPos = GameMain.mainPlayer.position;
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
                    planetWorkManager.Planet.constructionSystem.GameTick(time, isActive);
                    planetWorkManager.Planet.constructionSystem.ExcuteDeferredTargetChange();
                }
                else if (workPlan.Value.WorkType == WorkType.CheckBefore && planetWorkManager.Planet.factorySystem != null)
                {
                    planetWorkManager.Planet.factorySystem.CheckBeforeGameTick();
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
                    _workerThreadExecutor.inserterLocalPlanet = localPlanet;
                    _workerThreadExecutor.inserterFactories = [planetWorkManager.Planet];
                    _workerThreadExecutor.inserterFactoryCnt = 1;
                    _workerThreadExecutor.inserterTime = time;
                    _workerThreadExecutor.InserterPartExecute();
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
                    planetWorkManager.Planet.cargoTraffic.SplitterGameTick(time);
                }
                else if (workPlan.Value.WorkType == WorkType.Monitor && planetWorkManager.Planet.cargoTraffic != null)
                {
                    planetWorkManager.Planet.cargoTraffic.MonitorGameTick();
                }
                else if (workPlan.Value.WorkType == WorkType.Spraycoater && planetWorkManager.Planet.cargoTraffic != null)
                {
                    planetWorkManager.OptimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(planetWorkManager.Planet);
                    //planetWorkManager.Planet.cargoTraffic.SpraycoaterGameTick();
                }
                else if (workPlan.Value.WorkType == WorkType.Piler && planetWorkManager.Planet.cargoTraffic != null)
                {
                    planetWorkManager.Planet.cargoTraffic.PilerGameTick();
                }
                else if (workPlan.Value.WorkType == WorkType.OutputToBelt && planetWorkManager.Planet.transport != null)
                {
                    int stationPilerLevel = GameMain.history.stationPilerLevel;
                    planetWorkManager.Planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
                }
                else if (workPlan.Value.WorkType == WorkType.SandboxMode && planetWorkManager.Planet.transport != null)
                {
                    planetWorkManager.Planet.transport.GameTick_SandboxMode();
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
}

internal sealed class WorkStealingMultiThreadedFactorySimulation
{
    private readonly object _singleThreadedCodeLock = new();
    private StarClusterWorkManager _starClusterWorkManager;
    private WorkExecutor[] _workExecutors;

    public void Simulate()
    {
        MultithreadSystem multithreadSystem = GameMain.multithreadSystem;
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager(GameMain.data.factories, multithreadSystem.usedThreadCnt);
        }
        _starClusterWorkManager.Reset();
        if (_starClusterWorkManager.Parallelism != multithreadSystem.usedThreadCnt)
        {
            _starClusterWorkManager.SetMaxWorkParallelism(multithreadSystem.usedThreadCnt);
        }
        if (_workExecutors == null || _workExecutors.Length != multithreadSystem.usedThreadCnt)
        {
            _workExecutors = new WorkExecutor[multithreadSystem.usedThreadCnt];
            for (int i = 0; i < _workExecutors.Length; i++)
            {
                _workExecutors[i] = new WorkExecutor(_starClusterWorkManager, multithreadSystem.workerThreadExecutors[i], _singleThreadedCodeLock);
            }
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        Parallel.ForEach(_workExecutors, parallelOptions, workExecutor => workExecutor.Execute(GameMain.localPlanet, GameMain.gameTick));
    }

    public void Clear()
    {
        _starClusterWorkManager = null;
        _workExecutors = null;
    }
}