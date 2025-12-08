using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Producing;

internal sealed class ProducingLabExecutor
{
    private ReadonlyArray<int> _labNetworkIds = default;
    public LabState[] _labStates = null!;
    public OptimizedProducingLab[] _optimizedLabs = null!;
    public LabPowerFields[] _labsPowerFields = null!;
    public ReadonlyArray<short> _labRecipeIndexes = default;
    public ReadonlyArray<int> _entityIds = default;
    public Dictionary<int, int> _labIdToOptimizedLabIndex = null!;
    public HashSet<int> _unOptimizedLabIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int _producedSize = -1;
    public short[] _served = null!;
    public short[] _incServed = null!;
    public short[] _produced = null!;

    public int Count => _optimizedLabs.Length;

    public void GameTickLabProduceMode(PlanetFactory planet,
                                       ReadonlyArray<short> producingLabPowerConsumerIndexes,
                                       ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                       long[] thisSubFactoryNetworkPowerConsumption,
                                       int[] productRegister,
                                       int[] consumeRegister,
                                       SubFactoryNeeds subFactoryNeeds,
                                       UniverseStaticData universeStaticData)
    {
        float[] networkServes = planet.powerSystem.networkServes;
        ReadonlyArray<int> labNetworkIds = _labNetworkIds;
        LabState[] labStates = _labStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        ReadonlyArray<ProducingLabRecipe> producingLabRecipes = universeStaticData.ProducingLabRecipes;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ProducingLab);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        ReadonlyArray<short> labRecipeIndexes = _labRecipeIndexes;

