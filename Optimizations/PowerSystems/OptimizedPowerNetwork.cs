using System.Collections.Generic;
using Weaver.Optimizations.PowerSystems.Generators;

namespace Weaver.Optimizations.PowerSystems;

internal sealed class OptimizedPowerNetwork
{
    private readonly PowerNetwork _powerNetwork;
    private readonly int _networkIndex;
    private readonly int[] _networkNonOptimizedPowerConsumerIndexes;
    private readonly WindGeneratorExecutor _windExecutor;
    private readonly SolarGeneratorExecutor _solarExecutor;
    private readonly GammaPowerGeneratorExecutor _gammaPowerGeneratorExecutor;
    private readonly GeothermalGeneratorExecutor _geothermalGeneratorExecutor;
    private readonly FuelGeneratorExecutor _fuelGeneratorExecutor;
    private readonly PowerExchangerExecutor _powerExchangerExecutor;
    private readonly long _totalPowerNodeEnergyConsumption;

    public OptimizedPowerNetwork(PowerNetwork powerNetwork,
                                 int networkIndex,
                                 int[] networkNonOptimizedPowerConsumerIndexes,
                                 WindGeneratorExecutor windExecutor,
                                 SolarGeneratorExecutor solarGeneratorExecutor,
                                 GammaPowerGeneratorExecutor gammaPowerGeneratorExecutor,
                                 GeothermalGeneratorExecutor geothermalGeneratorExecutor,
                                 FuelGeneratorExecutor fuelGeneratorExecutor,
                                 PowerExchangerExecutor powerExchangerExecutor,
                                 long totalPowerNodeEnergyConsumption)
    {
        _powerNetwork = powerNetwork;
        _networkIndex = networkIndex;
        _networkNonOptimizedPowerConsumerIndexes = networkNonOptimizedPowerConsumerIndexes;
        _windExecutor = windExecutor;
        _solarExecutor = solarGeneratorExecutor;
        _gammaPowerGeneratorExecutor = gammaPowerGeneratorExecutor;
        _geothermalGeneratorExecutor = geothermalGeneratorExecutor;
        _fuelGeneratorExecutor = fuelGeneratorExecutor;
        _powerExchangerExecutor = powerExchangerExecutor;
        _totalPowerNodeEnergyConsumption = totalPowerNodeEnergyConsumption;
    }

    public (long, bool) RequestDysonSpherePower(PowerSystem powerSystem, float eta, float increase, UnityEngine.Vector3 normalized)
    {
        return _gammaPowerGeneratorExecutor.EnergyCap_Gamma_Req(eta, increase, normalized);
    }

