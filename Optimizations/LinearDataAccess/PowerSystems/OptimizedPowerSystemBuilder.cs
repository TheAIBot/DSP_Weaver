using PowerNetworkStructures;
using System.Collections.Generic;
using System.Linq;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class SubFactoryPowerSystemBuilder
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedPowerSystemBuilder _optimizedPowerSystemBuilder;
    private readonly List<int> _assemblerPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterBiPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterPowerConsumerTypeIndexes = [];
    private readonly List<int> _producingLabPowerConsumerTypeIndexes = [];
    private readonly List<int> _researchingLabPowerConsumerTypeIndexes = [];
    private readonly List<int> _spraycoaterPowerConsumerTypeIndexes = [];
    private readonly List<int> _fractionatorPowerConsumerTypeIndexes = [];
    private readonly List<int> _ejectorPowerConsumerTypeIndexes = [];
    private readonly List<int> _siloPowerConsumerTypeIndexes = [];
    private readonly List<int> _pilerPowerConsumerTypeIndexes = [];
    private readonly List<int> _monitorPowerConsumerTypeIndexes = [];
    private readonly List<int> _waterMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _oilMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _beltVeinMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _stationVeinMinerPowerConsumerTypeIndexes = [];
    private readonly long[] _networksPowerConsumptions;
    private readonly Dictionary<int, OptimizedFuelGeneratorLocation> _fuelGeneratorIdToOptimizedFueldGeneratorLocation;
    public OptimizedFuelGenerator[][] FuelGeneratorSegments { get; }

    public SubFactoryPowerSystemBuilder(PlanetFactory planet,
                                        OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                                        long[] networksPowerConsumptions,
                                        Dictionary<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdToOptimizedFueldGeneratorLocation,
                                        OptimizedFuelGenerator[][] fuelGeneratorSegments)
    {
        _planet = planet;
        _optimizedPowerSystemBuilder = optimizedPowerSystemBuilder;
        _networksPowerConsumptions = networksPowerConsumptions;
        _fuelGeneratorIdToOptimizedFueldGeneratorLocation = fuelGeneratorIdToOptimizedFueldGeneratorLocation;
        FuelGeneratorSegments = fuelGeneratorSegments;
    }

    public void AddAssembler(ref readonly AssemblerComponent assembler, int networkIndex)
    {
        AddEntity(_assemblerPowerConsumerTypeIndexes, assembler.pcId, networkIndex);
    }

    public OptimizedPowerSystemInserterBuilder CreateBiInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_planet.powerSystem, this, _inserterBiPowerConsumerTypeIndexes, _fuelGeneratorIdToOptimizedFueldGeneratorLocation);
    }

    public OptimizedPowerSystemInserterBuilder CreateInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_planet.powerSystem, this, _inserterPowerConsumerTypeIndexes, _fuelGeneratorIdToOptimizedFueldGeneratorLocation);
    }

    public void AddProducingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_producingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddResearchingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_researchingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddSpraycoater(ref readonly SpraycoaterComponent spraycoater, int networkIndex)
    {
        AddEntity(_spraycoaterPowerConsumerTypeIndexes, spraycoater.pcId, networkIndex);
    }

    public void AddFractionator(ref readonly FractionatorComponent fractionator, int networkIndex)
    {
        AddEntity(_fractionatorPowerConsumerTypeIndexes, fractionator.pcId, networkIndex);
    }

    public void AddEjector(ref readonly EjectorComponent ejector, int networkIndex)
    {
        AddEntity(_ejectorPowerConsumerTypeIndexes, ejector.pcId, networkIndex);
    }

    public void AddSilo(ref readonly SiloComponent silo, int networkIndex)
    {
        AddEntity(_siloPowerConsumerTypeIndexes, silo.pcId, networkIndex);
    }

    public void AddPiler(ref readonly PilerComponent piler, int networkIndex)
    {
        AddEntity(_pilerPowerConsumerTypeIndexes, piler.pcId, networkIndex);
    }

    public void AddMonitor(ref readonly MonitorComponent monitor, int networkIndex)
    {
        AddEntity(_monitorPowerConsumerTypeIndexes, monitor.pcId, networkIndex);
    }

    public void AddWaterMiner(ref readonly MinerComponent miner, int networkIndex)
    {
        AddEntity(_waterMinerPowerConsumerTypeIndexes, miner.pcId, networkIndex);
    }

    public void AddOilMiner(ref readonly MinerComponent miner, int networkIndex)
    {
        AddEntity(_oilMinerPowerConsumerTypeIndexes, miner.pcId, networkIndex);
    }

    public OptimizedPowerSystemVeinMinerBuilder CreateBeltVeinMinerBuilder()
    {
        return new OptimizedPowerSystemVeinMinerBuilder(_planet.powerSystem, this, _beltVeinMinerPowerConsumerTypeIndexes);
    }

    public OptimizedPowerSystemVeinMinerBuilder CreateStationVeinMinerBuilder()
    {
        return new OptimizedPowerSystemVeinMinerBuilder(_planet.powerSystem, this, _stationVeinMinerPowerConsumerTypeIndexes);
    }

    public void AddEntity(List<int> powerConsumerTypeIndexes, int powerConsumerIndex, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _planet.powerSystem.consumerPool[powerConsumerIndex];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        powerConsumerTypeIndexes.Add(_optimizedPowerSystemBuilder.GetOrAddPowerConsumerType(powerConsumerType));

        _optimizedPowerSystemBuilder.AddPowerConsumerIndexToNetwork(powerConsumerIndex, networkIndex);
    }

    public SubFactoryPowerConsumption Build(PowerConsumerType[] powerConsumerTypes)
    {
        return new SubFactoryPowerConsumption(powerConsumerTypes,
                                              _assemblerPowerConsumerTypeIndexes.ToArray(),
                                              _inserterBiPowerConsumerTypeIndexes.ToArray(),
                                              _inserterPowerConsumerTypeIndexes.ToArray(),
                                              _producingLabPowerConsumerTypeIndexes.ToArray(),
                                              _researchingLabPowerConsumerTypeIndexes.ToArray(),
                                              _spraycoaterPowerConsumerTypeIndexes.ToArray(),
                                              _fractionatorPowerConsumerTypeIndexes.ToArray(),
                                              _ejectorPowerConsumerTypeIndexes.ToArray(),
                                              _siloPowerConsumerTypeIndexes.ToArray(),
                                              _pilerPowerConsumerTypeIndexes.ToArray(),
                                              _monitorPowerConsumerTypeIndexes.ToArray(),
                                              _waterMinerPowerConsumerTypeIndexes.ToArray(),
                                              _oilMinerPowerConsumerTypeIndexes.ToArray(),
                                              _beltVeinMinerPowerConsumerTypeIndexes.ToArray(),
                                              _stationVeinMinerPowerConsumerTypeIndexes.ToArray(),
                                              _networksPowerConsumptions);
    }
}