        for (int labIndex = 0; labIndex < optimizedLabs.Length; labIndex++)
        {
            int networkIndex = labNetworkIds[labIndex];
            ref LabState labState = ref labStates[labIndex];
            ref LabPowerFields labPowerFields = ref labsPowerFields[labIndex];
            if (labState != LabState.Active)
            {
                UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
                continue;
            }

            ref OptimizedProducingLab lab = ref optimizedLabs[labIndex];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[labRecipeIndexes[labIndex]];
            lab.UpdateNeedsAssemble(in producingLabRecipe,
                                    groupNeeds,
                                    served,
                                    componentsNeeds,
                                    labIndex);

            int servedOffset = labIndex * groupNeeds.GroupNeedsSize;
            int producedOffset = labIndex * producedSize;
            float power = networkServes[networkIndex];
            labState = lab.InternalUpdateAssemble(power,
                                                  productRegister,
                                                  consumeRegister,
                                                  in producingLabRecipe,
                                                  ref labPowerFields,
                                                  servedOffset,
                                                  producedOffset,
                                                  served,
                                                  incServed,
                                                  produced);

            UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
        }
    }

    public void GameTickLabOutputToNext(long time, SubFactoryNeeds subFactoryNeeds, UniverseStaticData universeStaticData)
    {
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ProducingLab);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        ReadonlyArray<short> labRecipeIndexes = _labRecipeIndexes;
        LabState[] labStates = _labStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ReadonlyArray<ProducingLabRecipe> producingLabRecipes = universeStaticData.ProducingLabRecipes;

        int num = (int)(time & 3);
        for (int labIndex = 0; labIndex < optimizedLabs.Length; labIndex++)
        {
            if ((labIndex & 3) == num)
            {
                int servedOffset = labIndex * groupNeeds.GroupNeedsSize;
                ref OptimizedProducingLab lab = ref optimizedLabs[labIndex];
                ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[labRecipeIndexes[labIndex]];

                lab.UpdateOutputToNext(labIndex,
                                       optimizedLabs,
                                       labStates,
                                       in producingLabRecipe,
                                       groupNeeds,
                                       componentsNeeds,
                                       servedOffset,
                                       producedSize,
                                       served,
                                       incServed,
                                       produced);
            }
        }
    }

    public void UpdatePower(ReadonlyArray<short> producingLabPowerConsumerIndexes,
                            ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        ReadonlyArray<int> labNetworkIds = _labNetworkIds;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int labIndex = 0; labIndex < _optimizedLabs.Length; labIndex++)
        {
            int networkIndex = labNetworkIds[labIndex];
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
        }
    }

    private static void UpdatePower(ReadonlyArray<short> producingLabPowerConsumerIndexes,
                                    ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int labIndex,
                                    int networkIndex,
                                    LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = producingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> producingLabPowerConsumerIndexes,
                                                                         ReadonlyArray<PowerConsumerType> powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        LabPowerFields[] labsPowerFields = _labsPowerFields;
        ReadonlyArray<int> prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int labIndex = 0; labIndex < labsPowerFields.Length; labIndex++)
        {
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePowerConsumptionPerPrototype(producingLabPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               labIndex,
                                               labPowerFields);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> producingLabPowerConsumerIndexes,
                                                           ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                                           ReadonlyArray<int> prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int labIndex,
                                                           LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = producingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[labIndex]] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        LabComponent[] labComponents = planet.factorySystem.labPool;
        OptimizedProducingLab[] optimizedProducingLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ProducingLab);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        short[] needsPatterns = subFactoryNeeds.NeedsPatterns;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            if (!_labIdToOptimizedLabIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref OptimizedProducingLab optimizedLab = ref optimizedProducingLabs[optimizedIndex];
            optimizedLab.Save(ref labComponents[i],
                              labsPowerFields[optimizedIndex],
                              groupNeeds,
                              componentsNeeds,
                              needsPatterns,
                              producedSize,
                              served,
                              incServed,
                              produced,
                              optimizedIndex);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        List<int> labNetworkIds = [];
        List<LabState> labStates = [];
        List<OptimizedProducingLab> optimizedLabs = [];
        List<LabPowerFields> labsPowerFields = [];
        HashSet<ProducingLabRecipe> producingLabRecipes = [];
        List<int> entityIds = [];
        List<short> labRecipeIndexes = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];
        HashSet<int> unOptimizedLabIds = [];
        List<int[]> served = [];
        List<int[]> incServed = [];
        List<int[]> produced = [];
        GameHistoryData historyData = planet.gameData.history;
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.ProducingLab);

        HashSet<int> labIndexesInSubFactory = new(subFactoryGraph.GetAllNodes()
                                                                 .Where(x => x.EntityTypeIndex.EntityType == EntityType.ProducingLab)
                                                                 .Select(x => x.EntityTypeIndex.Index));

        // Order  descending because it resolves an issue where items moving between stacked
        // labs would briefly clog. Not sure why this is an issue. I suspect this is an issue
        // from the base game that my sub-factory reordering causes.
        foreach (int labIndex in labIndexesInSubFactory.OrderByDescending(x => x))
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[labIndex];
            if (lab.id != labIndex)
            {
                unOptimizedLabIds.Add(labIndex);
                continue;
            }

            if (lab.researchMode)
            {
                unOptimizedLabIds.Add(labIndex);
                continue;
            }

            if (lab.recipeId == 0)
            {
                unOptimizedLabIds.Add(labIndex);
                continue;
            }

            // It is possible to put a locked recipe into a building by using blueprints.
            // Such buildings should not run at all.
            // Planet reoptimization will enable the recipe when it has been researched.
            if (!historyData.RecipeUnlocked(lab.recipeId))
            {
                unOptimizedLabIds.Add(labIndex);
                continue;
            }

            int? nextLabIndex = null;
            if (planet.factorySystem.labPool[lab.nextLabId].id != 0 &&
                planet.factorySystem.labPool[lab.nextLabId].id == lab.nextLabId)
            {
                nextLabIndex = lab.nextLabId;
                if (!labIndexesInSubFactory.Contains(nextLabIndex.Value))
                {
                    throw new InvalidOperationException($"Labs next lab index is not part of the current sub factory. {nameof(nextLabIndex)}: {nextLabIndex.Value}");
                }
            }

            var producingLabRecipe = new ProducingLabRecipe(in lab, subFactoryProductionRegisterBuilder);
            producingLabRecipes.Add(producingLabRecipe);
            int producingLabRecipeIndex = universeStaticDataBuilder.AddProducingLabRecipe(in producingLabRecipe);

            labIdToOptimizedLabIndex.Add(labIndex, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedProducingLab(nextLabIndex, ref lab));
            labsPowerFields.Add(new LabPowerFields(in lab));
            int networkIndex = planet.powerSystem.consumerPool[lab.pcId].networkId;
            labNetworkIds.Add(networkIndex);
            labStates.Add(LabState.Active);
            entityIds.Add(lab.entityId);
            served.Add(lab.served);
            incServed.Add(lab.incServed);
            produced.Add(lab.produced);
            labRecipeIndexes.Add((short)producingLabRecipeIndex);
            subFactoryPowerSystemBuilder.AddProducingLab(in lab, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[lab.entityId]);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
            needsBuilder.AddNeeds(lab.needs, lab.recipeExecuteData.requires);
        }

        for (int i = 0; i < optimizedLabs.Count; i++)
        {
            OptimizedProducingLab lab = optimizedLabs[i];
            if (lab.nextLabIndex == OptimizedProducingLab.NO_NEXT_LAB)
            {
                continue;
            }

            if (!labIdToOptimizedLabIndex.TryGetValue(lab.nextLabIndex, out int nextOptimizedLabIndex))
            {
                throw new InvalidOperationException("Next lab index was not part of the converted research labs.");
            }

            optimizedLabs[i] = new OptimizedProducingLab(nextOptimizedLabIndex, ref lab);
        }

        if (producingLabRecipes.Count > 0)
        {
            int maxServedsSize = producingLabRecipes.Max(x => x.Requires.Length);
            int maxProducedSize = producingLabRecipes.Max(x => x.Products.Length);
            List<short> servedFlat = [];
            List<short> incServedFlat = [];
            List<short> producedFlat = [];

            // Apparently some labs have a ridiculously high number of items inside it.
            // I assume this to be a bug an clamp it to -5000, 5000 because i don't expect any
            // lab recipe would need such a high buffer for anything.
            const int minAllowedValue = -5000;
            const int maxAllowedValue = 5000;

            for (int labIndex = 0; labIndex < optimizedLabs.Count; labIndex++)
            {
                for (int servedIndex = 0; servedIndex < maxServedsSize; servedIndex++)
                {
                    servedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(served[labIndex], servedIndex, minAllowedValue, maxAllowedValue));
                    incServedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(incServed[labIndex], servedIndex, minAllowedValue, maxAllowedValue));
                }

                for (int producedIndex = 0; producedIndex < maxProducedSize; producedIndex++)
                {
                    producedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(produced[labIndex], producedIndex, minAllowedValue, maxAllowedValue));
                }
            }

            _producedSize = maxProducedSize;
            _served = servedFlat.ToArray();
            _incServed = incServedFlat.ToArray();
            _produced = producedFlat.ToArray();
        }

        _labNetworkIds = universeStaticDataBuilder.DeduplicateArrayUnmanaged(labNetworkIds);
        _labStates = labStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _entityIds = universeStaticDataBuilder.DeduplicateArrayUnmanaged(entityIds);
        _labRecipeIndexes = universeStaticDataBuilder.DeduplicateArrayUnmanaged(labRecipeIndexes);
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(universeStaticDataBuilder);
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, LabPowerFields producingLabPowerFields)
    {
        return powerConsumerType.GetRequiredEnergy(producingLabPowerFields.replicating, 1000 + producingLabPowerFields.extraPowerRatio);
    }
}
