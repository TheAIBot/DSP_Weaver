using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Ejectors;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.Labs.Researching;
using Weaver.Optimizations.Miners;
using Weaver.Optimizations.Monitors;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.Pilers;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Silos;
using Weaver.Optimizations.Splitters;
using Weaver.Optimizations.Spraycoaters;
using Weaver.Optimizations.Stations;
using Weaver.Optimizations.Statistics;
using Weaver.Optimizations.Tanks;
using Weaver.Optimizations.Turrets;

namespace Weaver.Optimizations;

internal sealed class OptimizedSubFactory
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private readonly UniverseStaticData _universeStaticData;
    private OptimizedProductionStatistics _optimizedProductionStatistics;
    private SubFactoryNeeds _subFactoryNeeds;

    public InserterExecutor<OptimizedBiInserter, BiInserterGrade> _optimizedBiInserterExecutor = null!;
    public InserterExecutor<OptimizedInserter, InserterGrade> _optimizedInserterExecutor = null!;

    public AssemblerExecutor _assemblerExecutor = null!;

    public VeinMinerExecutor<BeltMinerOutput> _beltVeinMinerExecutor = null!;
    public VeinMinerExecutor<StationMinerOutput> _stationVeinMinerExecutor = null!;
    public OilMinerExecutor _oilMinerExecutor = null!;
    public WaterMinerExecutor _waterMinerExecutor = null!;

    public EjectorExecutor _ejectorExecutor = null!;

    public SiloExecutor _siloExecutor = null!;

    public ProducingLabExecutor _producingLabExecutor = null!;
    public NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates = null!;
    public OptimizedProducingLab[] _optimizedProducingLabs = null!;
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

    public OptimizedSubFactory(PlanetFactory planet, 
                               OptimizedTerrestrialPlanet optimizedTerrestrialPlanet, 
                               StarClusterResearchManager starClusterResearchManager,
                               UniverseStaticData universeStaticData)
    {
        _planet = planet;
        _optimizedPlanet = optimizedTerrestrialPlanet;
        _starClusterResearchManager = starClusterResearchManager;
        _universeStaticData = universeStaticData;
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
        _assemblerExecutor.Save(_planet, _subFactoryNeeds);
        _producingLabExecutor.Save(_planet, _subFactoryNeeds);
        _researchingLabExecutor.Save(_planet, _subFactoryNeeds);
        _spraycoaterExecutor.Save(_planet);
        _fractionatorExecutor.Save(_planet);
        _tankExecutor.Save(_planet);
        _siloExecutor.Save(_planet, _subFactoryNeeds);
        _ejectorExecutor.Save(_planet, _subFactoryNeeds);
    }

    public void Initialize(Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           PlanetWideBeltExecutor planetWideBeltExecutor,
                           TurretExecutorBuilder turretExecutorBuilder,
                           PlanetWideProductionRegisterBuilder planetWideProductionRegisterBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           OptimizedItemId[]?[]? fuelNeeds,
                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder = optimizedPowerSystemBuilder.AddSubFactory(this);
        var subFactoryNeedsBuilder = new SubFactoryNeedsBuilder();

        InitializeBelts(subFactoryGraph, planetWideBeltExecutor, universeStaticDataBuilder);
        InitializeAssemblers(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, subFactoryNeedsBuilder, universeStaticDataBuilder);
        InitializeMiners(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor, universeStaticDataBuilder);
        InitializeStations(subFactoryGraph, _beltExecutor, _stationVeinMinerExecutor, universeStaticDataBuilder);
        InitializeEjectors(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, subFactoryNeedsBuilder, universeStaticDataBuilder);
        InitializeSilos(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, subFactoryNeedsBuilder, universeStaticDataBuilder);
        InitializeLabAssemblers(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, subFactoryNeedsBuilder, universeStaticDataBuilder);
        InitializeResearchingLabs(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, subFactoryNeedsBuilder, universeStaticDataBuilder);
        _subFactoryNeeds = subFactoryNeedsBuilder.Build();
        InitializeInserters(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor, fuelNeeds, _subFactoryNeeds, universeStaticDataBuilder);
        InitializeMonitors(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor, universeStaticDataBuilder);
        InitializeSpraycoaters(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor, universeStaticDataBuilder);
        InitializePilers(subFactoryGraph, subFactoryPowerSystemBuilder, _beltExecutor, universeStaticDataBuilder);
        InitializeFractionators(subFactoryGraph, subFactoryPowerSystemBuilder, subFactoryProductionRegisterBuilder, _beltExecutor, universeStaticDataBuilder);
        InitializeTanks(subFactoryGraph, _beltExecutor);
        InitializeSplitters(subFactoryGraph, _beltExecutor);

        turretExecutorBuilder.Initialize(_planet, subFactoryGraph, planetWideProductionRegisterBuilder, _beltExecutor);

        _optimizedProductionStatistics = subFactoryProductionRegisterBuilder.Build(universeStaticDataBuilder);

        HasCalculatedPowerConsumption = false;
    }

    private void InitializeInserters(Graph subFactoryGraph,
                                     SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                     BeltExecutor beltExecutor,
                                     OptimizedItemId[]?[]? fuelNeeds,
                                     SubFactoryNeeds subFactoryNeeds,
                                     UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter, BiInserterGrade>(_assemblerExecutor._assemblerNetworkIdAndStates,
                                                                                                  _producingLabNetworkIdAndStates,
                                                                                                  _researchingLabNetworkIdAndStates,
                                                                                                  subFactoryPowerSystemBuilder.FuelGeneratorSegments,
                                                                                                  fuelNeeds,
                                                                                                  subFactoryNeeds,
                                                                                                  _assemblerExecutor._producedSize,
                                                                                                  _assemblerExecutor._served,
                                                                                                  _assemblerExecutor._incServed,
                                                                                                  _assemblerExecutor._produced,
                                                                                                  _assemblerExecutor._assemblerRecipeIndexes,
                                                                                                  _assemblerExecutor._needToUpdateNeeds,
                                                                                                  _producingLabExecutor._producedSize,
                                                                                                  _producingLabExecutor._served,
                                                                                                  _producingLabExecutor._incServed,
                                                                                                  _producingLabExecutor._produced,
                                                                                                  _producingLabExecutor._labRecipeIndexes,
                                                                                                  _researchingLabExecutor._matrixServed,
                                                                                                  _researchingLabExecutor._matrixIncServed,
                                                                                                  _siloExecutor._siloIndexes,
                                                                                                  _ejectorExecutor._ejectorIndexes,
                                                                                                  universeStaticDataBuilder.UniverseStaticData);
        _optimizedBiInserterExecutor.Initialize(_planet, 
                                                this, 
                                                subFactoryGraph, 
                                                x => x.bidirectional, 
                                                subFactoryPowerSystemBuilder.CreateBiInserterBuilder(), 
                                                beltExecutor,
                                                universeStaticDataBuilder,
                                                universeStaticDataBuilder.BiInserterGrades);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter, InserterGrade>(_assemblerExecutor._assemblerNetworkIdAndStates,
                                                                                            _producingLabNetworkIdAndStates,
                                                                                            _researchingLabNetworkIdAndStates,
                                                                                            subFactoryPowerSystemBuilder.FuelGeneratorSegments,
                                                                                            fuelNeeds,
                                                                                            subFactoryNeeds,
                                                                                            _assemblerExecutor._producedSize,
                                                                                            _assemblerExecutor._served,
                                                                                            _assemblerExecutor._incServed,
                                                                                            _assemblerExecutor._produced,
                                                                                            _assemblerExecutor._assemblerRecipeIndexes,
                                                                                            _assemblerExecutor._needToUpdateNeeds,
                                                                                            _producingLabExecutor._producedSize,
                                                                                            _producingLabExecutor._served,
                                                                                            _producingLabExecutor._incServed,
                                                                                            _producingLabExecutor._produced,
                                                                                            _producingLabExecutor._labRecipeIndexes,
                                                                                            _researchingLabExecutor._matrixServed,
                                                                                            _researchingLabExecutor._matrixIncServed,
                                                                                            _siloExecutor._siloIndexes,
                                                                                            _ejectorExecutor._ejectorIndexes,
                                                                                            universeStaticDataBuilder.UniverseStaticData);
        _optimizedInserterExecutor.Initialize(_planet, 
                                              this, 
                                              subFactoryGraph, 
                                              x => !x.bidirectional, 
                                              subFactoryPowerSystemBuilder.CreateInserterBuilder(), 
                                              beltExecutor,
                                              universeStaticDataBuilder,
                                              universeStaticDataBuilder.InserterGrades);
    }

    private void InitializeAssemblers(Graph subFactoryGraph,
                                      SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                      SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                      SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                                      UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(_planet,
                                                subFactoryGraph,
                                                subFactoryPowerSystemBuilder,
                                                subFactoryProductionRegisterBuilder,
                                                subFactoryNeedsBuilder,
                                                universeStaticDataBuilder);
    }

    private void InitializeMiners(Graph subFactoryGraph,
                                  SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                  SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                  BeltExecutor beltExecutor,
                                  UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _beltVeinMinerExecutor = new VeinMinerExecutor<BeltMinerOutput>();
        _beltVeinMinerExecutor.Initialize(_planet,
                                          subFactoryGraph,
                                          subFactoryPowerSystemBuilder.CreateBeltVeinMinerBuilder(),
                                          subFactoryProductionRegisterBuilder,
                                          beltExecutor,
                                          universeStaticDataBuilder);

        _stationVeinMinerExecutor = new VeinMinerExecutor<StationMinerOutput>();
        _stationVeinMinerExecutor.Initialize(_planet,
                                             subFactoryGraph,
                                             subFactoryPowerSystemBuilder.CreateStationVeinMinerBuilder(),
                                             subFactoryProductionRegisterBuilder,
                                             beltExecutor,
                                             universeStaticDataBuilder);

        _oilMinerExecutor = new OilMinerExecutor();
        _oilMinerExecutor.Initialize(_planet,
                                     subFactoryGraph,
                                     subFactoryPowerSystemBuilder,
                                     subFactoryProductionRegisterBuilder,
                                     beltExecutor,
                                     universeStaticDataBuilder);

        _waterMinerExecutor = new WaterMinerExecutor();
        _waterMinerExecutor.Initialize(_planet,
                                       subFactoryGraph,
                                       subFactoryPowerSystemBuilder,
                                       subFactoryProductionRegisterBuilder,
                                       beltExecutor,
                                       universeStaticDataBuilder);
    }

    private void InitializeEjectors(Graph subFactoryGraph,
                                    SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                    SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                    SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                                    UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _ejectorExecutor = new EjectorExecutor();
        _ejectorExecutor.Initialize(_planet, 
                                    subFactoryGraph, 
                                    subFactoryPowerSystemBuilder, 
                                    subFactoryProductionRegisterBuilder, 
                                    subFactoryNeedsBuilder,
                                    universeStaticDataBuilder);
    }

    private void InitializeSilos(Graph subFactoryGraph,
                                 SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                 SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                 SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                                 UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _siloExecutor = new SiloExecutor();
        _siloExecutor.Initialize(_planet, 
                                 subFactoryGraph, 
                                 subFactoryPowerSystemBuilder, 
                                 subFactoryProductionRegisterBuilder, 
                                 subFactoryNeedsBuilder,
                                 universeStaticDataBuilder);
    }

    private void InitializeLabAssemblers(Graph subFactoryGraph,
                                         SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                         SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                         SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                                         UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(_planet, 
                                         subFactoryGraph, 
                                         subFactoryPowerSystemBuilder, 
                                         subFactoryProductionRegisterBuilder, 
                                         subFactoryNeedsBuilder,
                                         universeStaticDataBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(Graph subFactoryGraph,
                                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                           SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor(_starClusterResearchManager);
        _researchingLabExecutor.Initialize(_planet, 
                                           subFactoryGraph, 
                                           subFactoryPowerSystemBuilder, 
                                           subFactoryProductionRegisterBuilder, 
                                           subFactoryNeedsBuilder,
                                           universeStaticDataBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeMonitors(Graph subFactoryGraph, 
                                    SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, 
                                    BeltExecutor beltExecutor,
                                    UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _monitorExecutor = new MonitorExecutor();
        _monitorExecutor.Initialize(_planet, 
                                    subFactoryGraph, 
                                    subFactoryPowerSystemBuilder, 
                                    beltExecutor,
                                    universeStaticDataBuilder);
    }

    private void InitializeSpraycoaters(Graph subFactoryGraph, 
                                        SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, 
                                        SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder, 
                                        BeltExecutor beltExecutor,
                                        UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(_planet, 
                                        subFactoryGraph, 
                                        subFactoryPowerSystemBuilder, 
                                        subFactoryProductionRegisterBuilder, 
                                        beltExecutor,
                                        universeStaticDataBuilder);
    }

    private void InitializePilers(Graph subFactoryGraph, 
                                  SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, 
                                  BeltExecutor beltExecutor,
                                  UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _pilerExecutor = new PilerExecutor();
        _pilerExecutor.Initialize(_planet, 
                                  subFactoryGraph, 
                                  subFactoryPowerSystemBuilder, 
                                  beltExecutor,
                                  universeStaticDataBuilder);
    }

    private void InitializeFractionators(Graph subFactoryGraph, 
                                         SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder, 
                                         SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder, 
                                         BeltExecutor beltExecutor,
                                         UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _fractionatorExecutor = new FractionatorExecutor();
        _fractionatorExecutor.Initialize(_planet, 
                                         subFactoryGraph, 
                                         subFactoryPowerSystemBuilder, 
                                         subFactoryProductionRegisterBuilder, 
                                         beltExecutor,
                                         universeStaticDataBuilder);
    }

    private void InitializeStations(Graph subFactoryGraph,
                                    BeltExecutor beltExecutor,
                                    VeinMinerExecutor<StationMinerOutput> stationVeinMinerExecutor,
                                    UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _stationExecutor = new StationExecutor();
        _stationExecutor.Initialize(_planet,
                                    subFactoryGraph,
                                    beltExecutor,
                                    stationVeinMinerExecutor,
                                    universeStaticDataBuilder);
    }

    private void InitializeTanks(Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        _tankExecutor = new TankExecutor();
        _tankExecutor.Initialize(_planet, subFactoryGraph, beltExecutor);
    }

    private void InitializeBelts(Graph subFactoryGraph, 
                                 PlanetWideBeltExecutor planetWideBeltExecutor, 
                                 UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _beltExecutor = new BeltExecutor();
        _beltExecutor.Initialize(_planet, 
                                 subFactoryGraph,
                                 universeStaticDataBuilder);

        planetWideBeltExecutor.AddBeltExecutor(_beltExecutor);
    }

    private void InitializeSplitters(Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        _splitterExecutor = new SplitterExecutor();
        _splitterExecutor.Initialize(_planet, subFactoryGraph, beltExecutor);
    }

    public void GameTick(int workerIndex, long time, SubFactoryPowerConsumption powerSystem)
    {
        var miningFlags = new MiningFlags();
        long[] networkPowerConsumptions = powerSystem.NetworksPowerConsumption;
        Array.Clear(networkPowerConsumptions, 0, networkPowerConsumptions.Length);

        int[] productRegister = _optimizedProductionStatistics.ProductRegister;
        int[] consumeRegister = _optimizedProductionStatistics.ConsumeRegister;
        PowerConsumerType[] powerConsumerTypes = _universeStaticData.PowerConsumerTypes;

        int minerCount = _beltVeinMinerExecutor.Count + _stationVeinMinerExecutor.Count + _oilMinerExecutor.Count + _waterMinerExecutor.Count;
        if (minerCount > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Miner, workerIndex);
            _beltVeinMinerExecutor.GameTick(_planet, powerSystem.BeltVeinMinerPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, productRegister, ref miningFlags, _beltExecutor.OptimizedCargoPaths);
            _stationVeinMinerExecutor.GameTick(_planet, powerSystem.StationVeinMinerPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, productRegister, ref miningFlags, _beltExecutor.OptimizedCargoPaths);
            _oilMinerExecutor.GameTick(_planet, powerSystem.OilMinerPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, productRegister, _beltExecutor.OptimizedCargoPaths);
            _waterMinerExecutor.GameTick(_planet, powerSystem.WaterMinerPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, productRegister, _beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndSample(DPEntry.Miner, workerIndex);
        }

        if (_assemblerExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Assembler, workerIndex);
            _assemblerExecutor.GameTick(_planet, 
                                        powerSystem.AssemblerPowerConsumerTypeIndexes,
                                        powerConsumerTypes, 
                                        networkPowerConsumptions, 
                                        productRegister, 
                                        consumeRegister, 
                                        _subFactoryNeeds, 
                                        _universeStaticData);
            DeepProfiler.EndSample(DPEntry.Assembler, workerIndex);
        }

        if (_fractionatorExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Fractionator, workerIndex);
            _fractionatorExecutor.GameTick(_planet, 
                                           powerSystem.FractionatorPowerConsumerTypeIndexes,
                                           powerConsumerTypes, 
                                           networkPowerConsumptions, 
                                           productRegister, 
                                           consumeRegister, 
                                           _beltExecutor.OptimizedCargoPaths, 
                                           _universeStaticData);
            DeepProfiler.EndSample(DPEntry.Fractionator, workerIndex);
        }

        if (_ejectorExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Ejector, workerIndex);
            _ejectorExecutor.GameTick(_planet, time, powerSystem.EjectorPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, consumeRegister, _subFactoryNeeds);
            DeepProfiler.EndSample(DPEntry.Ejector, workerIndex);
        }

        if (_siloExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Silo, workerIndex);
            _siloExecutor.GameTick(_planet, powerSystem.SiloPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, consumeRegister, _subFactoryNeeds);
            DeepProfiler.EndSample(DPEntry.Silo, workerIndex);
        }

        int labCount = _producingLabExecutor.Count + _researchingLabExecutor.Count;
        if (labCount > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Lab, workerIndex);
            if (_producingLabExecutor.Count > 0)
            {
                _producingLabExecutor.GameTickLabProduceMode(_planet, 
                                                             powerSystem.ProducingLabPowerConsumerTypeIndexes,
                                                             powerConsumerTypes, 
                                                             networkPowerConsumptions, 
                                                             productRegister, 
                                                             consumeRegister, 
                                                             _subFactoryNeeds,
                                                             _universeStaticData);
                _producingLabExecutor.GameTickLabOutputToNext(_subFactoryNeeds,
                                                              _universeStaticData);
            }
            if (_researchingLabExecutor.Count > 0)
            {
                _researchingLabExecutor.GameTickLabResearchMode(_planet, powerSystem.ResearchingLabPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, consumeRegister, _subFactoryNeeds);
                _researchingLabExecutor.GameTickLabOutputToNext(_subFactoryNeeds);
            }
            DeepProfiler.EndMajorSample(DPEntry.Lab, workerIndex);
        }

        if (_stationExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Transport, workerIndex, _planet.planetId);
            DeepProfiler.BeginMajorSample(DPEntry.Station, workerIndex);
            _stationExecutor.StationGameTick(_planet, time, _stationVeinMinerExecutor, ref miningFlags);
            DeepProfiler.EndMajorSample(DPEntry.Station, workerIndex);
            DeepProfiler.EndSample(DPEntry.Transport, workerIndex);
        }

        if (_stationExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Station, workerIndex);
            _stationExecutor.InputFromBelt(_beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Station, workerIndex);
        }

        int inserterCount = _optimizedBiInserterExecutor.Count + _optimizedInserterExecutor.Count;
        if (inserterCount > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Inserter, workerIndex);
            _optimizedBiInserterExecutor.GameTickInserters(_planet, 
                                                           powerSystem.InserterBiPowerConsumerTypeIndexes,
                                                           powerConsumerTypes, 
                                                           networkPowerConsumptions, 
                                                           _beltExecutor.OptimizedCargoPaths, 
                                                           _universeStaticData);
            _optimizedInserterExecutor.GameTickInserters(_planet, 
                                                         powerSystem.InserterPowerConsumerTypeIndexes,
                                                         powerConsumerTypes, 
                                                         networkPowerConsumptions, 
                                                         _beltExecutor.OptimizedCargoPaths, 
                                                         _universeStaticData);
            DeepProfiler.EndMajorSample(DPEntry.Inserter, workerIndex);
        }

        if (_tankExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Storage, workerIndex);
            // Storage has no logic on planets the player isn't on which is why it is omitted
            _tankExecutor.GameTick(_beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Storage, workerIndex);
        }

        if (_beltExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Belt, workerIndex);
            _beltExecutor.GameTick();
            DeepProfiler.EndMajorSample(DPEntry.Belt, workerIndex);
        }

        if (_splitterExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Splitter, workerIndex);
            _splitterExecutor.GameTick(this, _beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Splitter, workerIndex);
        }

        if (_monitorExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Monitor, workerIndex);
            _monitorExecutor.GameTick(_planet, powerSystem.MonitorPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, _beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Monitor, workerIndex);
        }

        if (_spraycoaterExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Spraycoater, workerIndex);
            _spraycoaterExecutor.GameTick(powerSystem.SpraycoaterPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, consumeRegister, _beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Spraycoater, workerIndex);
        }

        if (_pilerExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Piler, workerIndex);
            _pilerExecutor.GameTick(_planet, powerSystem.PilerPowerConsumerTypeIndexes, powerConsumerTypes, networkPowerConsumptions, _beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Piler, workerIndex);
        }

        if (_stationExecutor.Count > 0)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Station, workerIndex);
            _stationExecutor.OutputToBelt(_beltExecutor.OptimizedCargoPaths);
            DeepProfiler.EndMajorSample(DPEntry.Station, workerIndex);
        }

        _optimizedPlanet.AddMiningFlags(miningFlags);

        HasCalculatedPowerConsumption = true;
    }

    public void RefreshPowerConsumptionDemands(ProductionStatistics statistics, SubFactoryPowerConsumption powerSystem)
    {
        PowerConsumerType[] powerConsumerTypes = _universeStaticData.PowerConsumerTypes;
        RefreshPowerConsumptionDemands(statistics, _beltVeinMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.BeltVeinMinerPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _stationVeinMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.StationVeinMinerPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _oilMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.OilMinerPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _waterMinerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.WaterMinerPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _assemblerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.AssemblerPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _fractionatorExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.FractionatorPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _ejectorExecutor.UpdatePowerConsumptionPerPrototype(_planet, powerSystem.EjectorPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _siloExecutor.UpdatePowerConsumptionPerPrototype(_planet, powerSystem.SiloPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _producingLabExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.ProducingLabPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _researchingLabExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.ResearchingLabPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _optimizedBiInserterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.InserterBiPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _optimizedInserterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.InserterPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _monitorExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.MonitorPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _spraycoaterExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.SpraycoaterPowerConsumerTypeIndexes, powerConsumerTypes));
        RefreshPowerConsumptionDemands(statistics, _pilerExecutor.UpdatePowerConsumptionPerPrototype(powerSystem.PilerPowerConsumerTypeIndexes, powerConsumerTypes));
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
            return new TypedObjectIndex(EntityType.Ejector, _ejectorExecutor.GetOptimizedEjectorIndex(entity.ejectorId));
        }
        else if (entity.siloId != 0)
        {
            return new TypedObjectIndex(EntityType.Silo, _siloExecutor.GetOptimizedSiloIndex(entity.siloId));
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

    public bool InsertCargoIntoStorage(int entityId, OptimizedCargo cargo, bool useBan = true)
    {
        int storageId = _planet.entityPool[entityId].storageId;
        if (storageId > 0)
        {
            StorageComponent storageComponent = _planet.factoryStorage.storagePool[storageId];
            while (storageComponent != null)
            {
                if (!useBan || storageComponent.lastFullItem != cargo.Item)
                {
                    if (AddCargo(storageComponent, cargo, useBan))
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

    private static bool AddCargo(StorageComponent storage, OptimizedCargo cargo, bool useBan = false)
    {
        if (cargo.Item <= 0 || cargo.Stack == 0 || cargo.Item >= 12000)
        {
            return false;
        }
        bool flag = storage.type > EStorageType.Default;
        if (flag)
        {
            if (storage.type == EStorageType.Fuel && !StorageComponent.itemIsFuel[cargo.Item])
            {
                return false;
            }
            if (storage.type == EStorageType.Ammo && (!StorageComponent.itemIsAmmo[cargo.Item] || StorageComponent.itemIsBomb[cargo.Item]))
            {
                return false;
            }
            if (storage.type == EStorageType.Bomb && !StorageComponent.itemIsBomb[cargo.Item])
            {
                return false;
            }
            if (storage.type == EStorageType.Fighter && !StorageComponent.itemIsFighter[cargo.Item])
            {
                return false;
            }
        }
		bool result = false;
		bool flag2 = false;
		int num = 0;
		int num2 = (useBan ? (storage.size - storage.bans) : storage.size);
		ref byte stack = ref cargo.Stack;
        for (int i = 0; i < num2; i++)
        {
            if (storage.grids[i].itemId == 0)
            {
                if (flag && (storage.type == EStorageType.DeliveryFiltered || storage.grids[i].filter > 0) && cargo.Item != storage.grids[i].filter)
                {
                    continue;
                }
                if (num == 0)
                {
                    num = StorageComponent.itemStackCount[cargo.Item];
                }
                storage.grids[i].itemId = cargo.Item;
                if (storage.grids[i].filter == 0)
                {
                    storage.grids[i].stackSize = num;
                }
            }
            if (storage.grids[i].itemId == cargo.Item)
            {
                if (num == 0)
                {
                    num = storage.grids[i].stackSize;
                }
                int num3 = num - storage.grids[i].count;
                if (stack <= num3)
                {
                    storage.grids[i].count += stack;
                    storage.grids[i].inc += cargo.Inc;
                    result = true;
                    flag2 = true;
                    break;
                }
                storage.grids[i].count = num;
                storage.grids[i].inc += storage.split_inc(ref stack, ref cargo.Inc, (byte)num3);
                flag2 = true;
            }
        }
        if (flag2)
        {
            storage.searchStart = 0;
            storage.lastEmptyItem = -1;
            storage.NotifyStorageChange();
        }
        return result;
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