internal sealed class OptimizedPowerSystemBuilder
{
    private readonly PlanetFactory _planet;
    private readonly List<PowerConsumerType> _powerConsumerTypes = [];
    private readonly Dictionary<PowerConsumerType, int> _powerConsumerTypeToIndex = [];
    private readonly Dictionary<int, HashSet<int>> _networkIndexToOptimizedConsumerIndexes = [];
    private readonly Dictionary<OptimizedSubFactory, SubFactoryPowerSystemBuilder> _subFactoryToPowerSystemBuilder = [];
    private readonly Dictionary<int, FuelGeneratorExecutor> _networkIndexToFueldGeneratorExecutor;
    private readonly Dictionary<int, OptimizedFuelGeneratorLocation> _fuelGeneratorIdToOptimizedFueldGeneratorLocation;
    private readonly OptimizedFuelGenerator[][] _fuelGeneratorSegments;

    private OptimizedPowerSystemBuilder(PlanetFactory planet,
                                       Dictionary<int, FuelGeneratorExecutor> networkIndexToFueldGeneratorExecutor,
                                       Dictionary<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdToOptimizedFueldGeneratorLocation,
                                       OptimizedFuelGenerator[][] fuelGeneratorSegments)
    {
        _planet = planet;
        _networkIndexToFueldGeneratorExecutor = networkIndexToFueldGeneratorExecutor;
        _fuelGeneratorIdToOptimizedFueldGeneratorLocation = fuelGeneratorIdToOptimizedFueldGeneratorLocation;
        _fuelGeneratorSegments = fuelGeneratorSegments;
    }

