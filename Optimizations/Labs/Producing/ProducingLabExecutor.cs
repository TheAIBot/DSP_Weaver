using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Producing;

internal sealed class ProducingLabExecutor
{
    public NetworkIdAndState<LabState>[] _networkIdAndStates = null!;
    public OptimizedProducingLab[] _optimizedLabs = null!;
    public LabPowerFields[] _labsPowerFields = null!;
    public short[] _labRecipeIndexes = null!;
    public ProducingLabRecipe[] _producingLabRecipes = null!;
    public int[] _entityIds = null!;
    public Dictionary<int, int> _labIdToOptimizedLabIndex = null!;
    public HashSet<int> _unOptimizedLabIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int _producedSize = -1;
    public short[] _served = null!;
    public short[] _incServed = null!;
    public short[] _produced = null!;

    public int Count => _optimizedLabs.Length;

    public void GameTickLabProduceMode(PlanetFactory planet,
                                       int[] producingLabPowerConsumerIndexes,
                                       PowerConsumerType[] powerConsumerTypes,
                                       long[] thisSubFactoryNetworkPowerConsumption,
                                       int[] productRegister,
                                       int[] consumeRegister,
                                       SubFactoryNeeds subFactoryNeeds)
    {
        float[] networkServes = planet.powerSystem.networkServes;
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ProducingLab);
        short[] needs = subFactoryNeeds.Needs;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        short[] labRecipeIndexes = _labRecipeIndexes;

        for (int labIndex = 0; labIndex < optimizedLabs.Length; labIndex++)
        {
            ref NetworkIdAndState<LabState> networkIdAndState = ref networkIdAndStates[labIndex];
            ref LabPowerFields labPowerFields = ref labsPowerFields[labIndex];
            if ((LabState)networkIdAndState.State != LabState.Active)
            {
                UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIdAndState.Index, labPowerFields);
                continue;
            }

            ref OptimizedProducingLab lab = ref optimizedLabs[labIndex];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[labRecipeIndexes[labIndex]];
            lab.UpdateNeedsAssemble(in producingLabRecipe,
                                    groupNeeds,
                                    served,
                                    needs,
                                    labIndex);

            int servedOffset = labIndex * groupNeeds.GroupNeedsSize;
            int producedOffset = labIndex * producedSize;
            float power = networkServes[networkIdAndState.Index];
            networkIdAndState.State = (int)lab.InternalUpdateAssemble(power,
                                                                      productRegister,
                                                                      consumeRegister,
                                                                      in producingLabRecipe,
                                                                      ref labPowerFields,
                                                                      servedOffset,
                                                                      producedOffset,
                                                                      served,
                                                                      incServed,
                                                                      produced);

            UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIdAndState.Index, labPowerFields);
        }
    }

    public void GameTickLabOutputToNext(SubFactoryNeeds subFactoryNeeds)
    {
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ProducingLab);
        short[] needs = subFactoryNeeds.Needs;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        short[] labRecipeIndexes = _labRecipeIndexes;
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        for (int labIndex = (int)(GameMain.gameTick % 5); labIndex < optimizedLabs.Length; labIndex += 5)
        {
            int servedOffset = labIndex * groupNeeds.GroupNeedsSize;
            ref OptimizedProducingLab lab = ref optimizedLabs[labIndex];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[labRecipeIndexes[labIndex]];
            lab.UpdateOutputToNext(labIndex,
                                   optimizedLabs,
                                   networkIdAndStates,
                                   in producingLabRecipe,
                                   groupNeeds,
                                   needs,
                                   servedOffset,
                                   producedSize,
                                   served,
                                   incServed,
                                   produced);
        }
    }

    public void UpdatePower(int[] producingLabPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int labIndex = 0; labIndex < _optimizedLabs.Length; labIndex++)
        {
            int networkIndex = networkIdAndStates[labIndex].Index;
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
        }
    }

    private static void UpdatePower(int[] producingLabPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int labIndex,
                                    int networkIndex,
                                    LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = producingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] producingLabPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        LabPowerFields[] labsPowerFields = _labsPowerFields;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
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

    private static void UpdatePowerConsumptionPerPrototype(int[] producingLabPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
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
        short[] needs = subFactoryNeeds.Needs;
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
                              needs,
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
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder)
    {
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedProducingLab> optimizedLabs = [];
        List<LabPowerFields> labsPowerFields = [];
        Dictionary<ProducingLabRecipe, int> producingLabRecipeToRecipeIndex = [];
        List<ProducingLabRecipe> producingLabRecipes = [];
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
            if (!producingLabRecipeToRecipeIndex.TryGetValue(producingLabRecipe, out int producingLabRecipeIndex))
            {
                producingLabRecipeToRecipeIndex.Add(producingLabRecipe, producingLabRecipes.Count);
                producingLabRecipeIndex = producingLabRecipes.Count;
                producingLabRecipes.Add(producingLabRecipe);
            }

            labIdToOptimizedLabIndex.Add(labIndex, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedProducingLab(nextLabIndex, ref lab));
            labsPowerFields.Add(new LabPowerFields(in lab));
            int networkIndex = planet.powerSystem.consumerPool[lab.pcId].networkId;
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, networkIndex));
            entityIds.Add(lab.entityId);
            served.Add(lab.served);
            incServed.Add(lab.incServed);
            produced.Add(lab.produced);
            labRecipeIndexes.Add((short)producingLabRecipeIndex);
            subFactoryPowerSystemBuilder.AddProducingLab(in lab, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[lab.entityId]);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
            needsBuilder.AddNeeds(lab.needs, producingLabRecipe.Requires.Length);
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

        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _producingLabRecipes = producingLabRecipes.ToArray();
        _entityIds = entityIds.ToArray();
        _labRecipeIndexes = labRecipeIndexes.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, LabPowerFields producingLabPowerFields)
    {
        return powerConsumerType.GetRequiredEnergy(producingLabPowerFields.replicating, 1000 + producingLabPowerFields.extraPowerRatio);
    }
}
