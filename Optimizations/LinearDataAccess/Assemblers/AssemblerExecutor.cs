using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

internal sealed class AssemblerExecutor
{
    public NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates = null!;
    public OptimizedAssembler[] _optimizedAssemblers = null!;
    private bool[] _assemblerReplicatings = null!;
    private int[] _assemblerExtraPowerRatios = null!;
    private AssemblerTimingData[] _assemblersTimingData = null!;
    public AssemblerNeeds[] _assemblersNeeds = null!;
    public int[] _assemblerRecipeIndexes = null!;
    public AssemblerRecipe[] _assemblerRecipes = null!;
    public Dictionary<int, int> _assemblerIdToOptimizedIndex = null!;
    public HashSet<int> _unOptimizedAssemblerIds = null!;

    public int AssemblerCount => _optimizedAssemblers.Length;

    public void GameTick(PlanetFactory planet, int[] productRegister, int[] consumeRegister)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        AssemblerRecipe[] assemblerRecipes = _assemblerRecipes;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        AssemblerTimingData[] assemblersTimingData = _assemblersTimingData;
        AssemblerNeeds[] assemblersNeeds = _assemblersNeeds;
        int[] assemblerRecipeIndexes = _assemblerRecipeIndexes;

        for (int k = 0; k < optimizedAssemblers.Length; k++)
        {
            ref NetworkIdAndState<AssemblerState> assemblerNetworkIdAndState = ref _assemblerNetworkIdAndStates[k];
            if ((AssemblerState)assemblerNetworkIdAndState.State != AssemblerState.Active)
            {
                continue;
            }

            AssemblerRecipe recipeData = assemblerRecipes[assemblerRecipeIndexes[k]];
            AssemblerNeeds assemblerNeeds = assemblersNeeds[k];
            ref AssemblerTimingData assemblerTimingData = ref assemblersTimingData[k];
            OptimizedAssembler.UpdateNeeds(ref recipeData, ref assemblerTimingData, ref assemblerNeeds);

            float power = networkServes[assemblerNetworkIdAndState.Index];
            ref bool replicating = ref assemblerReplicatings[k];
            if (!assemblerTimingData.UpdateTimings(power, replicating, in recipeData))
            {
                continue;
            }

            ref OptimizedAssembler assembler = ref optimizedAssemblers[k];
            ref int extraPowerRatios = ref assemblerExtraPowerRatios[k];
            assemblerNetworkIdAndState.State = (int)assembler.Update(power,
                                                                     productRegister,
                                                                     consumeRegister,
                                                                     ref recipeData,
                                                                     ref replicating,
                                                                     ref extraPowerRatios,
                                                                     ref assemblerTimingData,
                                                                     ref assemblerNeeds);
        }
    }

    public void UpdatePower(int[] assemblerPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates = _assemblerNetworkIdAndStates;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        for (int j = 0; j < optimizedAssemblers.Length; j++)
        {
            int networkIndex = assemblerNetworkIdAndStates[j].Index;
            int powerConsumerTypeIndex = assemblerPowerConsumerTypeIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, assemblerReplicatings[j], assemblerExtraPowerRatios[j]);
        }
    }

    public void Save(PlanetFactory planet)
    {
        AssemblerComponent[] assemblers = planet.factorySystem.assemblerPool;
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        AssemblerRecipe[] assemblerRecipes = _assemblerRecipes;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        AssemblerTimingData[] assemblersTimingData = _assemblersTimingData;
        AssemblerNeeds[] assemblersNeeds = _assemblersNeeds;
        int[] assemblerRecipeIndexes = _assemblerRecipeIndexes;
        for (int i = 1; i < planet.factorySystem.assemblerCursor; i++)
        {
            if (!_assemblerIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedAssembler optimizedAssembler = ref optimizedAssemblers[optimizedIndex];
            ref readonly AssemblerRecipe assemblerRecipe = ref assemblerRecipes[assemblerRecipeIndexes[optimizedIndex]];
            optimizedAssembler.Save(ref assemblers[i],
                                    in assemblerRecipe,
                                    assemblerReplicatings[optimizedIndex],
                                    assemblerExtraPowerRatios[optimizedIndex],
                                    in assemblersTimingData[optimizedIndex],
                                    in assemblersNeeds[optimizedIndex]);
        }
    }

    public void InitializeAssemblers(PlanetFactory planet,
                                     Graph subFactoryGraph,
                                     OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                                     SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        List<NetworkIdAndState<AssemblerState>> assemblerNetworkIdAndStates = [];
        List<OptimizedAssembler> optimizedAssemblers = [];
        List<bool> assemblerReplicatings = [];
        List<int> assemblerExtraPowerRatios = [];
        List<AssemblerTimingData> assemblersTimingData = [];
        List<AssemblerNeeds> assemblersNeeds = [];
        List<int> assemblerRecipeIndexes = [];
        Dictionary<AssemblerRecipe, int> assemblerRecipeToIndex = [];
        List<AssemblerRecipe> assemblerRecipes = [];
        Dictionary<int, int> assemblerIdToOptimizedIndex = [];
        HashSet<int> unOptimizedAssemblerIds = [];
        GameHistoryData historyData = planet.gameData.history;
        foreach (int assemblerIndex in subFactoryGraph.GetAllNodes()
                                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Assembler)
                                                      .Select(x => x.EntityTypeIndex.Index)
                                                      .OrderBy(x => x))
        {
            ref AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[assemblerIndex];
            if (assembler.id != assemblerIndex)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            if (assembler.recipeId == 0)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            // An assembler where no recipe has ever been set will somehow have recipeId != 0
            // while needs == null
            if (assembler.needs == null)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            // It is possible to put a locked recipe into a building by using blueprints.
            // Such buildings should not run at all.
            // Planet reoptimization will enable the recipe when it has been researched.
            if (!historyData.RecipeUnlocked(assembler.recipeId))
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            AssemblerRecipe assemblerRecipe = new AssemblerRecipe(assembler.recipeId,
                                                                  assembler.recipeType,
                                                                  assembler.timeSpend,
                                                                  assembler.extraTimeSpend,
                                                                  assembler.productive,
                                                                  subFactoryProductionRegisterBuilder.AddConsume(assembler.requires),
                                                                  assembler.requireCounts,
                                                                  subFactoryProductionRegisterBuilder.AddProduct(assembler.products),
                                                                  assembler.productCounts);
            if (!assemblerRecipeToIndex.TryGetValue(assemblerRecipe, out int assemblerRecipeIndex))
            {
                assemblerRecipeIndex = assemblerRecipeToIndex.Count;
                assemblerRecipeToIndex.Add(assemblerRecipe, assemblerRecipeIndex);
                assemblerRecipes.Add(assemblerRecipe);
            }

            assemblerIdToOptimizedIndex.Add(assembler.id, optimizedAssemblers.Count);
            int networkIndex = planet.powerSystem.consumerPool[assembler.pcId].networkId;
            assemblerNetworkIdAndStates.Add(new NetworkIdAndState<AssemblerState>((int)(assembler.recipeId == 0 ? AssemblerState.InactiveNoRecipeSet : AssemblerState.Active),
                                                                                  networkIndex));
            optimizedAssemblers.Add(new OptimizedAssembler(ref assembler));
            assemblerReplicatings.Add(assembler.replicating);
            assemblerExtraPowerRatios.Add(assembler.extraPowerRatio);
            assemblersTimingData.Add(new AssemblerTimingData(in assembler));
            assemblersNeeds.Add(new AssemblerNeeds(in assembler));
            assemblerRecipeIndexes.Add(assemblerRecipeIndex);
            optimizedPowerSystemBuilder.AddAssembler(ref assembler, networkIndex);


            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates.ToArray();
        _assemblerRecipes = assemblerRecipes.ToArray();
        _optimizedAssemblers = optimizedAssemblers.ToArray();
        _assemblerReplicatings = assemblerReplicatings.ToArray();
        _assemblerExtraPowerRatios = assemblerExtraPowerRatios.ToArray();
        _assemblersTimingData = assemblersTimingData.ToArray();
        _assemblersNeeds = assemblersNeeds.ToArray();
        _assemblerRecipeIndexes = assemblerRecipeIndexes.ToArray();
        _assemblerIdToOptimizedIndex = assemblerIdToOptimizedIndex;
        _unOptimizedAssemblerIds = unOptimizedAssemblerIds;
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, bool assemblerReplicating, int assemblerExtraPowerRatio)
    {
        return powerConsumerType.GetRequiredEnergy(assemblerReplicating, 1000 + assemblerExtraPowerRatio);
    }
}
