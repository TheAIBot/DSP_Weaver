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
using Weaver.Optimizations.LinearDataAccess.Turrets;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedSubFactory
{
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public AssemblerExecutor _assemblerExecutor;

    public VeinMinerExecutor<BeltMinerOutput> _beltVeinMinerExecutor;
    public VeinMinerExecutor<StationMinerOutput> _stationVeinMinerExecutor;
    public OilMinerExecutor _oilMinerExecutor;
    public WaterMinerExecutor _waterMinerExecutor;

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

    public MiningFlags _miningFlags;

    public OptimizedSubFactory(PlanetFactory planet, StarClusterResearchManager starClusterResearchManager)
    {
        _planet = planet;
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void Save(CargoContainer cargoContainer)
    {
        _beltExecutor.Save(cargoContainer);
        _beltVeinMinerExecutor.Save(_planet);
        _stationVeinMinerExecutor.Save(_planet);
        _oilMinerExecutor.Save(_planet);
        _waterMinerExecutor.Save(_planet);
        _optimizedBiInserterExecutor.Save(_planet);
        _optimizedInserterExecutor.Save(_planet);
        _assemblerExecutor.Save(_planet);
        _producingLabExecutor.Save(_planet);
        _researchingLabExecutor.Save(_planet);
        _spraycoaterExecutor.Save(_planet);
        _fractionatorExecutor.Save(_planet);
        _tankExecutor.Save(_planet);
    }

    public void Initialize(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, TurretExecutorBuilder turretExecutorBuilder)
    {
        optimizedPowerSystemBuilder.AddSubFactory(this);

        InitializeBelts(subFactoryGraph);
        InitializeAssemblers(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeMiners(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializeStations(subFactoryGraph, _beltExecutor, _stationVeinMinerExecutor);
        InitializeEjectors(subFactoryGraph);
        InitializeSilos(subFactoryGraph);
        InitializeLabAssemblers(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeResearchingLabs(subFactoryGraph, optimizedPowerSystemBuilder);
        InitializeInserters(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializeMonitors(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializeSpraycoaters(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializePilers(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializeFractionators(subFactoryGraph, optimizedPowerSystemBuilder, _beltExecutor);
        InitializeDispensers(subFactoryGraph);
        InitializeTanks(subFactoryGraph, _beltExecutor);
        InitializeSplitters(subFactoryGraph, _beltExecutor);

        turretExecutorBuilder.Initialize(_planet, subFactoryGraph, _beltExecutor);
    }

    private void InitializeInserters(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedBiInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder(), beltExecutor);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder(), beltExecutor);
    }

    private void InitializeAssemblers(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(_planet, subFactoryGraph, optimizedPowerSystemBuilder);
    }

    private void InitializeMiners(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _beltVeinMinerExecutor = new VeinMinerExecutor<BeltMinerOutput>();
        _beltVeinMinerExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder.CreateBeltVeinMinerBuilder(), beltExecutor);

        _stationVeinMinerExecutor = new VeinMinerExecutor<StationMinerOutput>();
        _stationVeinMinerExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder.CreateStationVeinMinerBuilder(), beltExecutor);

        _oilMinerExecutor = new OilMinerExecutor();
        _oilMinerExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);

        _waterMinerExecutor = new WaterMinerExecutor();
        _waterMinerExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);
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

    private void InitializeMonitors(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _monitorExecutor = new MonitorExecutor();
        _monitorExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);
    }

    private void InitializeSpraycoaters(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(_planet, this, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);
    }

    private void InitializePilers(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _pilerExecutor = new PilerExecutor();
        _pilerExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);
    }

    private void InitializeFractionators(Graph subFactoryGraph, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _fractionatorExecutor = new FractionatorExecutor();
        _fractionatorExecutor.Initialize(_planet, subFactoryGraph, optimizedPowerSystemBuilder, beltExecutor);
    }

    private void InitializeStations(Graph subFactoryGraph, BeltExecutor beltExecutor, VeinMinerExecutor<StationMinerOutput> stationVeinMinerExecutor)
    {
        _stationExecutor = new StationExecutor();
        _stationExecutor.Initialize(_planet, subFactoryGraph, beltExecutor, stationVeinMinerExecutor);
    }

    private void InitializeDispensers(Graph subFactoryGraph)
    {
        _dispenserExecutor = new DispenserExecutor();
        _dispenserExecutor.Initialize(subFactoryGraph);
    }

    private void InitializeTanks(Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        _tankExecutor = new TankExecutor();
        _tankExecutor.Initialize(_planet, subFactoryGraph, beltExecutor);
    }

    private void InitializeBelts(Graph subFactoryGraph)
    {
        _beltExecutor = new BeltExecutor();
        _beltExecutor.Initialize(_planet, subFactoryGraph);
    }

    private void InitializeSplitters(Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        _splitterExecutor = new SplitterExecutor();
        _splitterExecutor.Initialize(_planet, subFactoryGraph, beltExecutor);
    }

    public void GameTick(WorkerTimings workerTimings, long time)
    {
        _miningFlags = new MiningFlags();

        workerTimings.StartTimer();
        _beltVeinMinerExecutor.GameTick(_planet, ref _miningFlags);
        _stationVeinMinerExecutor.GameTick(_planet, ref _miningFlags);
        _oilMinerExecutor.GameTick(_planet);
        _waterMinerExecutor.GameTick(_planet);
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
        _stationExecutor.StationGameTick(_planet, time, _stationVeinMinerExecutor, ref _miningFlags);
        workerTimings.RecordTime(WorkType.TransportData);

        workerTimings.StartTimer();
        _stationExecutor.InputFromBelt(_planet, time);
        workerTimings.RecordTime(WorkType.InputFromBelt);

        workerTimings.StartTimer();
        _optimizedBiInserterExecutor.GameTickInserters(_planet);
        _optimizedInserterExecutor.GameTickInserters(_planet);
        workerTimings.RecordTime(WorkType.InserterData);

        workerTimings.StartTimer();
        // Storage has no logic on planets the player isn't on which is why it is omitted
        _tankExecutor.GameTick();
        workerTimings.RecordTime(WorkType.Storage);

        workerTimings.StartTimer();
        _beltExecutor.GameTick(_planet);
        workerTimings.RecordTime(WorkType.CargoPathsData);

        workerTimings.StartTimer();
        _splitterExecutor.GameTick(_planet, this, _beltExecutor, time);
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

    public bool InsertCargoIntoStorage(int entityId, ref OptimizedCargo cargo, bool useBan = true)
    {
        int storageId = _planet.entityPool[entityId].storageId;
        if (storageId > 0)
        {
            StorageComponent storageComponent = _planet.factoryStorage.storagePool[storageId];
            while (storageComponent != null)
            {
                if (!useBan || storageComponent.lastFullItem != cargo.item)
                {
                    if (AddWholeCargo(storageComponent, ref cargo, useBan))
                    {
                        return true;
                    }
                    if (storageComponent.nextStorage == null)
                    {
                        return false;
                    }
                }
                storageComponent = storageComponent.nextStorage;
            }
        }
        return false;
    }

    public int PickFromStorageFiltered(int entityId, ref int filter, int count, out int inc)
    {
        inc = 0;
        int num = count;
        int storageId = _planet.entityPool[entityId].storageId;
        if (storageId > 0)
        {
            StorageComponent storageComponent = _planet.factoryStorage.storagePool[storageId];
            StorageComponent storageComponent2 = storageComponent;
            if (storageComponent != null)
            {
                storageComponent = storageComponent.topStorage;
                while (storageComponent != null)
                {
                    if (storageComponent.lastEmptyItem != 0 && storageComponent.lastEmptyItem != filter)
                    {
                        int filter2 = filter;
                        int count2 = count;
                        storageComponent.TakeTailItemsFiltered(ref filter2, ref count2, out var inc2, _planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                        count -= count2;
                        inc += inc2;
                        if (filter2 > 0)
                        {
                            filter = filter2;
                        }
                        if (count == 0)
                        {
                            storageComponent.lastEmptyItem = -1;
                            return num;
                        }
                        if (filter >= 0)
                        {
                            storageComponent.lastEmptyItem = filter;
                        }
                    }
                    if (storageComponent == storageComponent2)
                    {
                        break;
                    }
                    storageComponent = storageComponent.previousStorage;
                    continue;
                }
            }
        }
        return num - count;
    }

    private bool AddWholeCargo(StorageComponent storage, ref OptimizedCargo cargo, bool useBan = false)
    {
        if (cargo.item <= 0 || cargo.stack == 0 || cargo.item >= 12000)
        {
            return false;
        }
        bool flag = storage.type > EStorageType.Default;
        if (flag)
        {
            if (storage.type == EStorageType.Fuel && !StorageComponent.itemIsFuel[cargo.item])
            {
                return false;
            }
            if (storage.type == EStorageType.Ammo && (!StorageComponent.itemIsAmmo[cargo.item] || StorageComponent.itemIsBomb[cargo.item]))
            {
                return false;
            }
            if (storage.type == EStorageType.Bomb && !StorageComponent.itemIsBomb[cargo.item])
            {
                return false;
            }
            if (storage.type == EStorageType.Fighter && !StorageComponent.itemIsFighter[cargo.item])
            {
                return false;
            }
        }
        bool flag2 = false;
        int num = 0;
        int num2 = (useBan ? (storage.size - storage.bans) : storage.size);
        for (int i = 0; i < num2; i++)
        {
            if (storage.grids[i].itemId == 0)
            {
                if (flag && (storage.type == EStorageType.DeliveryFiltered || storage.grids[i].filter > 0) && cargo.item != storage.grids[i].filter)
                {
                    continue;
                }
                if (num == 0)
                {
                    num = StorageComponent.itemStackCount[cargo.item];
                }
                storage.grids[i].itemId = cargo.item;
                if (storage.grids[i].filter == 0)
                {
                    storage.grids[i].stackSize = num;
                }
            }
            if (storage.grids[i].itemId == cargo.item)
            {
                if (num == 0)
                {
                    num = storage.grids[i].stackSize;
                }
                int num3 = num - storage.grids[i].count;
                if (cargo.stack <= num3)
                {
                    storage.grids[i].count += cargo.stack;
                    storage.grids[i].inc += cargo.inc;
                    flag2 = true;
                    break;
                }
            }
        }
        if (flag2)
        {
            storage.searchStart = 0;
            storage.lastEmptyItem = -1;
            storage.NotifyStorageChange();
        }
        return flag2;
    }
}