    public static OptimizedPowerSystemBuilder Create(PlanetFactory planet)
    {
        Dictionary<int, FuelGeneratorExecutor> networkIndexToFueldGeneratorExecutor = [];
        Dictionary<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdToOptimizedFueldGeneratorLocation = [];
        List<OptimizedFuelGenerator[]> fuelGeneratorSegments = [];
        for (int networkIndex = 1; networkIndex < planet.powerSystem.netCursor; networkIndex++)
        {
            PowerNetwork? powerNetwork = planet.powerSystem.netPool[networkIndex];
            if (powerNetwork == null || powerNetwork.id != networkIndex)
            {
                continue;
            }

            var fuelExecutor = new FuelGeneratorExecutor();
            fuelExecutor.Initialize(planet, networkIndex);
            networkIndexToFueldGeneratorExecutor.Add(networkIndex, fuelExecutor);

            foreach (KeyValuePair<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdWithOptimizedFuelGeneratorLocation in fuelExecutor.FuelGeneratorIdToOptimizedFuelGeneratorLocation)
            {
                fuelGeneratorIdToOptimizedFueldGeneratorLocation.Add(fuelGeneratorIdWithOptimizedFuelGeneratorLocation.Key,
                                                                     new OptimizedFuelGeneratorLocation(fuelGeneratorSegments.Count + fuelGeneratorIdWithOptimizedFuelGeneratorLocation.Value.SegmentIndex,
                                                                                                        fuelGeneratorIdWithOptimizedFuelGeneratorLocation.Value.Index));
            }
            fuelGeneratorSegments.AddRange(fuelExecutor.GeneratorSegments);
        }

        return new OptimizedPowerSystemBuilder(planet,
                                               networkIndexToFueldGeneratorExecutor,
                                               fuelGeneratorIdToOptimizedFueldGeneratorLocation,
                                               fuelGeneratorSegments.ToArray());
    }

    public SubFactoryPowerSystemBuilder AddSubFactory(OptimizedSubFactory subFactory)
    {
        var subFactoryPowerSystemBuilder = new SubFactoryPowerSystemBuilder(_planet,
                                                                            this,
                                                                            new long[_planet.powerSystem.netCursor],
                                                                            _fuelGeneratorIdToOptimizedFueldGeneratorLocation,
                                                                            _fuelGeneratorSegments);
        _subFactoryToPowerSystemBuilder.Add(subFactory, subFactoryPowerSystemBuilder);
        return subFactoryPowerSystemBuilder;
    }

    public int GetOrAddPowerConsumerType(PowerConsumerType powerConsumerType)
    {
        if (!_powerConsumerTypeToIndex.TryGetValue(powerConsumerType, out int index))
        {
            _powerConsumerTypeToIndex.Add(powerConsumerType, _powerConsumerTypes.Count);
            index = _powerConsumerTypes.Count;
            _powerConsumerTypes.Add(powerConsumerType);
        }

        return index;
    }

    public void AddPowerConsumerIndexToNetwork(int powerConsumerIndex, int networkIndex)
    {
        if (!_networkIndexToOptimizedConsumerIndexes.TryGetValue(networkIndex, out HashSet<int> optimizedConsumerIndexes))
        {
            optimizedConsumerIndexes = [];
            _networkIndexToOptimizedConsumerIndexes.Add(networkIndex, optimizedConsumerIndexes);
        }

        optimizedConsumerIndexes.Add(powerConsumerIndex);
    }

