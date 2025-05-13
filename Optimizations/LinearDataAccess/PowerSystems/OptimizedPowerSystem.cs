using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystem
{
    private readonly PowerConsumerType[] _powerConsumerTypes;
    private readonly int[] _assemblerPowerConsumerTypeIndexes;
    private readonly int[] _inserterBiPowerConsumerTypeIndexes;
    private readonly int[] _inserterPowerConsumerTypeIndexes;
    private readonly int[] _producingLabPowerConsumerTypeIndexes;
    private readonly int[] _researchingLabPowerConsumerTypeIndexes;
    private readonly Dictionary<OptimizedSubFactory, int[]> _subFactoryToSpraycoaterPowerConsumerTypeIndexes;
    private readonly int[] _fractionatorPowerConsumerTypeIndexes;
    private readonly int[] _pilerPowerConsumerTypeIndexes;
    private readonly int[] _monitorPowerConsumerTypeIndexes;
    private readonly int[] _waterMinerPowerConsumerTypeIndexes;
    private readonly int[] _oilMinerPowerConsumerTypeIndexes;
    private readonly int[] _beltVeinMinerPowerConsumerTypeIndexes;
    private readonly int[] _stationVeinMinerPowerConsumerTypeIndexes;
    private readonly OptimizedPowerNetwork[] _optimizedPowerNetworks;
    private readonly Dictionary<OptimizedSubFactory, long[]> _subFactoryToNetworkPowerConsumptions;

    public OptimizedPowerSystem(PowerConsumerType[] powerConsumerTypes,
                                int[] assemblerPowerConsumerTypeIndexes,
                                int[] inserterBiPowerConsumerTypeIndexes,
                                int[] inserterPowerConsumerTypeIndexes,
                                int[] producingLabPowerConsumerTypeIndexes,
                                int[] researchingLabPowerConsumerTypeIndexes,
                                Dictionary<OptimizedSubFactory, int[]> subFactoryToSpraycoaterPowerConsumerTypeIndexes,
                                int[] fractionatorPowerConsumerTypeIndexes,
                                int[] pilerPowerConsumerTypeIndexes,
                                int[] monitorPowerConsumerTypeIndexes,
                                int[] waterMinerPowerConsumerTypeIndexes,
                                int[] oilMinerPowerConsumerTypeIndexes,
                                int[] beltVeinMinerPowerConsumerTypeIndexes,
                                int[] stationVeinMinerPowerConsumerTypeIndexes,
                                OptimizedPowerNetwork[] optimizedPowerNetworks,
                                Dictionary<OptimizedSubFactory, long[]> subFactoryToNetworkPowerConsumptions)
    {
        _powerConsumerTypes = powerConsumerTypes;
        _assemblerPowerConsumerTypeIndexes = assemblerPowerConsumerTypeIndexes;
        _inserterBiPowerConsumerTypeIndexes = inserterBiPowerConsumerTypeIndexes;
        _inserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
        _producingLabPowerConsumerTypeIndexes = producingLabPowerConsumerTypeIndexes;
        _researchingLabPowerConsumerTypeIndexes = researchingLabPowerConsumerTypeIndexes;
        _subFactoryToSpraycoaterPowerConsumerTypeIndexes = subFactoryToSpraycoaterPowerConsumerTypeIndexes;
        _fractionatorPowerConsumerTypeIndexes = fractionatorPowerConsumerTypeIndexes;
        _pilerPowerConsumerTypeIndexes = pilerPowerConsumerTypeIndexes;
        _monitorPowerConsumerTypeIndexes = monitorPowerConsumerTypeIndexes;
        _waterMinerPowerConsumerTypeIndexes = waterMinerPowerConsumerTypeIndexes;
        _oilMinerPowerConsumerTypeIndexes = oilMinerPowerConsumerTypeIndexes;
        _beltVeinMinerPowerConsumerTypeIndexes = beltVeinMinerPowerConsumerTypeIndexes;
        _stationVeinMinerPowerConsumerTypeIndexes = stationVeinMinerPowerConsumerTypeIndexes;
        _optimizedPowerNetworks = optimizedPowerNetworks;
        _subFactoryToNetworkPowerConsumptions = subFactoryToNetworkPowerConsumptions;
    }

    public void RequestDysonSpherePower(PlanetFactory planet)
    {
        PowerSystem powerSystem = planet.powerSystem;
        powerSystem.dysonSphere = powerSystem.factory.gameData.dysonSpheres[powerSystem.planet.star.index];
        float eta = 1f - GameMain.history.solarEnergyLossRate;
        float increase = ((powerSystem.dysonSphere != null) ? ((float)((double)powerSystem.dysonSphere.grossRadius / ((double)powerSystem.planet.sunDistance * 40000.0))) : 0f);
        UnityEngine.Vector3 normalized = powerSystem.planet.runtimeLocalSunDirection.normalized;

        long energySum = 0L;
        bool flag = false;
        OptimizedPowerNetwork[] optimizedPowerNetworks = _optimizedPowerNetworks;
        for (int i = 0; i < optimizedPowerNetworks.Length; i++)
        {
            (long powerNetworkEnergySum, bool flag1) = optimizedPowerNetworks[i].RequestDysonSpherePower(powerSystem, eta, increase, normalized);
            energySum += powerNetworkEnergySum;
            flag |= flag1;
        }

        if (powerSystem.dysonSphere == null && flag)
        {
            powerSystem.dysonSphere = powerSystem.factory.CheckOrCreateDysonSphere();
        }
        if (powerSystem.dysonSphere != null)
        {
            powerSystem.dysonSphere.energyReqCurrentTick += energySum;
        }
    }

    public void BeforePower(PlanetFactory planet, OptimizedSubFactory subFactory)
    {
        long[] thisSubFactoryNetworkPowerConsumption = _subFactoryToNetworkPowerConsumptions[subFactory];
        Array.Clear(thisSubFactoryNetworkPowerConsumption, 0, thisSubFactoryNetworkPowerConsumption.Length);

        FactorySystemBeforePower(planet, subFactory, thisSubFactoryNetworkPowerConsumption);
        CargoTrafficBeforePower(planet, subFactory, thisSubFactoryNetworkPowerConsumption);
        // Transport has to be done on a per planet basis due to dispenser logic execution order.
        // Might also be the case stations require it but i have not checked.
        // Same seems to be true for field generators so everything defense will also be handled
        // on a per planet basis.
        // Could not be bothered to change the digital system. It runs planet wide as well.
    }

    public void GameTick(PlanetFactory planet, long time)
    {
        PowerSystem powerSystem = planet.powerSystem;
        FactoryProductionStat factoryProductionStat = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = factoryProductionStat.productRegister;
        int[] consumeRegister = factoryProductionStat.consumeRegister;
        long num = 0L;
        long num2 = 0L;
        long num3 = 0L;
        long num4 = 0L;
        long num5 = 0L;
        PlanetData planetData = powerSystem.factory.planet;
        float windStrength = planetData.windStrength;
        float luminosity = planetData.luminosity;
        UnityEngine.Vector3 normalized = planetData.runtimeLocalSunDirection.normalized;
        if (powerSystem.networkServes == null || powerSystem.networkServes.Length != powerSystem.netPool.Length)
        {
            powerSystem.networkServes = new float[powerSystem.netPool.Length];
        }
        if (powerSystem.networkGenerates == null || powerSystem.networkGenerates.Length != powerSystem.netPool.Length)
        {
            powerSystem.networkGenerates = new float[powerSystem.netPool.Length];
        }
        Array.Clear(powerSystem.currentGeneratorCapacities, 0, powerSystem.currentGeneratorCapacities.Length);
        int num9 = (int)((float)Math.Min(Math.Abs(powerSystem.factory.planet.rotationPeriod), Math.Abs(powerSystem.factory.planet.orbitalPeriod)) * 60f / 2160f);
        if (num9 < 1)
        {
            num9 = 1;
        }
        else if (num9 > 60)
        {
            num9 = 60;
        }
        if (powerSystem.factory.planet.singularity == EPlanetSingularity.TidalLocked)
        {
            num9 = 60;
        }
        bool flag2 = time % num9 == 0L || GameMain.onceGameTick <= 2;
        OptimizedPowerNetwork[] optimizedPowerNetworks = _optimizedPowerNetworks;
        for (int i = 0; i < optimizedPowerNetworks.Length; i++)
        {
            optimizedPowerNetworks[i].GameTick(planet,
                                               time,
                                               productRegister,
                                               consumeRegister,
                                               ref num,
                                               ref num2,
                                               ref num3,
                                               ref num4,
                                               ref num5,
                                               windStrength,
                                               luminosity,
                                               normalized,
                                               flag2,
                                               _subFactoryToNetworkPowerConsumptions.Values);
        }
        lock (factoryProductionStat)
        {
            factoryProductionStat.powerGenRegister = num;
            factoryProductionStat.powerConRegister = num2;
            factoryProductionStat.powerDisRegister = num3;
            factoryProductionStat.powerChaRegister = num4;
            factoryProductionStat.energyConsumption += num5;
        }
    }

    public void Save(PlanetFactory planet)
    {
        OptimizedPowerNetwork[] optimizedPowerNetworks = _optimizedPowerNetworks;
        for (int i = 0; i < optimizedPowerNetworks.Length; i++)
        {
            optimizedPowerNetworks[i].Save(planet);
        }
    }

    private void FactorySystemBeforePower(PlanetFactory planet, OptimizedSubFactory subFactory, long[] subFactoryNetworkPowerConsumption)
    {
        subFactory._beltVeinMinerExecutor.UpdatePower(_beltVeinMinerPowerConsumerTypeIndexes,
                                                     _powerConsumerTypes,
                                                     subFactoryNetworkPowerConsumption);
        subFactory._stationVeinMinerExecutor.UpdatePower(_stationVeinMinerPowerConsumerTypeIndexes,
                                                         _powerConsumerTypes,
                                                         subFactoryNetworkPowerConsumption);
        subFactory._oilMinerExecutor.UpdatePower(_oilMinerPowerConsumerTypeIndexes,
                                                 _powerConsumerTypes,
                                                 subFactoryNetworkPowerConsumption);
        subFactory._waterMinerExecutor.UpdatePower(_waterMinerPowerConsumerTypeIndexes,
                                                   _powerConsumerTypes,
                                                   subFactoryNetworkPowerConsumption);
        subFactory._assemblerExecutor.UpdatePower(_assemblerPowerConsumerTypeIndexes,
                                                  _powerConsumerTypes,
                                                  subFactoryNetworkPowerConsumption);
        subFactory._fractionatorExecutor.UpdatePower(_fractionatorPowerConsumerTypeIndexes,
                                                     _powerConsumerTypes,
                                                     subFactoryNetworkPowerConsumption);
        subFactory._ejectorExecutor.UpdatePower(planet);
        subFactory._siloExecutor.UpdatePower(planet);

        subFactory._producingLabExecutor.UpdatePower(_producingLabPowerConsumerTypeIndexes,
                                                     _powerConsumerTypes,
                                                     subFactoryNetworkPowerConsumption);
        subFactory._researchingLabExecutor.UpdatePower(_researchingLabPowerConsumerTypeIndexes,
                                                       _powerConsumerTypes,
                                                       subFactoryNetworkPowerConsumption);

        subFactory._optimizedBiInserterExecutor.UpdatePower(_inserterBiPowerConsumerTypeIndexes,
                                                           _powerConsumerTypes,
                                                           subFactoryNetworkPowerConsumption);
        subFactory._optimizedInserterExecutor.UpdatePower(_inserterPowerConsumerTypeIndexes,
                                                          _powerConsumerTypes,
                                                          subFactoryNetworkPowerConsumption);
    }

    private void CargoTrafficBeforePower(PlanetFactory planet, OptimizedSubFactory subFactory, long[] subFactoryNetworkPowerConsumption)
    {
        subFactory._monitorExecutor.UpdatePower(_monitorPowerConsumerTypeIndexes,
                                                _powerConsumerTypes,
                                                subFactoryNetworkPowerConsumption);
        subFactory._spraycoaterExecutor.UpdatePower(_subFactoryToSpraycoaterPowerConsumerTypeIndexes[subFactory],
                                                    _powerConsumerTypes,
                                                    subFactoryNetworkPowerConsumption);
        subFactory._pilerExecutor.UpdatePower(_pilerPowerConsumerTypeIndexes,
                                              _powerConsumerTypes,
                                              subFactoryNetworkPowerConsumption);
    }
}
