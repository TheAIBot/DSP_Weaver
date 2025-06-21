using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

internal sealed class FractionatorExecutor
{
    private int[] _fractionatorNetworkId = null!;
    private OptimizedFractionator[] _optimizedFractionators = null!;
    private FractionatorPowerFields[] _fractionatorsPowerFields = null!;
    private FractionatorConfiguration[] _fractionatorConfigurations = null!;
    public Dictionary<int, int> _fractionatorIdToOptimizedIndex = null!;

    public int FractionatorCount => _optimizedFractionators.Length;

    public void GameTick(PlanetFactory planet,
                         int[] fractionatorPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] fractionatorNetworkId = _fractionatorNetworkId;
        OptimizedFractionator[] optimizedFractionators = _optimizedFractionators;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        FractionatorConfiguration[] fractionatorConfigurations = _fractionatorConfigurations;

        for (int fractionatorIndex = 0; fractionatorIndex < optimizedFractionators.Length; fractionatorIndex++)
        {
            int networkIndex = fractionatorNetworkId[fractionatorIndex];
            float power2 = networkServes[networkIndex];
            ref OptimizedFractionator fractionator = ref optimizedFractionators[fractionatorIndex];
            ref readonly FractionatorConfiguration configuration = ref fractionatorConfigurations[fractionator.configurationIndex];
            ref FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[fractionatorIndex];
            fractionator.InternalUpdate(power2,
                                        in configuration,
                                        ref fractionatorPowerFields,
                                        productRegister,
                                        consumeRegister);

            UpdatePower(fractionatorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, fractionatorIndex, networkIndex, in fractionatorPowerFields);
        }
    }

    public void UpdatePower(int[] fractionatorPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] fractionatorNetworkId = _fractionatorNetworkId;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        for (int fractionatorIndex = 0; fractionatorIndex < fractionatorsPowerFields.Length; fractionatorIndex++)
        {
            int networkIndex = fractionatorNetworkId[fractionatorIndex];
            ref readonly FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[fractionatorIndex];

            UpdatePower(fractionatorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, fractionatorIndex, networkIndex, in fractionatorPowerFields);
        }
    }

    private static void UpdatePower(int[] fractionatorPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int fractionatorIndex,
                                    int networkIndex,
                                    ref readonly FractionatorPowerFields fractionatorPowerFields)
    {
        int powerConsumerTypeIndex = fractionatorPowerConsumerTypeIndexes[fractionatorIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, in fractionatorPowerFields);
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

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> fractionatorNetworkId = [];
        List<OptimizedFractionator> optimizedFractionators = [];
        List<FractionatorPowerFields> fractionatorsPowerFields = [];
        Dictionary<FractionatorConfiguration, int> fractionatorConfigurationToIndex = [];
        List<FractionatorConfiguration> fractionatorConfigurations = [];
        Dictionary<int, int> fractionatorIdToOptimizedIndex = [];

        foreach (int fractionatorIndex in subFactoryGraph.GetAllNodes()
                                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Fractionator)
                                                         .Select(x => x.EntityTypeIndex.Index)
                                                         .OrderBy(x => x))
        {
            ref FractionatorComponent fractionator = ref planet.factorySystem.fractionatorPool[fractionatorIndex];
            if (fractionator.id != fractionatorIndex)
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

            OptimizedCargoPath? belt0 = null;
            OptimizedCargoPath? belt1 = null;
            OptimizedCargoPath? belt2 = null;

            if (fractionator.belt0 > 0)
            {
                CargoPath cargoPath0 = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[fractionator.belt0].segPathId];
                belt0 = cargoPath0 != null ? beltExecutor.GetOptimizedCargoPath(cargoPath0) : null;
            }
            if (fractionator.belt1 > 0)
            {
                CargoPath cargoPat1 = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[fractionator.belt1].segPathId);
                belt1 = cargoPat1 != null ? beltExecutor.GetOptimizedCargoPath(cargoPat1) : null;
            }
            if (fractionator.belt2 > 0)
            {
                CargoPath cargoPat2 = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[fractionator.belt2].segPathId);
                belt2 = cargoPat2 != null ? beltExecutor.GetOptimizedCargoPath(cargoPat2) : null;
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

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly FractionatorPowerFields fractionatorPowerFields)
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
