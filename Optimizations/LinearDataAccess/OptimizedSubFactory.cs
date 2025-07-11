﻿using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Belts;
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
using Weaver.Optimizations.LinearDataAccess.Statistics;
using Weaver.Optimizations.LinearDataAccess.Tanks;
using Weaver.Optimizations.LinearDataAccess.Turrets;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedSubFactory
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private OptimizedProductionStatistics _optimizedProductionStatistics;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor = null!;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor = null!;

    public AssemblerExecutor _assemblerExecutor = null!;

    public VeinMinerExecutor<BeltMinerOutput> _beltVeinMinerExecutor = null!;
    public VeinMinerExecutor<StationMinerOutput> _stationVeinMinerExecutor = null!;
    public OilMinerExecutor _oilMinerExecutor = null!;
    public WaterMinerExecutor _waterMinerExecutor = null!;

    public EjectorExecutor _ejectorExecutor = null!;

    public SiloExecutor _siloExecutor = null!;

    //private NetworkIdAndState<LabState>[] _labProduceNetworkIdAndStates;
    public ProducingLabExecutor _producingLabExecutor = null!;
    public NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates = null!;
    public OptimizedProducingLab[] _optimizedProducingLabs = null!;
    public ProducingLabRecipe[] _producingLabRecipes = null!;
    public Dictionary<int, int> _producingLabIdToOptimizedIndex = null!;

    public ResearchingLabExecutor _researchingLabExecutor = null!;
    public NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates = null!;
    public OptimizedResearchingLab[] _optimizedResearchingLabs = null!;
    public Dictionary<int, int> _researchingLabIdToOptimizedIndex = null!;

    public MonitorExecutor _monitorExecutor = null!;
    public SpraycoaterExecutor _spraycoaterExecutor = null!;
    public PilerExecutor _pilerExecutor = null!;

    public FractionatorExecutor _fractionatorExecutor = null!;

    public StationExecutor _stationExecutor = null!;

    public TankExecutor _tankExecutor = null!;

    public BeltExecutor _beltExecutor = null!;
    public SplitterExecutor _splitterExecutor = null!;

    public bool HasCalculatedPowerConsumption = false;

    public OptimizedSubFactory(PlanetFactory planet, OptimizedTerrestrialPlanet optimizedTerrestrialPlanet, StarClusterResearchManager starClusterResearchManager)
    {
        _planet = planet;
        _optimizedPlanet = optimizedTerrestrialPlanet;
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

    public void Initialize(Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           PlanetWideBeltExecutor planetWideBeltExecutor,
                           TurretExecutorBuilder turretExecutorBuilder,
                           PlanetWideProductionRegisterBuilder planetWideProductionRegisterBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           OptimizedItemId[]?[]? fuelNeeds)
    {
        SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder = optimizedPowerSystemBuilder.AddSubFactory(this);

        InitializeBelts(subFactoryGraph, planetWideBeltExecutor);
        InitializeAssemblers(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        InitializeMiners(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor);
        InitializeStations(subFactoryGraph, _beltExecutor, _stationVeinMinerExecutor);
        InitializeEjectors(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        InitializeSilos(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        InitializeLabAssemblers(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        InitializeResearchingLabs(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        InitializeInserters(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor, fuelNeeds);
        InitializeMonitors(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor);
        InitializeSpraycoaters(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor);
        InitializePilers(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor);
        InitializeFractionators(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor);
        InitializeTanks(subFactoryGraph, _beltExecutor);
        InitializeSplitters(subFactoryGraph);

        turretExecutorBuilder.Initialize(_planet, subFactoryGraph, planetWideProductionRegisterBuilder, _beltExecutor);

        _optimizedProductionStatistics = subFactoryProductionRegisterBuilder.Build();
        HasCalculatedPowerConsumption = false;
    }

    private void InitializeInserters(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, BeltExecutor beltExecutor, OptimizedItemId[]?[]? fuelNeeds)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates,
                                                                                 _producingLabNetworkIdAndStates,
                                                                                 _researchingLabNetworkIdAndStates,
                                                                                 subFactoryPowerSystemBuilder.FuelGeneratorSegments,
                                                                                 fuelNeeds);
        _optimizedBiInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => x.bidirectional, subFactoryPowerSystemBuilder.CreateBiInserterBuilder(), beltExecutor);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates,
                                                                             _producingLabNetworkIdAndStates,
                                                                             _researchingLabNetworkIdAndStates,
                                                                             subFactoryPowerSystemBuilder.FuelGeneratorSegments,
                                                                             fuelNeeds);
        _optimizedInserterExecutor.Initialize(_planet, this, subFactoryGraph, x => !x.bidirectional, subFactoryPowerSystemBuilder.CreateInserterBuilder(), beltExecutor);
    }

    private void InitializeAssemblers(Graph subFactoryGraph,
                                      SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                      SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
    }

    private void InitializeMiners(Graph subFactoryGraph,
                                  SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                  SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                  BeltExecutor beltExecutor)
    {
        _beltVeinMinerExecutor = new VeinMinerExecutor<BeltMinerOutput>();
        _beltVeinMinerExecutor.Initialize(_planet,
                                          subFactoryGraph,
                                          subFactoryPowerSystemBuilder.CreateBeltVeinMinerBuilder(),
                                          subFactoryProductionRegisterBuilder,
                                          beltExecutor);

        _stationVeinMinerExecutor = new VeinMinerExecutor<StationMinerOutput>();
        _stationVeinMinerExecutor.Initialize(_planet,
                                             subFactoryGraph,
                                             subFactoryPowerSystemBuilder.CreateStationVeinMinerBuilder(),
                                             subFactoryProductionRegisterBuilder,
                                             beltExecutor);

        _oilMinerExecutor = new OilMinerExecutor();
        _oilMinerExecutor.Initialize(_planet,
                                     subFactoryGraph,
                                     subFactoryPowerSystemBuilder,
                                     subFactoryProductionRegisterBuilder,
                                     beltExecutor);

        _waterMinerExecutor = new WaterMinerExecutor();
        _waterMinerExecutor.Initialize(_planet,
                                       subFactoryGraph,
                                       subFactoryPowerSystemBuilder,
                                       subFactoryProductionRegisterBuilder,
                                       beltExecutor);
    }

    private void InitializeEjectors(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _ejectorExecutor = new EjectorExecutor();
        _ejectorExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
    }

    private void InitializeSilos(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _siloExecutor = new SiloExecutor();
        _siloExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
    }

    private void InitializeLabAssemblers(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor(_starClusterResearchManager);
        _researchingLabExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeMonitors(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _monitorExecutor = new MonitorExecutor();
        _monitorExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, beltExecutor);
    }

    private void InitializeSpraycoaters(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder, BeltExecutor beltExecutor)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, beltExecutor);
    }

    private void InitializePilers(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, BeltExecutor beltExecutor)
    {
        _pilerExecutor = new PilerExecutor();
        _pilerExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, beltExecutor);
    }

    private void InitializeFractionators(Graph subFactoryGraph, SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder, BeltExecutor beltExecutor)
    {
        _fractionatorExecutor = new FractionatorExecutor();
        _fractionatorExecutor.Initialize(_planet, subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, beltExecutor);
    }

    private void InitializeStations(Graph subFactoryGraph,
                                    BeltExecutor beltExecutor,
                                    VeinMinerExecutor<StationMinerOutput> stationVeinMinerExecutor)
    {
        _stationExecutor = new StationExecutor();
        _stationExecutor.Initialize(_planet,
                                    subFactoryGraph,
                                    beltExecutor,
                                    stationVeinMinerExecutor);
    }

    private void InitializeTanks(Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        _tankExecutor = new TankExecutor();
        _tankExecutor.Initialize(_planet, subFactoryGraph, beltExecutor);
    }

    private void InitializeBelts(Graph subFactoryGraph, PlanetWideBeltExecutor planetWideBeltExecutor)
    {
        _beltExecutor = new BeltExecutor();
        _beltExecutor.Initialize(_planet, subFactoryGraph);

        planetWideBeltExecutor.AddBeltExecutor(_beltExecutor);
    }

    private void InitializeSplitters(Graph subFactoryGraph)
    {
        _splitterExecutor = new SplitterExecutor();
        _splitterExecutor.Initialize(_planet, subFactoryGraph);
    }

    public void GameTick(WorkerTimings workerTimings, long time, SubFactoryPowerConsumption powerSystem)
    {
        var miningFlags = new MiningFlags();
        long[] networkPowerConsumptions = powerSystem.NetworksPowerConsumption;
        Array.Clear(networkPowerConsumptions, 0, networkPowerConsumptions.Length);

        int[] productRegister = _optimizedProductionStatistics.ProductRegister;
        int[] consumeRegister = _optimizedProductionStatistics.ConsumeRegister;

        workerTimings.StartTimer();
        _beltVeinMinerExecutor.GameTick(_planet, powerSystem.BeltVeinMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister, ref miningFlags);
        _stationVeinMinerExecutor.GameTick(_planet, powerSystem.StationVeinMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister, ref miningFlags);
        _oilMinerExecutor.GameTick(_planet, powerSystem.OilMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister);
        _waterMinerExecutor.GameTick(_planet, powerSystem.WaterMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister);
        _assemblerExecutor.GameTick(_planet, powerSystem.AssemblerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister, consumeRegister);
        _fractionatorExecutor.GameTick(_planet, powerSystem.FractionatorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister, consumeRegister);
        _ejectorExecutor.GameTick(_planet, time, powerSystem.EjectorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, consumeRegister);
        _siloExecutor.GameTick(_planet, powerSystem.SiloPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, consumeRegister);
        _producingLabExecutor.GameTickLabProduceMode(_planet, powerSystem.ProducingLabPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, productRegister, consumeRegister);
        _producingLabExecutor.GameTickLabOutputToNext();
        workerTimings.RecordTime(WorkType.Assembler);

        workerTimings.StartTimer();
        _researchingLabExecutor.GameTickLabResearchMode(_planet, powerSystem.ResearchingLabPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, consumeRegister);
        _researchingLabExecutor.GameTickLabOutputToNext();
        workerTimings.RecordTime(WorkType.LabResearchMode);

        workerTimings.StartTimer();
        _stationExecutor.StationGameTick(_planet, time, _stationVeinMinerExecutor, ref miningFlags);
        workerTimings.RecordTime(WorkType.TransportData);

        workerTimings.StartTimer();
        _stationExecutor.InputFromBelt();
        workerTimings.RecordTime(WorkType.InputFromBelt);

        workerTimings.StartTimer();
        _optimizedBiInserterExecutor.GameTickInserters(_planet, powerSystem.InserterBiPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions);
        _optimizedInserterExecutor.GameTickInserters(_planet, powerSystem.InserterPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions);
        workerTimings.RecordTime(WorkType.InserterData);

        workerTimings.StartTimer();
        // Storage has no logic on planets the player isn't on which is why it is omitted
        _tankExecutor.GameTick();
        workerTimings.RecordTime(WorkType.Storage);

        workerTimings.StartTimer();
        _beltExecutor.GameTick();
        workerTimings.RecordTime(WorkType.CargoPathsData);

        workerTimings.StartTimer();
        _splitterExecutor.GameTick(_planet, this, _beltExecutor);
        workerTimings.RecordTime(WorkType.Splitter);

        workerTimings.StartTimer();
        _monitorExecutor.GameTick(_planet, powerSystem.MonitorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions);
        workerTimings.RecordTime(WorkType.Monitor);

        workerTimings.StartTimer();
        _spraycoaterExecutor.GameTick(powerSystem.SpraycoaterPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions, consumeRegister);
        workerTimings.RecordTime(WorkType.Spraycoater);

        workerTimings.StartTimer();
        _pilerExecutor.GameTick(_planet, powerSystem.PilerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes, networkPowerConsumptions);
        workerTimings.RecordTime(WorkType.Piler);

        workerTimings.StartTimer();
        _stationExecutor.OutputToBelt();
        workerTimings.RecordTime(WorkType.OutputToBelt);

        _optimizedPlanet.AddMiningFlags(miningFlags);

        HasCalculatedPowerConsumption = true;
    }

    public void RefreshPowerConsumptionDemands(ProductionStatistics statistics, SubFactoryPowerConsumption powerSystem)
    {
        RefreshPowerConsumptionDemands(statistics, _beltVeinMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.BeltVeinMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _stationVeinMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.StationVeinMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _oilMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.OilMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _waterMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.WaterMinerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _assemblerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.AssemblerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _fractionatorExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.FractionatorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _ejectorExecutor.UpdatePowerConsumptionPerPrototype(_planet, powerSystem.EjectorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _siloExecutor.UpdatePowerConsumptionPerPrototype(_planet, powerSystem.SiloPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _producingLabExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.ProducingLabPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _researchingLabExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.ResearchingLabPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _optimizedBiInserterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.InserterBiPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _optimizedInserterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.InserterPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _monitorExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.MonitorPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _spraycoaterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.SpraycoaterPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _pilerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.PilerPowerConsumerTypeIndexes, powerSystem.PowerConsumerTypes));
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
            ref readonly PowerGeneratorComponent component = ref planet.powerSystem.genPool[entity.powerGenId];
            bool isFuelGenerator = !component.wind && !component.photovoltaic && !component.gamma && !component.geothermal;
            EntityType powerGeneratorType = isFuelGenerator ? EntityType.FuelPowerGenerator : EntityType.PowerGenerator;
            return new TypedObjectIndex(powerGeneratorType, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new TypedObjectIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new TypedObjectIndex(EntityType.Inserter, entity.inserterId);
        }
        else if (entity.powerGenId != 0)
        {
            return new TypedObjectIndex(EntityType.FuelPowerGenerator, entity.powerGenId);
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

    private static bool AddWholeCargo(StorageComponent storage, ref OptimizedCargo cargo, bool useBan = false)
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

    private static void RefreshPowerConsumptionDemands(ProductionStatistics statistics, PrototypePowerConsumptions prototypePowerConsumptions)
    {
        int[] powerConId2Index = ItemProto.powerConId2Index;
        for (int i = 0; i < prototypePowerConsumptions.PrototypeIds.Length; i++)
        {
            int num = powerConId2Index[prototypePowerConsumptions.PrototypeIds[i]];
            statistics.conDemands[num] += prototypePowerConsumptions.PrototypeIdPowerConsumption[i];
            statistics.conCount[num] += prototypePowerConsumptions.PrototypeIdCounts[i];
            statistics.totalConDemand += prototypePowerConsumptions.PrototypeIdPowerConsumption[i];
        }
    }
}