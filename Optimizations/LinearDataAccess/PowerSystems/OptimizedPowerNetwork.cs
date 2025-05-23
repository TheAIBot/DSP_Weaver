using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class OptimizedPowerNetwork
{
    private readonly PowerNetwork _powerNetwork;
    private readonly int _networkIndex;
    private readonly int[] _networkNonOptimizedPowerConsumerIndexes;
    private readonly int[] _generatorIndexes;
    private readonly GammaPowerGeneratorExecutor _gammaPowerGeneratorExecutor;
    private readonly PowerExchangerExecutor _powerExchangerExecutor;

    public OptimizedPowerNetwork(PowerNetwork powerNetwork,
                                 int networkIndex,
                                 int[] networkNonOptimizedPowerConsumerIndexes,
                                 int[] generatorIndexes,
                                 GammaPowerGeneratorExecutor gammaPowerGeneratorExecutor,
                                 PowerExchangerExecutor powerExchangerExecutor)
    {
        _powerNetwork = powerNetwork;
        _networkIndex = networkIndex;
        _networkNonOptimizedPowerConsumerIndexes = networkNonOptimizedPowerConsumerIndexes;
        _generatorIndexes = generatorIndexes;
        _gammaPowerGeneratorExecutor = gammaPowerGeneratorExecutor;
        _powerExchangerExecutor = powerExchangerExecutor;
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
                         float windStrength,
                         float luminosity,
                         UnityEngine.Vector3 normalized,
                         bool flag2,
                         Dictionary<OptimizedSubFactory, long[]>.ValueCollection subFactoryToNetworkPowerConsumptions)
    {
        PowerSystem powerSystem = planet.powerSystem;
        PowerNetwork powerNetwork = _powerNetwork;
        int[] consumers = _networkNonOptimizedPowerConsumerIndexes;
        long num11 = 0L;
        for (int j = 0; j < consumers.Length; j++)
        {
            long requiredEnergy = powerSystem.consumerPool[consumers[j]].requiredEnergy;
            num11 += requiredEnergy;
            num2 += requiredEnergy;
        }
        foreach (long[] subFactoryNetworkPowerConsumptionPrepared in subFactoryToNetworkPowerConsumptions)
        {
            num11 += subFactoryNetworkPowerConsumptionPrepared[_networkIndex];
            num2 += subFactoryNetworkPowerConsumptionPrepared[_networkIndex];
        }

        long num23 = 0L;
        long num24 = 0L;
        (long inputEnergySum, long outputEnergySum) = _powerExchangerExecutor.InputOutputUpdate(powerSystem.currentGeneratorCapacities);
        long num27 = inputEnergySum;
        long num26 = outputEnergySum;
        long num22 = outputEnergySum;

        int[] generatorIndexes = _generatorIndexes;
        for (int i = 0; i < generatorIndexes.Length; i++)
        {
            long num31;
            int num30 = generatorIndexes[i];
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
                throw new InvalidOperationException("Gamma power generator EnergyCap should not be handled by the existing logic anymore.");
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
        long gammaEnergyCapacity = _gammaPowerGeneratorExecutor.EnergyCap(planet, powerSystem.currentGeneratorCapacities);
        num22 += gammaEnergyCapacity;
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
        if (num32 >= 0)
        {
            for (int m = 0; m < count4; m++)
            {
                int num36 = accumulators[m];
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
        double num40 = num32 < num27 ? num32 / (double)num27 : 1.0;
        _powerExchangerExecutor.UpdateInput(productRegister, consumeRegister, num40, ref num32, ref num23, ref num4);

        long num44 = num22 < num11 + num23 ? num22 + num34 + num23 : num11 + num34 + num23;
        double num45 = num44 < num26 ? num44 / (double)num26 : 1.0;
        _powerExchangerExecutor.UpdateOutput(productRegister, consumeRegister, num45, ref num44, ref num24, ref num3);

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
        powerSystem.networkServes[_networkIndex] = num52;
        powerSystem.networkGenerates[_networkIndex] = num53;
        for (int i = 0; i < generatorIndexes.Length; i++)
        {
            int num30 = generatorIndexes[i];
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
            if (powerSystem.genPool[num30].wind)
            {
            }
            else if (powerSystem.genPool[num30].gamma)
            {
                throw new InvalidOperationException("Gamma power generator GameTick should not be handled by the existing logic anymore.");
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
        _gammaPowerGeneratorExecutor.GameTick(planet, time, productRegister, consumeRegister);
    }

    public void Save(PlanetFactory planet)
    {
        _gammaPowerGeneratorExecutor.Save(planet);
        _powerExchangerExecutor.Save(planet);
    }
}