    public OptimizedPowerSystem Build(PlanetWideBeltExecutor planetWideBeltExecutor)
    {
        int[][] networkNonOptimizedPowerConsumerIndexes = new int[_planet.powerSystem.netCursor][];
        for (int i = 0; i < networkNonOptimizedPowerConsumerIndexes.Length; i++)
        {
            if (!_networkIndexToOptimizedConsumerIndexes.TryGetValue(i, out HashSet<int> optimizedConsumerIndexes))
            {
                networkNonOptimizedPowerConsumerIndexes[i] = _planet.powerSystem.netPool[i].consumers.ToArray();
                continue;
            }

            networkNonOptimizedPowerConsumerIndexes[i] = _planet.powerSystem.netPool[i].consumers.Except(optimizedConsumerIndexes).ToArray();
        }

        PowerConsumerType[] powerConsumerTypes = _powerConsumerTypes.ToArray();
        OptimizedPowerNetwork[] optimizedPowerNetworks = GetOptimizedPowerNetworks(_planet, planetWideBeltExecutor, _networkIndexToOptimizedConsumerIndexes);
        Dictionary<OptimizedSubFactory, SubFactoryPowerConsumption> subFactoryToPowerConsumption = _subFactoryToPowerSystemBuilder.ToDictionary(x => x.Key, x => x.Value.Build(powerConsumerTypes));

        return new OptimizedPowerSystem(optimizedPowerNetworks,
                                        subFactoryToPowerConsumption);
    }

    private OptimizedPowerNetwork[] GetOptimizedPowerNetworks(PlanetFactory planet,
                                                              PlanetWideBeltExecutor planetWideBeltExecutor,
                                                              Dictionary<int, HashSet<int>> networkIndexToOptimizedConsumerIndexes)
    {
        List<OptimizedPowerNetwork> optimizedPowerNetworks = [];

        for (int i = 1; i < planet.powerSystem.netCursor; i++)
        {
            PowerNetwork? powerNetwork = planet.powerSystem.netPool[i];
            if (powerNetwork == null || powerNetwork.id != i)
            {
                continue;
            }

            int[] networkNonOptimizedPowerConsumerIndexes;
            if (networkIndexToOptimizedConsumerIndexes.TryGetValue(i, out HashSet<int> optimizedConsumerIndexes))
            {
                networkNonOptimizedPowerConsumerIndexes = planet.powerSystem.netPool[i].consumers.Except(optimizedConsumerIndexes)
                                                                                                 .OrderBy(x => x)
                                                                                                 .ToArray();
            }
            else
            {
                networkNonOptimizedPowerConsumerIndexes = planet.powerSystem.netPool[i].consumers.ToArray();
            }

            var windExecutor = new WindGeneratorExecutor();
            windExecutor.Initialize(planet, i);

            var solarExecutor = new SolarGeneratorExecutor();
            solarExecutor.Initialize(planet, i);

            var gammaExecutor = new GammaPowerGeneratorExecutor();
            gammaExecutor.Initialize(planet, i, planetWideBeltExecutor);

            var geothermalExecutor = new GeothermalGeneratorExecutor();
            geothermalExecutor.Initialize(planet, i);

            var fuelExecutor = _networkIndexToFueldGeneratorExecutor[i];

            var powerExchangerExecutor = new PowerExchangerExecutor();
            powerExchangerExecutor.Initialize(planet, i, planetWideBeltExecutor);

            long totalPowerNodeEnergyConsumption = GetTotalPowerNodeEnergyConsumption(planet, powerNetwork);

            optimizedPowerNetworks.Add(new OptimizedPowerNetwork(powerNetwork,
                                                                 i,
                                                                 networkNonOptimizedPowerConsumerIndexes,
                                                                 windExecutor,
                                                                 solarExecutor,
                                                                 gammaExecutor,
                                                                 geothermalExecutor,
                                                                 fuelExecutor,
                                                                 powerExchangerExecutor,
                                                                 totalPowerNodeEnergyConsumption));
        }

        return optimizedPowerNetworks.ToArray();
    }

    private static long GetTotalPowerNodeEnergyConsumption(PlanetFactory planet, PowerNetwork powerNetwork)
    {
        long totalPowerNodeEnergyConsumption = 0;
        foreach (Node node in powerNetwork.nodes)
        {
            int id = node.id;
            if (planet.powerSystem.nodePool[id].id != id || !planet.powerSystem.nodePool[id].isCharger)
            {
                continue;
            }

            planet.powerSystem.nodePool[id].requiredEnergy = planet.powerSystem.nodePool[id].idleEnergyPerTick;
            totalPowerNodeEnergyConsumption += planet.powerSystem.nodePool[id].requiredEnergy;
        }

        return totalPowerNodeEnergyConsumption;
    }
}