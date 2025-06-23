using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class SubFactoryPowerConsumption
{
    public PowerConsumerType[] PowerConsumerTypes { get; }
    public int[] AssemblerPowerConsumerTypeIndexes { get; }
    public int[] InserterBiPowerConsumerTypeIndexes { get; }
    public int[] InserterPowerConsumerTypeIndexes { get; }
    public int[] ProducingLabPowerConsumerTypeIndexes { get; }
    public int[] ResearchingLabPowerConsumerTypeIndexes { get; }
    public int[] SpraycoaterPowerConsumerTypeIndexes { get; }
    public int[] FractionatorPowerConsumerTypeIndexes { get; }
    public int[] EjectorPowerConsumerTypeIndexes { get; }
    public int[] SiloPowerConsumerTypeIndexes { get; }
    public int[] PilerPowerConsumerTypeIndexes { get; }
    public int[] MonitorPowerConsumerTypeIndexes { get; }
    public int[] WaterMinerPowerConsumerTypeIndexes { get; }
    public int[] OilMinerPowerConsumerTypeIndexes { get; }
    public int[] BeltVeinMinerPowerConsumerTypeIndexes { get; }
    public int[] StationVeinMinerPowerConsumerTypeIndexes { get; }
    public long[] NetworksPowerConsumption { get; }

    public SubFactoryPowerConsumption(PowerConsumerType[] powerConsumerTypes,
                                      int[] assemblerPowerConsumerTypeIndexes,
                                      int[] inserterBiPowerConsumerTypeIndexes,
                                      int[] inserterPowerConsumerTypeIndexes,
                                      int[] producingLabPowerConsumerTypeIndexes,
                                      int[] researchingLabPowerConsumerTypeIndexes,
                                      int[] spraycoaterPowerConsumerTypeIndexes,
                                      int[] fractionatorPowerConsumerTypeIndexes,
                                      int[] ejectorPowerConsumerTypeIndexes,
                                      int[] siloPowerConsumerTypeIndexes,
                                      int[] pilerPowerConsumerTypeIndexes,
                                      int[] monitorPowerConsumerTypeIndexes,
                                      int[] waterMinerPowerConsumerTypeIndexes,
                                      int[] oilMinerPowerConsumerTypeIndexes,
                                      int[] beltVeinMinerPowerConsumerTypeIndexes,
                                      int[] stationVeinMinerPowerConsumerTypeIndexes,
                                      long[] networksPowerConsumption)
    {
        PowerConsumerTypes = powerConsumerTypes;
        AssemblerPowerConsumerTypeIndexes = assemblerPowerConsumerTypeIndexes;
        InserterBiPowerConsumerTypeIndexes = inserterBiPowerConsumerTypeIndexes;
        InserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
        ProducingLabPowerConsumerTypeIndexes = producingLabPowerConsumerTypeIndexes;
        ResearchingLabPowerConsumerTypeIndexes = researchingLabPowerConsumerTypeIndexes;
        SpraycoaterPowerConsumerTypeIndexes = spraycoaterPowerConsumerTypeIndexes;
        FractionatorPowerConsumerTypeIndexes = fractionatorPowerConsumerTypeIndexes;
        EjectorPowerConsumerTypeIndexes = ejectorPowerConsumerTypeIndexes;
        SiloPowerConsumerTypeIndexes = siloPowerConsumerTypeIndexes;
        PilerPowerConsumerTypeIndexes = pilerPowerConsumerTypeIndexes;
        MonitorPowerConsumerTypeIndexes = monitorPowerConsumerTypeIndexes;
        WaterMinerPowerConsumerTypeIndexes = waterMinerPowerConsumerTypeIndexes;
        OilMinerPowerConsumerTypeIndexes = oilMinerPowerConsumerTypeIndexes;
        BeltVeinMinerPowerConsumerTypeIndexes = beltVeinMinerPowerConsumerTypeIndexes;
        StationVeinMinerPowerConsumerTypeIndexes = stationVeinMinerPowerConsumerTypeIndexes;
        NetworksPowerConsumption = networksPowerConsumption;
    }
}

internal sealed class OptimizedPowerSystem
{
    private readonly OptimizedPowerNetwork[] _optimizedPowerNetworks;
    public readonly Dictionary<OptimizedSubFactory, SubFactoryPowerConsumption> _subFactoryToPowerConsumption;

    public OptimizedPowerSystem(OptimizedPowerNetwork[] optimizedPowerNetworks,
                                Dictionary<OptimizedSubFactory, SubFactoryPowerConsumption> subFactoryToPowerConsumption)
    {
        _optimizedPowerNetworks = optimizedPowerNetworks;
        _subFactoryToPowerConsumption = subFactoryToPowerConsumption;
    }

    public SubFactoryPowerConsumption GetSubFactoryPowerConsumption(OptimizedSubFactory subFactory)
    {
        return _subFactoryToPowerConsumption[subFactory];
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
        if (subFactory.HasCalculatedPowerConsumption)
        {
            return;
        }

        SubFactoryPowerConsumption subFactoryPowerConsumption = _subFactoryToPowerConsumption[subFactory];

        long[] networksPowerConsumption = subFactoryPowerConsumption.NetworksPowerConsumption;
        Array.Clear(networksPowerConsumption, 0, networksPowerConsumption.Length);

        FactorySystemBeforePower(planet, subFactory, subFactoryPowerConsumption, networksPowerConsumption);
        CargoTrafficBeforePower(planet, subFactory, subFactoryPowerConsumption, networksPowerConsumption);
        // Transport has to be done on a per planet basis due to dispenser logic execution order.
        // Might also be the case stations require it but i have not checked.
        // Same seems to be true for field generators so everything defense will also be handled
        // on a per planet basis.
        // Could not be bothered to change the digital system. It runs planet wide as well.
    }

