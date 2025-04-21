using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Dispensers;
using Weaver.Optimizations.LinearDataAccess.Ejectors;
using Weaver.Optimizations.LinearDataAccess.Fractionators;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.Miners;
using Weaver.Optimizations.LinearDataAccess.Monitors;
using Weaver.Optimizations.LinearDataAccess.Pilers;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Silos;
using Weaver.Optimizations.LinearDataAccess.Splitters;
using Weaver.Optimizations.LinearDataAccess.Spraycoaters;
using Weaver.Optimizations.LinearDataAccess.Stations;
using Weaver.Optimizations.LinearDataAccess.Tanks;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

namespace Weaver.Optimizations.LinearDataAccess;

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
}