    public void GameTick(PlanetFactory planet,
                         long time,
                         int[] productRegister,
                         int[] consumeRegister,
                         ref long num,
                         ref long num2,
                         ref long num3,
                         ref long num4,
                         ref long num5,
                         ref long num7,
                         float windStrength,
                         float luminosity,
                         UnityEngine.Vector3 normalized,
                         bool flag2,
                         Dictionary<OptimizedSubFactory, SubFactoryPowerConsumption>.ValueCollection subFactoryToPowerConsumption,
                         int workerIndex)
    {
        PowerSystem powerSystem = planet.powerSystem;
        PowerNetwork powerNetwork = _powerNetwork;
        int[] consumers = _networkNonOptimizedPowerConsumerIndexes;
        long totalEnergyDemand = 0L;
        if (consumers.Length > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
            for (int j = 0; j < consumers.Length; j++)
            {
                long requiredEnergy = powerSystem.consumerPool[consumers[j]].requiredEnergy;
                totalEnergyDemand += requiredEnergy;
                num2 += requiredEnergy;
            }
            DeepProfiler.EndSample(DPEntry.PowerConsumer, workerIndex);
        }

        totalEnergyDemand += _totalPowerNodeEnergyConsumption;
        num2 += _totalPowerNodeEnergyConsumption;
        foreach (SubFactoryPowerConsumption subFactoryNetworkPowerConsumptionPrepared in subFactoryToPowerConsumption)
        {
            totalEnergyDemand += subFactoryNetworkPowerConsumptionPrepared.NetworksPowerConsumption[_networkIndex];
            num2 += subFactoryNetworkPowerConsumptionPrepared.NetworksPowerConsumption[_networkIndex];
        }

        long num23 = 0L;
        long num24 = 0L;
        (long inputEnergySum, long outputEnergySum) = _powerExchangerExecutor.InputOutputUpdate(powerSystem.currentGeneratorCapacities, workerIndex);
        long num27 = inputEnergySum;
        long num26 = outputEnergySum;

        long totalEnergyProduction = outputEnergySum;
        int generatorCount = _windExecutor.GeneratorCount
                           + _solarExecutor.GeneratorCount
                           + _gammaPowerGeneratorExecutor.GeneratorCount
                           + _geothermalGeneratorExecutor.GeneratorCount
                           + _fuelGeneratorExecutor.GeneratorCount;
        if (generatorCount > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerGenerator, workerIndex);
            long windEnergyCapacity = _windExecutor.EnergyCap(windStrength, powerSystem.currentGeneratorCapacities);
            totalEnergyProduction += windEnergyCapacity;
            long solarEnergyCapacity = _solarExecutor.EnergyCap(luminosity, normalized, flag2, powerSystem.currentGeneratorCapacities);
            totalEnergyProduction += solarEnergyCapacity;
            long gammaEnergyCapacity = _gammaPowerGeneratorExecutor.EnergyCap(planet, powerSystem.currentGeneratorCapacities);
            totalEnergyProduction += gammaEnergyCapacity;
            long geothermalEnergyCapacity = _geothermalGeneratorExecutor.EnergyCap(powerSystem.currentGeneratorCapacities);
            totalEnergyProduction += geothermalEnergyCapacity;
            long fuelEnergyCapacity = _fuelGeneratorExecutor.EnergyCap(powerSystem.currentGeneratorCapacities);
            totalEnergyProduction += fuelEnergyCapacity;
            DeepProfiler.EndSample(DPEntry.PowerGenerator, workerIndex);
        }


