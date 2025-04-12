using System.Collections.Generic;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

internal sealed class FractionatorExecutor
{
    private int[] _fractionatorNetworkId;
    private OptimizedFractionator[] _optimizedFractionators;
    private FractionatorPowerFields[] _fractionatorsPowerFields;
    private FractionatorConfiguration[] _fractionatorConfigurations;
    public Dictionary<int, int> _fractionatorIdToOptimizedIndex;

    public int FractionatorCount => _optimizedFractionators.Length;

    public void GameTick(PlanetFactory planet, long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, _optimizedFractionators.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        CargoTraffic cargoTraffic = planet.cargoTraffic;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] fractionatorNetworkId = _fractionatorNetworkId;
        OptimizedFractionator[] optimizedFractionators = _optimizedFractionators;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        FractionatorConfiguration[] fractionatorConfigurations = _fractionatorConfigurations;

        for (int i = _start; i < _end; i++)
        {
            float power2 = networkServes[fractionatorNetworkId[i]];
            ref OptimizedFractionator fractionator = ref optimizedFractionators[i];
            ref readonly FractionatorConfiguration configuration = ref fractionatorConfigurations[fractionator.configurationIndex];
            ref FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[i];
            fractionator.InternalUpdate(cargoTraffic,
                                        power2,
                                        in configuration,
                                        ref fractionatorPowerFields,
                                        productRegister,
                                        consumeRegister);
        }
    }

    public void UpdatePower(int[] fractionatorPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisThreadNetworkPowerConsumption,
                            int _usedThreadCnt,
                            int _curThreadIdx,
                            int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, fractionatorPowerConsumerTypeIndexes.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        int[] fractionatorNetworkId = _fractionatorNetworkId;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        for (int j = _start; j < _end; j++)
        {
            int networkIndex = fractionatorNetworkId[j];
            int powerConsumerTypeIndex = fractionatorPowerConsumerTypeIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisThreadNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, in fractionatorsPowerFields[j]);
        }
    }

    public void Save(PlanetFactory planet)
    {
        SignData[] entitySignPool = planet.entitySignPool;
        FractionatorComponent[] fractionators = planet.factorySystem.fractionatorPool;
        OptimizedFractionator[] optimizedFractionators = _optimizedFractionators;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        for (int i = 1; i < planet.factorySystem.fractionatorCursor; i++)
        {
            if (!_fractionatorIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedFractionator optimizedFractionator = ref optimizedFractionators[optimizedIndex];
            ref readonly FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[optimizedIndex];
            optimizedFractionator.Save(ref fractionators[i], in fractionatorPowerFields, entitySignPool);
        }
    }

    public void Initialize(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        List<int> fractionatorNetworkId = [];
        List<OptimizedFractionator> optimizedFractionators = [];
        List<FractionatorPowerFields> fractionatorsPowerFields = [];
        Dictionary<FractionatorConfiguration, int> fractionatorConfigurationToIndex = [];
        List<FractionatorConfiguration> fractionatorConfigurations = [];
        Dictionary<int, int> fractionatorIdToOptimizedIndex = [];

        for (int i = 0; i < planet.factorySystem.fractionatorCursor; i++)
        {
            ref FractionatorComponent fractionator = ref planet.factorySystem.fractionatorPool[i];
            if (fractionator.id != i)
            {
                continue;
            }

            FractionatorConfiguration configuration = new FractionatorConfiguration(fractionator.isOutput0,
                                                                                    fractionator.isOutput1,
                                                                                    fractionator.isOutput2,
                                                                                    fractionator.fluidInputMax,
                                                                                    fractionator.fluidOutputMax,
                                                                                    fractionator.productOutputMax);
            if (!fractionatorConfigurationToIndex.TryGetValue(configuration, out int fractionatorConfigurationIndex))
            {
                fractionatorConfigurationIndex = fractionatorConfigurationToIndex.Count;
                fractionatorConfigurationToIndex.Add(configuration, fractionatorConfigurationIndex);
                fractionatorConfigurations.Add(configuration);
            }

            CargoPath? belt0 = null;
            CargoPath? belt1 = null;
            CargoPath? belt2 = null;

            if (fractionator.belt0 > 0)
            {
                belt0 = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[fractionator.belt0].segPathId];
            }
            if (fractionator.belt1 > 0)
            {
                belt1 = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[fractionator.belt1].segPathId);
            }
            if (fractionator.belt2 > 0)
            {
                belt2 = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[fractionator.belt2].segPathId);
            }

            fractionatorIdToOptimizedIndex.Add(fractionator.id, optimizedFractionators.Count);
            int networkIndex = planet.powerSystem.consumerPool[fractionator.pcId].networkId;
            fractionatorNetworkId.Add(networkIndex);
            optimizedFractionators.Add(new OptimizedFractionator(belt0,
                                                                 belt1,
                                                                 belt2,
                                                                 fractionatorConfigurationIndex,
                                                                 in fractionator));
            fractionatorsPowerFields.Add(new FractionatorPowerFields(in fractionator));
            optimizedPowerSystemBuilder.AddFractionator(in fractionator, networkIndex);

        }

        _fractionatorNetworkId = fractionatorNetworkId.ToArray();
        _optimizedFractionators = optimizedFractionators.ToArray();
        _fractionatorsPowerFields = fractionatorsPowerFields.ToArray();
        _fractionatorConfigurations = fractionatorConfigurations.ToArray();
        _fractionatorIdToOptimizedIndex = fractionatorIdToOptimizedIndex;
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly FractionatorPowerFields fractionatorPowerFields)
    {
        double num = (((double)fractionatorPowerFields.fluidInputCargoCount > 0.0001) ? ((float)fractionatorPowerFields.fluidInputCount / fractionatorPowerFields.fluidInputCargoCount) : 4f);
        double num2 = (double)((fractionatorPowerFields.fluidInputCargoCount < 30f) ? fractionatorPowerFields.fluidInputCargoCount : 30f) * num - 30.0;
        if (num2 < 0.0)
        {
            num2 = 0.0;
        }
        int permillage = (int)((num2 * 50.0 + 1000.0) * Cargo.powerTableRatio[fractionatorPowerFields.incLevel] + 0.5);
        return powerConsumerType.GetRequiredEnergy(fractionatorPowerFields.isWorking, permillage);
    }
}
