using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerSystem
{
    private readonly PowerConsumerType[] _powerConsumerTypes;
    private readonly int[][] _networkNonOptimizedPowerConsumerIndexes;
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
    private readonly Dictionary<OptimizedSubFactory, long[]> _subFactoryToNetworkPowerConsumptions;

    public OptimizedPowerSystem(PowerConsumerType[] powerConsumerTypes,
                                int[][] networkNonOptimizedPowerConsumerIndexes,
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
                                Dictionary<OptimizedSubFactory, long[]> subFactoryToNetworkPowerConsumptions)
    {
        _powerConsumerTypes = powerConsumerTypes;
        _networkNonOptimizedPowerConsumerIndexes = networkNonOptimizedPowerConsumerIndexes;
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
        _subFactoryToNetworkPowerConsumptions = subFactoryToNetworkPowerConsumptions;
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
        AnimData[] entityAnimPool = powerSystem.factory.entityAnimPool;
        if (powerSystem.networkServes == null || powerSystem.networkServes.Length != powerSystem.netPool.Length)
        {
            powerSystem.networkServes = new float[powerSystem.netPool.Length];
        }
        if (powerSystem.networkGenerates == null || powerSystem.networkGenerates.Length != powerSystem.netPool.Length)
        {
            powerSystem.networkGenerates = new float[powerSystem.netPool.Length];
        }
        bool useIonLayer = GameMain.history.useIonLayer;
        bool useCata = time % 10 == 0;
        Array.Clear(powerSystem.currentGeneratorCapacities, 0, powerSystem.currentGeneratorCapacities.Length);
        float response = powerSystem.dysonSphere != null ? powerSystem.dysonSphere.energyRespCoef : 0f;
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
        int num10 = (int)(time % 90);
        EntityData[] entityPool = powerSystem.factory.entityPool;
        for (int i = 1; i < powerSystem.netCursor; i++)
        {
            PowerNetwork powerNetwork = powerSystem.netPool[i];
            if (powerNetwork == null || powerNetwork.id != i)
            {
                continue;
            }
            int[] consumers = _networkNonOptimizedPowerConsumerIndexes[i];
            long num11 = 0L;
            for (int j = 0; j < consumers.Length; j++)
            {
                long requiredEnergy = powerSystem.consumerPool[consumers[j]].requiredEnergy;
                num11 += requiredEnergy;
                num2 += requiredEnergy;
            }
            foreach (long[] subFactoryNetworkPowerConsumptionPrepared in _subFactoryToNetworkPowerConsumptions.Values)
            {
                num11 += subFactoryNetworkPowerConsumptionPrepared[i];
                num2 += subFactoryNetworkPowerConsumptionPrepared[i];
            }
            long num22 = 0L;
            List<int> exchangers = powerNetwork.exchangers;
            int count2 = exchangers.Count;
            long num23 = 0L;
            long num24 = 0L;
            int num25 = 0;
            long num26 = 0L;
            long num27 = 0L;
            bool flag3 = false;
            bool flag4 = false;
            for (int k = 0; k < count2; k++)
            {
                num25 = exchangers[k];
                powerSystem.excPool[num25].StateUpdate();
                powerSystem.excPool[num25].BeltUpdate(powerSystem.factory);
                flag3 = powerSystem.excPool[num25].state >= 1f;
                flag4 = powerSystem.excPool[num25].state <= -1f;
                if (!flag3 && !flag4)
                {
                    powerSystem.excPool[num25].capsCurrentTick = 0L;
                    powerSystem.excPool[num25].currEnergyPerTick = 0L;
                }
                if (flag4)
                {
                    long num29 = powerSystem.excPool[num25].OutputCaps();
                    num26 += num29;
                    num22 = num26;
                    powerSystem.currentGeneratorCapacities[powerSystem.excPool[num25].subId] += num29;
                }
                else if (flag3)
                {
                    num27 += powerSystem.excPool[num25].InputCaps();
                }
            }
            List<int> generators = powerNetwork.generators;
            int count3 = generators.Count;
            int num30 = 0;
            long num31 = 0L;
            for (int l = 0; l < count3; l++)
            {
                num30 = generators[l];
                if (powerSystem.genPool[num30].wind)
                {
                    num31 = powerSystem.genPool[num30].EnergyCap_Wind(windStrength);
                    num22 += num31;
                }
                else if (powerSystem.genPool[num30].photovoltaic)
                {
                    if (flag2)
                    {
                        num31 = powerSystem.genPool[num30].EnergyCap_PV(normalized.x, normalized.y, normalized.z, luminosity);
                        num22 += num31;
                    }
                    else
                    {
                        num31 = powerSystem.genPool[num30].capacityCurrentTick;
                        num22 += num31;
                    }
                }
                else if (powerSystem.genPool[num30].gamma)
                {
                    num31 = powerSystem.genPool[num30].EnergyCap_Gamma(response);
                    num22 += num31;
                }
                else if (powerSystem.genPool[num30].geothermal)
                {
                    num31 = powerSystem.genPool[num30].EnergyCap_GTH();
                    num22 += num31;
                }
                else
                {
                    num31 = powerSystem.genPool[num30].EnergyCap_Fuel();
                    num22 += num31;
                }
                powerSystem.currentGeneratorCapacities[powerSystem.genPool[num30].subId] += num31;
            }
            num += num22 - num26;
            long num32 = num22 - num11;
            long num33 = 0L;
            if (num32 > 0 && powerNetwork.exportDemandRatio > 0.0)
            {
                if (powerNetwork.exportDemandRatio > 1.0)
                {
                    powerNetwork.exportDemandRatio = 1.0;
                }
                num33 = (long)(num32 * powerNetwork.exportDemandRatio + 0.5);
                num32 -= num33;
                num11 += num33;
            }
            powerNetwork.exportDemandRatio = 0.0;
            powerNetwork.energyStored = 0L;
            List<int> accumulators = powerNetwork.accumulators;
            int count4 = accumulators.Count;
            long num34 = 0L;
            long num35 = 0L;
            int num36 = 0;
            if (num32 >= 0)
            {
                for (int m = 0; m < count4; m++)
                {
                    num36 = accumulators[m];
                    powerSystem.accPool[num36].curPower = 0L;
                    long num37 = powerSystem.accPool[num36].InputCap();
                    if (num37 > 0)
                    {
                        num37 = num37 < num32 ? num37 : num32;
                        powerSystem.accPool[num36].curEnergy += num37;
                        powerSystem.accPool[num36].curPower = num37;
                        num32 -= num37;
                        num34 += num37;
                        num4 += num37;
                    }
                    powerNetwork.energyStored += powerSystem.accPool[num36].curEnergy;
                }
            }
            else
            {
                long num38 = -num32;
                for (int n = 0; n < count4; n++)
                {
                    num36 = accumulators[n];
                    powerSystem.accPool[num36].curPower = 0L;
                    long num39 = powerSystem.accPool[num36].OutputCap();
                    if (num39 > 0)
                    {
                        num39 = num39 < num38 ? num39 : num38;
                        powerSystem.accPool[num36].curEnergy -= num39;
                        powerSystem.accPool[num36].curPower = -num39;
                        num38 -= num39;
                        num35 += num39;
                        num3 += num39;
                    }
                    powerNetwork.energyStored += powerSystem.accPool[num36].curEnergy;
                }
            }
            double num40 = num32 < num27 ? num32 / (double)num27 : 1.0;
            for (int num41 = 0; num41 < count2; num41++)
            {
                num25 = exchangers[num41];
                if (powerSystem.excPool[num25].state >= 1f && num40 >= 0.0)
                {
                    long num42 = (long)(num40 * powerSystem.excPool[num25].capsCurrentTick + 0.99999);
                    long remaining = num32 < num42 ? num32 : num42;
                    long num43 = powerSystem.excPool[num25].InputUpdate(remaining, entityAnimPool, productRegister, consumeRegister);
                    num32 -= num43;
                    num23 += num43;
                    num4 += num43;
                }
                else
                {
                    powerSystem.excPool[num25].currEnergyPerTick = 0L;
                }
            }
            long num44 = num22 < num11 + num23 ? num22 + num34 + num23 : num11 + num34 + num23;
            double num45 = num44 < num26 ? num44 / (double)num26 : 1.0;
            for (int num46 = 0; num46 < count2; num46++)
            {
                num25 = exchangers[num46];
                if (powerSystem.excPool[num25].state <= -1f)
                {
                    long num47 = (long)(num45 * powerSystem.excPool[num25].capsCurrentTick + 0.99999);
                    long energyPay = num44 < num47 ? num44 : num47;
                    long num48 = powerSystem.excPool[num25].OutputUpdate(energyPay, entityAnimPool, productRegister, consumeRegister);
                    num24 += num48;
                    num3 += num48;
                    num44 -= num48;
                }
            }
            powerNetwork.energyCapacity = num22 - num26;
            powerNetwork.energyRequired = num11 - num33;
            powerNetwork.energyExport = num33;
            powerNetwork.energyServed = num22 + num35 < num11 ? num22 + num35 : num11;
            powerNetwork.energyAccumulated = num34 - num35;
            powerNetwork.energyExchanged = num23 - num24;
            powerNetwork.energyExchangedInputTotal = num23;
            powerNetwork.energyExchangedOutputTotal = num24;
            if (num33 > 0)
            {
                PlanetATField planetATField = powerSystem.factory.planetATField;
                planetATField.energy += num33;
                planetATField.atFieldRechargeCurrent = num33 * 60;
            }
            num22 += num35;
            num11 += num34;
            num5 += num22 >= num11 ? num2 + num33 : num22;
            long num49 = num24 - num11 > 0 ? num24 - num11 : 0;
            double num50 = num22 >= num11 ? 1.0 : num22 / (double)num11;
            num11 += num23 - num49;
            num22 -= num24;
            double num51 = num22 > num11 ? num11 / (double)num22 : 1.0;
            powerNetwork.consumerRatio = num50;
            powerNetwork.generaterRatio = num51;
            powerNetwork.energyDischarge = num35 + num24;
            powerNetwork.energyCharge = num34 + num23;
            float num52 = num22 > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num50 : 0f;
            float num53 = num22 > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num51 : 0f;
            powerSystem.networkServes[i] = num52;
            powerSystem.networkGenerates[i] = num53;
            for (int num55 = 0; num55 < count3; num55++)
            {
                num30 = generators[num55];
                long num56 = 0L;
                bool flag5 = !powerSystem.genPool[num30].wind && !powerSystem.genPool[num30].photovoltaic && !powerSystem.genPool[num30].gamma && !powerSystem.genPool[num30].geothermal;
                if (flag5)
                {
                    powerSystem.genPool[num30].currentStrength = num44 > 0 && powerSystem.genPool[num30].capacityCurrentTick > 0 ? 1 : 0;
                }
                if (num44 > 0 && powerSystem.genPool[num30].productId == 0)
                {
                    long num57 = (long)(num51 * powerSystem.genPool[num30].capacityCurrentTick + 0.99999);
                    num56 = num44 < num57 ? num44 : num57;
                    if (num56 > 0)
                    {
                        num44 -= num56;
                        if (flag5)
                        {
                            powerSystem.genPool[num30].GenEnergyByFuel(num56, consumeRegister);
                        }
                    }
                }
                powerSystem.genPool[num30].generateCurrentTick = num56;
                int entityId4 = powerSystem.genPool[num30].entityId;
                if (powerSystem.genPool[num30].wind)
                {
                }
                else if (powerSystem.genPool[num30].gamma)
                {
                    bool keyFrame = (num30 + num10) % 90 == 0;
                    powerSystem.genPool[num30].GameTick_Gamma(useIonLayer, useCata, keyFrame, powerSystem.factory, productRegister, consumeRegister);
                }
                else if (powerSystem.genPool[num30].fuelMask > 1)
                {
                }
                else if (powerSystem.genPool[num30].geothermal)
                {
                    float num58 = powerSystem.genPool[num30].warmup + powerSystem.genPool[num30].warmupSpeed;
                    powerSystem.genPool[num30].warmup = num58 > 1f ? 1f : num58 < 0f ? 0f : num58;
                }
            }
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