        num += totalEnergyProduction - num26;
        long totalEnergyOverProduction = totalEnergyProduction - totalEnergyDemand;
        if (totalEnergyOverProduction > 0 && powerNetwork.exportDemandRatio > 0.0)
        {
            if (powerNetwork.exportDemandRatio > 1.0)
            {
                powerNetwork.exportDemandRatio = 1.0;
            }
            num7 = (long)(totalEnergyOverProduction * powerNetwork.exportDemandRatio + 0.5);
            totalEnergyOverProduction -= num7;
            totalEnergyDemand += num7;
        }
        powerNetwork.exportDemandRatio = 0.0;
        powerNetwork.energyStored = 0L;
        List<int> accumulators = powerNetwork.accumulators;
        int count4 = accumulators.Count;
        long num34 = 0L;
        long num35 = 0L;
        if (count4 > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerAccumulator, workerIndex, count4);
            if (totalEnergyOverProduction >= 0)
            {
                for (int m = 0; m < count4; m++)
                {
                    int num36 = accumulators[m];
                    powerSystem.accPool[num36].curPower = 0L;
                    long num37 = powerSystem.accPool[num36].InputCap();
                    if (num37 > 0)
                    {
                        num37 = num37 < totalEnergyOverProduction ? num37 : totalEnergyOverProduction;
                        powerSystem.accPool[num36].curEnergy += num37;
                        powerSystem.accPool[num36].curPower = num37;
                        totalEnergyOverProduction -= num37;
                        num34 += num37;
                        num4 += num37;
                    }
                    powerNetwork.energyStored += powerSystem.accPool[num36].curEnergy;
                }
            }
            else
            {
                long num38 = -totalEnergyOverProduction;
                for (int n = 0; n < count4; n++)
                {
                    int num36 = accumulators[n];
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
            DeepProfiler.EndSample(DPEntry.PowerAccumulator, workerIndex);
        }
        double num40 = totalEnergyOverProduction < num27 ? totalEnergyOverProduction / (double)num27 : 1.0;
        _powerExchangerExecutor.UpdateInput(productRegister, consumeRegister, num40, ref totalEnergyOverProduction, ref num23, ref num4, workerIndex);

        long num44 = totalEnergyProduction < totalEnergyDemand + num23 ? totalEnergyProduction + num34 + num23 : totalEnergyDemand + num34 + num23;
        double num45 = num44 < num26 ? num44 / (double)num26 : 1.0;
        _powerExchangerExecutor.UpdateOutput(productRegister, consumeRegister, num45, ref num44, ref num24, ref num3, workerIndex);

        powerNetwork.energyCapacity = totalEnergyProduction - num26;
        powerNetwork.energyRequired = totalEnergyDemand - num7;
        powerNetwork.energyExport = num7;
        powerNetwork.energyServed = totalEnergyProduction + num35 < totalEnergyDemand ? totalEnergyProduction + num35 : totalEnergyDemand;
        powerNetwork.energyAccumulated = num34 - num35;
        powerNetwork.energyExchanged = num23 - num24;
        powerNetwork.energyExchangedInputTotal = num23;
        powerNetwork.energyExchangedOutputTotal = num24;
        if (num7 > 0)
        {
            PlanetATField planetATField = powerSystem.factory.planetATField;
            planetATField.energy += num7;
            planetATField.atFieldRechargeCurrent = num7 * 60;
        }
        totalEnergyProduction += num35;
        totalEnergyDemand += num34;
        num5 += totalEnergyProduction >= totalEnergyDemand ? num2 + num7 : totalEnergyProduction;
        long num49 = num24 - totalEnergyDemand > 0 ? num24 - totalEnergyDemand : 0;
        double num50 = totalEnergyProduction >= totalEnergyDemand ? 1.0 : totalEnergyProduction / (double)totalEnergyDemand;
        totalEnergyDemand += num23 - num49;
        totalEnergyProduction -= num24;
        double num51 = totalEnergyProduction > totalEnergyDemand ? totalEnergyDemand / (double)totalEnergyProduction : 1.0;
        powerNetwork.consumerRatio = num50;
        powerNetwork.generaterRatio = num51;
        powerNetwork.energyDischarge = num35 + num24;
        powerNetwork.energyCharge = num34 + num23;
        float num52 = totalEnergyProduction > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num50 : 0f;
        float num53 = totalEnergyProduction > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num51 : 0f;
        powerSystem.networkServes[_networkIndex] = num52;
        powerSystem.networkGenerates[_networkIndex] = num53;

        generatorCount = _gammaPowerGeneratorExecutor.GeneratorCount + _geothermalGeneratorExecutor.GeneratorCount + _fuelGeneratorExecutor.GeneratorCount;
        if (generatorCount > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerGenerator, workerIndex);
            _gammaPowerGeneratorExecutor.GameTick(time, productRegister, consumeRegister);
            _geothermalGeneratorExecutor.GameTick();
            _fuelGeneratorExecutor.GameTick(ref num44, num51, consumeRegister);
            DeepProfiler.EndSample(DPEntry.PowerGenerator, workerIndex);
        }
    }

    public void RefreshPowerGenerationCapacites(ProductionStatistics statistics, PlanetFactory planet)
    {
        int[] powerGenId2Index = ItemProto.powerGenId2Index;

        if (_windExecutor.IsUsed)
        {
            int num = powerGenId2Index[_windExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _windExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _windExecutor.GeneratorCount;
            statistics.totalGenCapacity += _windExecutor.TotalCapacityCurrentTick;
        }

        if (_solarExecutor.IsUsed)
        {
            int num = powerGenId2Index[_solarExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _solarExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _solarExecutor.GeneratorCount;
            statistics.totalGenCapacity += _solarExecutor.TotalCapacityCurrentTick;
        }

        if (_gammaPowerGeneratorExecutor.IsUsed)
        {
            int num = powerGenId2Index[_gammaPowerGeneratorExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _gammaPowerGeneratorExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _gammaPowerGeneratorExecutor.GeneratorCount;
            statistics.totalGenCapacity += _gammaPowerGeneratorExecutor.TotalCapacityCurrentTick;
        }

        if (_geothermalGeneratorExecutor.IsUsed)
        {
            int num = powerGenId2Index[_geothermalGeneratorExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _geothermalGeneratorExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _geothermalGeneratorExecutor.GeneratorCount;
            statistics.totalGenCapacity += _geothermalGeneratorExecutor.TotalCapacityCurrentTick;
        }


        GeneratorIDWithGenerators<OptimizedFuelGenerator>[] fuelGenerators = _fuelGeneratorExecutor.Generators;
        long[] fuelGeneratorsTotalCapacityCurrentTick = _fuelGeneratorExecutor.TotalGeneratorCapacitiesCurrentTick;
        for (int i = 0; i < fuelGenerators.Length; i++)
        {
            GeneratorIDWithGenerators<OptimizedFuelGenerator> fuelGenerator = fuelGenerators[i];

            int num = powerGenId2Index[fuelGenerator.GeneratorID.PrototypeId];
            statistics.genCount[num] += fuelGenerator.OptimizedFuelGenerators.Length;

            long totalCapacityCurrentTick = fuelGeneratorsTotalCapacityCurrentTick[i];
            statistics.genCapacities[num] += totalCapacityCurrentTick;
            statistics.totalGenCapacity += totalCapacityCurrentTick;
        }

        if (_powerExchangerExecutor.IsUsed)
        {
            int num = powerGenId2Index[_powerExchangerExecutor.PrototypeId.Value];

            // Game code takes negative values of total capacity. I inverted the source
            // so it didn't need to be done here.
            statistics.genCapacities[num] += _powerExchangerExecutor.TotalGenerationCapacityCurrentTick;
            statistics.genCount[num] += _powerExchangerExecutor.GeneratorCount;
            statistics.totalGenCapacity += _powerExchangerExecutor.TotalGenerationCapacityCurrentTick;
        }
    }

    public void RefreshPowerConsumptionDemands(ProductionStatistics statistics, PlanetFactory planet)
    {
        EntityData[] entityPool = planet.entityPool;
        PowerSystem powerSystem = planet.powerSystem;
        int[] powerConId2Index = ItemProto.powerConId2Index;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        int[] leftoverConsumers = _networkNonOptimizedPowerConsumerIndexes;
        for (int i = 0; i < leftoverConsumers.Length; i++)
        {
            int consumerIndex = leftoverConsumers[i];
            int num = powerConId2Index[entityPool[consumerPool[consumerIndex].entityId].protoId];
            statistics.conDemands[num] += consumerPool[consumerIndex].requiredEnergy;
            statistics.conCount[num]++;
            statistics.totalConDemand += consumerPool[consumerIndex].requiredEnergy;
        }

        if (_powerExchangerExecutor.IsUsed)
        {
            int num = powerConId2Index[_powerExchangerExecutor.PrototypeId.Value];
            statistics.conDemands[num] += _powerExchangerExecutor.TotalConsumptionCapacityCurrentTick;
            statistics.conCount[num] += _powerExchangerExecutor.GeneratorCount;
            statistics.totalConDemand += _powerExchangerExecutor.TotalConsumptionCapacityCurrentTick;
        }
    }

    public void Save(PlanetFactory planet)
    {
        _windExecutor.Save(planet);
        _solarExecutor.Save(planet);
        _gammaPowerGeneratorExecutor.Save(planet);
        _geothermalGeneratorExecutor.Save(planet);
        _fuelGeneratorExecutor.Save(planet);
        _powerExchangerExecutor.Save(planet);
    }
}