    public void GameTick(PlanetFactory planet, long time)
    {
        foreach (var subFactory in _subFactoryToPowerConsumption.Keys)
        {
            BeforePower(planet, subFactory);
        }

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
                                               _subFactoryToPowerConsumption.Values);
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

    public void RefreshPowerGenerationCapacites(ProductionStatistics statistics, PlanetFactory planet)
    {
        OptimizedPowerNetwork[] optimizedPowerNetworks = _optimizedPowerNetworks;
        for (int i = 0; i < optimizedPowerNetworks.Length; i++)
        {
            optimizedPowerNetworks[i].RefreshPowerGenerationCapacites(statistics, planet);
        }

        EntityData[] entityPool = planet.entityPool;
        PowerSystem powerSystem = planet.powerSystem;
        int[] powerGenId2Index = ItemProto.powerGenId2Index;
        PowerAccumulatorComponent[] accPool = powerSystem.accPool;
        int accCursor = powerSystem.accCursor;
        for (int j = 1; j < accCursor; j++)
        {
            if (accPool[j].id == j && accPool[j].curPower < 0)
            {
                int num2 = powerGenId2Index[entityPool[accPool[j].entityId].protoId];
                statistics.genCapacities[num2] += -accPool[j].curPower;
                statistics.genCount[num2]++;
                statistics.totalGenCapacity += -accPool[j].curPower;
            }
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

    private void FactorySystemBeforePower(PlanetFactory planet,
                                          OptimizedSubFactory subFactory,
                                          SubFactoryPowerConsumption subFactoryPowerConsumption,
                                          long[] networksPowerConsumption)
    {
        subFactory._beltVeinMinerExecutor.UpdatePower(subFactoryPowerConsumption.BeltVeinMinerPowerConsumerTypeIndexes,
                                                      subFactoryPowerConsumption.PowerConsumerTypes,
                                                      networksPowerConsumption);
        subFactory._stationVeinMinerExecutor.UpdatePower(subFactoryPowerConsumption.StationVeinMinerPowerConsumerTypeIndexes,
                                                         subFactoryPowerConsumption.PowerConsumerTypes,
                                                         networksPowerConsumption);
        subFactory._oilMinerExecutor.UpdatePower(subFactoryPowerConsumption.OilMinerPowerConsumerTypeIndexes,
                                                 subFactoryPowerConsumption.PowerConsumerTypes,
                                                 networksPowerConsumption);
        subFactory._waterMinerExecutor.UpdatePower(subFactoryPowerConsumption.WaterMinerPowerConsumerTypeIndexes,
                                                   subFactoryPowerConsumption.PowerConsumerTypes,
                                                   networksPowerConsumption);

        subFactory._assemblerExecutor.UpdatePower(subFactoryPowerConsumption.AssemblerPowerConsumerTypeIndexes,
                                                  subFactoryPowerConsumption.PowerConsumerTypes,
                                                  networksPowerConsumption);

        subFactory._fractionatorExecutor.UpdatePower(subFactoryPowerConsumption.FractionatorPowerConsumerTypeIndexes,
                                                     subFactoryPowerConsumption.PowerConsumerTypes,
                                                     networksPowerConsumption);

        subFactory._ejectorExecutor.UpdatePower(planet,
                                                subFactoryPowerConsumption.EjectorPowerConsumerTypeIndexes,
                                                subFactoryPowerConsumption.PowerConsumerTypes,
                                                networksPowerConsumption);
        subFactory._siloExecutor.UpdatePower(planet,
                                             subFactoryPowerConsumption.SiloPowerConsumerTypeIndexes,
                                             subFactoryPowerConsumption.PowerConsumerTypes,
                                             networksPowerConsumption);

        subFactory._producingLabExecutor.UpdatePower(subFactoryPowerConsumption.ProducingLabPowerConsumerTypeIndexes,
                                                     subFactoryPowerConsumption.PowerConsumerTypes,
                                                     networksPowerConsumption);
        subFactory._researchingLabExecutor.UpdatePower(subFactoryPowerConsumption.ResearchingLabPowerConsumerTypeIndexes,
                                                       subFactoryPowerConsumption.PowerConsumerTypes,
                                                       networksPowerConsumption);

        subFactory._optimizedBiInserterExecutor.UpdatePower(subFactoryPowerConsumption.InserterBiPowerConsumerTypeIndexes,
                                                            subFactoryPowerConsumption.PowerConsumerTypes,
                                                            networksPowerConsumption);
        subFactory._optimizedInserterExecutor.UpdatePower(subFactoryPowerConsumption.InserterPowerConsumerTypeIndexes,
                                                          subFactoryPowerConsumption.PowerConsumerTypes,
                                                          networksPowerConsumption);
    }

    private void CargoTrafficBeforePower(PlanetFactory planet,
                                         OptimizedSubFactory subFactory,
                                         SubFactoryPowerConsumption subFactoryPowerConsumption,
                                         long[] networksPowerConsumption)
    {
        subFactory._monitorExecutor.UpdatePower(subFactoryPowerConsumption.MonitorPowerConsumerTypeIndexes,
                                                subFactoryPowerConsumption.PowerConsumerTypes,
                                                networksPowerConsumption);
        subFactory._spraycoaterExecutor.UpdatePower(subFactoryPowerConsumption.SpraycoaterPowerConsumerTypeIndexes,
                                                    subFactoryPowerConsumption.PowerConsumerTypes,
                                                    networksPowerConsumption);
        subFactory._pilerExecutor.UpdatePower(subFactoryPowerConsumption.PilerPowerConsumerTypeIndexes,
                                              subFactoryPowerConsumption.PowerConsumerTypes,
                                              networksPowerConsumption);
    }
}
