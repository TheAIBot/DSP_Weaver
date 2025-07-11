﻿using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Producing;

internal sealed class ProducingLabExecutor
{
    public NetworkIdAndState<LabState>[] _networkIdAndStates = null!;
    public OptimizedProducingLab[] _optimizedLabs = null!;
    public LabPowerFields[] _labsPowerFields = null!;
    public ProducingLabRecipe[] _producingLabRecipes = null!;
    public int[] _entityIds = null!;
    public Dictionary<int, int> _labIdToOptimizedLabIndex = null!;
    public HashSet<int> _unOptimizedLabIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int ProducingLabCount => _optimizedLabs.Length;

    public void GameTickLabProduceMode(PlanetFactory planet,
                                       int[] producingLabPowerConsumerIndexes,
                                       PowerConsumerType[] powerConsumerTypes,
                                       long[] thisSubFactoryNetworkPowerConsumption,
                                       int[] productRegister,
                                       int[] consumeRegister)
    {
        float[] networkServes = planet.powerSystem.networkServes;
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
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
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
            lab.UpdateNeedsAssemble(in producingLabRecipe);

            float power = networkServes[networkIdAndState.Index];
            networkIdAndState.State = (int)lab.InternalUpdateAssemble(power,
                                                                      productRegister,
                                                                      consumeRegister,
                                                                      in producingLabRecipe,
                                                                      ref labPowerFields);

            UpdatePower(producingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIdAndState.Index, labPowerFields);
        }
    }

    public void GameTickLabOutputToNext()
    {
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        for (int i = (int)(GameMain.gameTick % 5); i < optimizedLabs.Length; i += 5)
        {
            ref OptimizedProducingLab lab = ref optimizedLabs[i];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
            lab.UpdateOutputToNext(i, optimizedLabs, networkIdAndStates, in producingLabRecipe);
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

    public void Save(PlanetFactory planet)
    {
        LabComponent[] labComponents = planet.factorySystem.labPool;
        OptimizedProducingLab[] optimizedProducingLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            if (!_labIdToOptimizedLabIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref OptimizedProducingLab optimizedLab = ref optimizedProducingLabs[optimizedIndex];
            optimizedLab.Save(ref labComponents[i], labsPowerFields[optimizedIndex]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedProducingLab> optimizedLabs = [];
        List<LabPowerFields> labsPowerFields = [];
        Dictionary<ProducingLabRecipe, int> producingLabRecipeToRecipeIndex = [];
        List<ProducingLabRecipe> producingLabRecipes = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];
        HashSet<int> unOptimizedLabIds = [];
        GameHistoryData historyData = planet.gameData.history;
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

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
            optimizedLabs.Add(new OptimizedProducingLab(producingLabRecipeIndex, nextLabIndex, ref lab));
            labsPowerFields.Add(new LabPowerFields(in lab));
            int networkIndex = planet.powerSystem.consumerPool[lab.pcId].networkId;
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, networkIndex));
            entityIds.Add(lab.entityId);
            subFactoryPowerSystemBuilder.AddProducingLab(in lab, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[lab.entityId]);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
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

        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _producingLabRecipes = producingLabRecipes.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, LabPowerFields producingLabPowerFields)
    {
        return powerConsumerType.GetRequiredEnergy(producingLabPowerFields.replicating, 1000 + producingLabPowerFields.extraPowerRatio);
    }
}
