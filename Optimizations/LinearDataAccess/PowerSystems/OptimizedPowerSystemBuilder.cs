using System.Collections.Generic;
using System.Linq;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystemBuilder
{
    private readonly PlanetFactory _planet;
    private readonly List<PowerConsumerType> _powerConsumerTypes = [];
    private readonly Dictionary<PowerConsumerType, int> _powerConsumerTypeToIndex = [];
    private readonly List<int> _assemblerPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterBiPowerConsumerTypeIndexes = [];
    private readonly List<int> _inserterPowerConsumerTypeIndexes = [];
    private readonly List<int> _producingLabPowerConsumerTypeIndexes = [];
    private readonly List<int> _researchingLabPowerConsumerTypeIndexes = [];
    private readonly Dictionary<OptimizedSubFactory, List<int>> _subFactoryToSpraycoaterPowerConsumerTypeIndexes = [];
    private readonly List<int> _fractionatorPowerConsumerTypeIndexes = [];
    private readonly List<int> _pilerPowerConsumerTypeIndexes = [];
    private readonly List<int> _monitorPowerConsumerTypeIndexes = [];
    private readonly List<int> _waterMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _oilMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _beltVeinMinerPowerConsumerTypeIndexes = [];
    private readonly List<int> _stationVeinMinerPowerConsumerTypeIndexes = [];
    private readonly Dictionary<int, HashSet<int>> _networkIndexToOptimizedConsumerIndexes = [];
    private readonly Dictionary<OptimizedSubFactory, long[]> _subFactoryToNetworkPowerConsumptions = [];

    public OptimizedPowerSystemBuilder(PlanetFactory planet)
    {
        _planet = planet;
    }

    public void AddSubFactory(OptimizedSubFactory subFactory)
    {
        _subFactoryToNetworkPowerConsumptions.Add(subFactory, new long[_planet.powerSystem.netCursor]);
        _subFactoryToSpraycoaterPowerConsumerTypeIndexes.Add(subFactory, []);
    }

    public void AddAssembler(ref readonly AssemblerComponent assembler, int networkIndex)
    {
        AddEntity(_assemblerPowerConsumerTypeIndexes, assembler.pcId, networkIndex);
    }

    public OptimizedPowerSystemInserterBuilder CreateBiInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_planet.powerSystem, this, _inserterBiPowerConsumerTypeIndexes);
    }

    public OptimizedPowerSystemInserterBuilder CreateInserterBuilder()
    {
        return new OptimizedPowerSystemInserterBuilder(_planet.powerSystem, this, _inserterPowerConsumerTypeIndexes);
    }

    public void AddProducingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_producingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddResearchingLab(ref readonly LabComponent lab, int networkIndex)
    {
        AddEntity(_researchingLabPowerConsumerTypeIndexes, lab.pcId, networkIndex);
    }

    public void AddSpraycoater(OptimizedSubFactory subFactory, ref readonly SpraycoaterComponent spraycoater, int networkIndex)
    {
        AddEntity(_subFactoryToSpraycoaterPowerConsumerTypeIndexes[subFactory], spraycoater.pcId, networkIndex);
    }

    public void AddFractionator(ref readonly FractionatorComponent fractionator, int networkIndex)
    {
        AddEntity(_fractionatorPowerConsumerTypeIndexes, fractionator.pcId, networkIndex);
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

        OptimizedPowerNetwork[] optimizedPowerNetworks = GetOptimizedPowerNetworks(_planet, planetWideBeltExecutor, _networkIndexToOptimizedConsumerIndexes);

        return new OptimizedPowerSystem(_powerConsumerTypes.ToArray(),
                                        _assemblerPowerConsumerTypeIndexes.ToArray(),
                                        _inserterBiPowerConsumerTypeIndexes.ToArray(),
                                        _inserterPowerConsumerTypeIndexes.ToArray(),
                                        _producingLabPowerConsumerTypeIndexes.ToArray(),
                                        _researchingLabPowerConsumerTypeIndexes.ToArray(),
                                        _subFactoryToSpraycoaterPowerConsumerTypeIndexes.ToDictionary(x => x.Key, x => x.Value.ToArray()),
                                        _fractionatorPowerConsumerTypeIndexes.ToArray(),
                                        _pilerPowerConsumerTypeIndexes.ToArray(),
                                        _monitorPowerConsumerTypeIndexes.ToArray(),
                                        _waterMinerPowerConsumerTypeIndexes.ToArray(),
                                        _oilMinerPowerConsumerTypeIndexes.ToArray(),
                                        _beltVeinMinerPowerConsumerTypeIndexes.ToArray(),
                                        _stationVeinMinerPowerConsumerTypeIndexes.ToArray(),
                                        optimizedPowerNetworks,
                                        _subFactoryToNetworkPowerConsumptions);
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

    private void AddEntity(List<int> powerConsumerTypeIndexes, int powerConsumerIndex, int networkIndex)
    {
        PowerConsumerComponent powerConsumerComponent = _planet.powerSystem.consumerPool[powerConsumerIndex];
        PowerConsumerType powerConsumerType = new PowerConsumerType(powerConsumerComponent.workEnergyPerTick, powerConsumerComponent.idleEnergyPerTick);
        powerConsumerTypeIndexes.Add(GetOrAddPowerConsumerType(powerConsumerType));

        AddPowerConsumerIndexToNetwork(powerConsumerIndex, networkIndex);
    }

    private static OptimizedPowerNetwork[] GetOptimizedPowerNetworks(PlanetFactory planet,
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
                networkNonOptimizedPowerConsumerIndexes = planet.powerSystem.netPool[i].consumers.Except(networkIndexToOptimizedConsumerIndexes[i])
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

            var fuelExecutor = new FuelGeneratorExecutor();
            fuelExecutor.Initialize(planet, i);

            var powerExchangerExecutor = new PowerExchangerExecutor();
            powerExchangerExecutor.Initialize(planet, i, planetWideBeltExecutor);

            optimizedPowerNetworks.Add(new OptimizedPowerNetwork(powerNetwork,
                                                                 i,
                                                                 networkNonOptimizedPowerConsumerIndexes,
                                                                 windExecutor,
                                                                 solarExecutor,
                                                                 gammaExecutor,
                                                                 geothermalExecutor,
                                                                 fuelExecutor,
                                                                 powerExchangerExecutor));
        }

        return optimizedPowerNetworks.ToArray();
    }
}