using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

internal sealed class FractionatorExecutor
{
    private int[] _fractionatorNetworkId = null!;
    private OptimizedFractionator[] _optimizedFractionators = null!;
    private FractionatorPowerFields[] _fractionatorsPowerFields = null!;
    private FractionatorConfiguration[] _fractionatorConfigurations = null!;
    private FractionatorRecipeProduct[]? _fractionatorRecipeProducts = null!;
    public Dictionary<int, int> _fractionatorIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int FractionatorCount => _optimizedFractionators.Length;

    public void GameTick(PlanetFactory planet,
                         int[] fractionatorPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] productRegister,
                         int[] consumeRegister)
    {
        if (_fractionatorRecipeProducts == null)
        {
            return;
        }

        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] fractionatorNetworkId = _fractionatorNetworkId;
        OptimizedFractionator[] optimizedFractionators = _optimizedFractionators;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        FractionatorConfiguration[] fractionatorConfigurations = _fractionatorConfigurations;
        FractionatorRecipeProduct[] fractionatorRecipeProducts = _fractionatorRecipeProducts;

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
                                        fractionatorRecipeProducts,
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

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] fractionatorPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int fractionatorIndex = 0; fractionatorIndex < fractionatorsPowerFields.Length; fractionatorIndex++)
        {
            ref readonly FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[fractionatorIndex];
            UpdatePowerConsumptionPerPrototype(fractionatorPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               fractionatorIndex,
                                               in fractionatorPowerFields);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] fractionatorPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int fractionatorIndex,
                                                           ref readonly FractionatorPowerFields fractionatorPowerFields)
    {
        int powerConsumerTypeIndex = fractionatorPowerConsumerTypeIndexes[fractionatorIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[fractionatorIndex]] += GetPowerConsumption(powerConsumerType, in fractionatorPowerFields);
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
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> fractionatorNetworkId = [];
        List<OptimizedFractionator> optimizedFractionators = [];
        List<FractionatorPowerFields> fractionatorsPowerFields = [];
        Dictionary<FractionatorConfiguration, int> fractionatorConfigurationToIndex = [];
        List<FractionatorConfiguration> fractionatorConfigurations = [];
        Dictionary<int, int> fractionatorIdToOptimizedIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

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

            beltExecutor.TryOptimizedCargoPath(planet, fractionator.belt0, out OptimizedCargoPath? belt0);
            beltExecutor.TryOptimizedCargoPath(planet, fractionator.belt1, out OptimizedCargoPath? belt1);
            beltExecutor.TryOptimizedCargoPath(planet, fractionator.belt2, out OptimizedCargoPath? belt2);

            OptimizedItemId fluidId = default;
            if (fractionator.fluidId > 0)
            {
                fluidId = subFactoryProductionRegisterBuilder.AddProduct(fractionator.fluidId);
            }

            OptimizedItemId productId = default;
            if (fractionator.productId > 0)
            {
                productId = subFactoryProductionRegisterBuilder.AddProduct(fractionator.productId);
            }

            fractionatorIdToOptimizedIndex.Add(fractionator.id, optimizedFractionators.Count);
            int networkIndex = planet.powerSystem.consumerPool[fractionator.pcId].networkId;
            fractionatorNetworkId.Add(networkIndex);
            optimizedFractionators.Add(new OptimizedFractionator(belt0,
                                                                 belt1,
                                                                 belt2,
                                                                 fractionatorConfigurationIndex,
                                                                 fluidId,
                                                                 productId,
                                                                 in fractionator));
            fractionatorsPowerFields.Add(new FractionatorPowerFields(in fractionator));
            subFactoryPowerSystemBuilder.AddFractionator(in fractionator, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[fractionator.entityId]);
        }

        _fractionatorRecipeProducts = null;
        if (optimizedFractionators.Count > 0)
        {
            RecipeProto[] fractionatorRecipes = RecipeProto.fractionatorRecipes;
            var fractionatorRecipeProducts = new FractionatorRecipeProduct[fractionatorRecipes.Length];
            for (int i = 0; i < fractionatorRecipeProducts.Length; i++)
            {
                fractionatorRecipeProducts[i] = new FractionatorRecipeProduct(fractionatorRecipes[i].Items[0],
                                                                              subFactoryProductionRegisterBuilder.AddConsume(fractionatorRecipes[i].Items[0]),
                                                                              subFactoryProductionRegisterBuilder.AddProduct(fractionatorRecipes[i].Results[0]),
                                                                              fractionatorRecipes[i].ResultCounts[0] / (float)fractionatorRecipes[i].ItemCounts[0]);

            }

            _fractionatorRecipeProducts = fractionatorRecipeProducts;
        }

        _fractionatorNetworkId = fractionatorNetworkId.ToArray();
        _optimizedFractionators = optimizedFractionators.ToArray();
        _fractionatorsPowerFields = fractionatorsPowerFields.ToArray();
        _fractionatorConfigurations = fractionatorConfigurations.ToArray();
        _fractionatorIdToOptimizedIndex = fractionatorIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
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
