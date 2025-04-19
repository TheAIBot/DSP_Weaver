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
    void Execute(long time);
}

internal sealed class PlanetWideBeforePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;

    public void Execute(long time)
    {
        _optimizedPlanet.BeforePowerStep(time);
    }
}

internal sealed class SubFactoryBeforePower : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedPowerSystem _optimizedPowerSystem;
    private readonly OptimizedSubFactory _subFactory;

    public void Execute(long time)
    {
        _optimizedPowerSystem.BeforePower(_planet, _subFactory);
    }
}

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;

    public void Execute(long time)
    {
        _optimizedPlanet.PowerStep(time);
    }
}

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;

    public void Execute(long time)
    {
        _subFactory.GameTick(time);
    }
}

internal sealed class PlanetWideTransport : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;

    public void Execute(long time)
    {
        _optimizedPlanet.TransportStep(time);
    }
}

internal sealed class PlanetWideDigitalSystem : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;

    public void Execute(long time)
    {
        _optimizedPlanet.DigitalSystemStep();
    }
}

internal sealed class WorkStep
{
    private int _scheduledCount;
    private int _completedCount;
    private readonly int _maxWorkCount;
    private readonly ManualResetEventSlim _waitForCompletion;
}

internal sealed class OptimizedPlanet
{
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private OptimizedSubFactory[] _subFactories;
    private OptimizedPowerSystem _optimizedPowerSystem;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    private WorkTracker[] _workTrackers;

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

        _workTrackers = null;
    }

    public void Initialize()
    {
        List<Graph> subFactoryGraphs = Graphifier.ToGraphs(_planet.factorySystem);
        Graphifier.CombineSmallGraphs(subFactoryGraphs);

        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(_planet.powerSystem);

        _subFactories = new OptimizedSubFactory[subFactoryGraphs.Count];
        for (int i = 0; i < _subFactories.Length; i++)
        {
            _subFactories[i] = new OptimizedSubFactory(_planet, _starClusterResearchManager);
            _subFactories[i].Initialize(subFactoryGraphs[i], optimizedPowerSystemBuilder);
        }

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();

        Status = OptimizedPlanetStatus.Running;

        _workTrackers = null;
    }

    public WorkTracker[] GetMultithreadedWork(int maxParallelism)
    {
        if (_workTrackersParallelism != maxParallelism)
        {
            _workTrackers = CreateMultithreadedWork(maxParallelism);
            _workTrackersParallelism = maxParallelism;
        }

        return _workTrackers;
    }

    private WorkTracker[] CreateMultithreadedWork(int maxParallelism)
    {
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
}

internal sealed class OptimizedSubFactory
{
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

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

    private WorkTracker[] _workTrackers;
    private int _workTrackersParallelism;

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

        Status = OptimizedPlanetStatus.Stopped;

        _workTrackers = null;
        _workTrackersParallelism = -1;
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

        Status = OptimizedPlanetStatus.Running;

        _workTrackers = null;
        _workTrackersParallelism = -1;
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
        _spraycoaterExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
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

    public void GameTick(long time)
    {
        _minerExecutor.GameTick(_planet);
        _assemblerExecutor.GameTick(_planet);
        _fractionatorExecutor.GameTick(_planet);
        _ejectorExecutor.GameTick(_planet, time);
        _siloExecutor.GameTick(_planet);

        _producingLabExecutor.GameTickLabProduceMode(_planet);
        _producingLabExecutor.GameTickLabOutputToNext();
        _researchingLabExecutor.GameTickLabResearchMode(_planet);
        _researchingLabExecutor.GameTickLabOutputToNext();

        // Transport input from belt

        _optimizedBiInserterExecutor.GameTickInserters(_planet);
        _optimizedInserterExecutor.GameTickInserters(_planet);

        // Storage
        // CargoPathsData
        // Splitter

        _monitorExecutor.GameTick(_planet);
        _spraycoaterExecutor.GameTick(_planet);
        _pilerExecutor.GameTick(_planet);

        // Transport output to belt
        // Transport sandbox mode
        // PresentCargoPathsData
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