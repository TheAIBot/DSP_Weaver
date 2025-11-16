using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Fractionators;

internal sealed class FractionatorExecutor
{
    private int[]? _fractionatorNetworkId = null;
    private OptimizedFractionator[] _optimizedFractionators = null!;
    private FractionatorPowerFields[] _fractionatorsPowerFields = null!;
    private FractionatorRecipeProduct[] _fractionatorRecipeProducts = null!;
    public Dictionary<int, int> _fractionatorIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int Count => _optimizedFractionators.Length;

    public void GameTick(PlanetFactory planet,
                         short[] fractionatorPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] productRegister,
                         int[] consumeRegister,
                         OptimizedCargoPath[] optimizedCargoPaths,
                         UniverseStaticData universeStaticData)
    {
        if (_fractionatorNetworkId == null)
        {
            return;
        }

        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] fractionatorNetworkId = _fractionatorNetworkId;
        OptimizedFractionator[] optimizedFractionators = _optimizedFractionators;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        FractionatorConfiguration[] fractionatorConfigurations = universeStaticData.FractionatorConfigurations;
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
                                        consumeRegister,
                                        optimizedCargoPaths);

            UpdatePower(fractionatorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, fractionatorIndex, networkIndex, in fractionatorPowerFields);
        }
    }

    public void UpdatePower(short[] fractionatorPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        if (_fractionatorNetworkId == null)
        {
            return;
        }

        int[] fractionatorNetworkId = _fractionatorNetworkId;
        FractionatorPowerFields[] fractionatorsPowerFields = _fractionatorsPowerFields;
        for (int fractionatorIndex = 0; fractionatorIndex < fractionatorsPowerFields.Length; fractionatorIndex++)
        {
            int networkIndex = fractionatorNetworkId[fractionatorIndex];
            ref readonly FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[fractionatorIndex];

            UpdatePower(fractionatorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, fractionatorIndex, networkIndex, in fractionatorPowerFields);
        }
    }

    private static void UpdatePower(short[] fractionatorPowerConsumerTypeIndexes,
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

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(short[] fractionatorPowerConsumerTypeIndexes,
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

    private static void UpdatePowerConsumptionPerPrototype(short[] fractionatorPowerConsumerTypeIndexes,
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

            ref readonly FractionatorPowerFields fractionatorPowerFields = ref fractionatorsPowerFields[optimizedIndex];
            optimizedFractionators[optimizedIndex].Save(ref fractionators[i], in fractionatorPowerFields, entitySignPool);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           BeltExecutor beltExecutor,
                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        List<int> fractionatorNetworkId = [];
        List<OptimizedFractionator> optimizedFractionators = [];
        List<FractionatorPowerFields> fractionatorsPowerFields = [];
        Dictionary<int, int> fractionatorIdToOptimizedIndex = [];
        HashSet<FractionatorRecipeProduct> fractionatorRecipeProducts = [];
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

            var configuration = new FractionatorConfiguration(fractionator.isOutput0,
                                                              fractionator.isOutput1,
                                                              fractionator.isOutput2,
                                                              fractionator.fluidInputMax,
                                                              fractionator.fluidOutputMax,
                                                              fractionator.productOutputMax);
            int fractionatorConfigurationIndex = universeStaticDataBuilder.AddFractionatorConfiguration(in configuration);

            beltExecutor.TryGetOptimizedCargoPathIndex(planet, fractionator.belt0, out BeltIndex belt0Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, fractionator.belt1, out BeltIndex belt1Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, fractionator.belt2, out BeltIndex belt2Index);

            OptimizedItemId fluidId = default;
            if (fractionator.fluidId > 0)
            {
                fluidId = subFactoryProductionRegisterBuilder.AddConsume(fractionator.fluidId);
            }

            OptimizedItemId productId = default;
            if (fractionator.productId > 0)
            {
                productId = subFactoryProductionRegisterBuilder.AddProduct(fractionator.productId);
            }

            fractionatorIdToOptimizedIndex.Add(fractionator.id, optimizedFractionators.Count);
            int networkIndex = planet.powerSystem.consumerPool[fractionator.pcId].networkId;
            fractionatorNetworkId.Add(networkIndex);
            optimizedFractionators.Add(new OptimizedFractionator(belt0Index,
                                                                 belt1Index,
                                                                 belt2Index,
                                                                 fractionatorConfigurationIndex,
                                                                 fluidId,
                                                                 productId,
                                                                 in fractionator));
            fractionatorsPowerFields.Add(new FractionatorPowerFields(in fractionator));
            subFactoryPowerSystemBuilder.AddFractionator(in fractionator, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[fractionator.entityId]);
        }

        _fractionatorNetworkId = null;
        if (optimizedFractionators.Count > 0)
        {
            RecipeProto[] fractionatorRecipes = RecipeProto.fractionatorRecipes;
            for (int i = 0; i < fractionatorRecipes.Length; i++)
            {
                RecipeProto fractionatorRecipe = fractionatorRecipes[i];
                var fractionatorRecipeProduct = new FractionatorRecipeProduct(fractionatorRecipe.Items[0],
                                                                              subFactoryProductionRegisterBuilder.AddConsume(fractionatorRecipe.Items[0]),
                                                                              subFactoryProductionRegisterBuilder.AddProduct(fractionatorRecipe.Results[0]),
                                                                              fractionatorRecipe.ResultCounts[0] / (float)fractionatorRecipe.ItemCounts[0]);
                fractionatorRecipeProducts.Add(fractionatorRecipeProduct);
            }

            _fractionatorNetworkId = fractionatorNetworkId.ToArray();
        }

        _optimizedFractionators = optimizedFractionators.ToArray();
        _fractionatorsPowerFields = fractionatorsPowerFields.ToArray();
        _fractionatorIdToOptimizedIndex = fractionatorIdToOptimizedIndex;
        _fractionatorRecipeProducts = fractionatorRecipeProducts.ToArray();
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(universeStaticDataBuilder);
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, ref readonly FractionatorPowerFields fractionatorPowerFields)
    {
        double num = fractionatorPowerFields.fluidInputCargoCount > 0.0001 ? fractionatorPowerFields.fluidInputCount / fractionatorPowerFields.fluidInputCargoCount : 4f;
        double num2 = (double)(fractionatorPowerFields.fluidInputCargoCount < 30f ? fractionatorPowerFields.fluidInputCargoCount : 30f) * num - 30.0;
        if (num2 < 0.0)
        {
            num2 = 0.0;
        }
        int permillage = (int)((num2 * 50.0 + 1000.0) * Cargo.powerTableRatio[fractionatorPowerFields.incLevel] + 0.5);
        return powerConsumerType.GetRequiredEnergy(fractionatorPowerFields.isWorking, permillage);
    }
}
