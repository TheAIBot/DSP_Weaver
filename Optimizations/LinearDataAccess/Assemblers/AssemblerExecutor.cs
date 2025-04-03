using System.Collections.Generic;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

internal sealed class AssemblerExecutor
{
    public NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates;
    public OptimizedAssembler[] _optimizedAssemblers;
    private bool[] _assemblerReplicatings;
    private int[] _assemblerExtraPowerRatios;
    public AssemblerRecipe[] _assemblerRecipes;
    public Dictionary<int, int> _assemblerIdToOptimizedIndex;

    public void GameTick(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        AssemblerRecipe[] assemblerRecipes = _assemblerRecipes;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;

        if (!WorkerThreadExecutor.CalculateMissionIndex(0, optimizedAssemblers.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        for (int k = _start; k < _end; k++)
        {
            ref NetworkIdAndState<AssemblerState> assemblerNetworkIdAndState = ref _assemblerNetworkIdAndStates[k];
            if ((AssemblerState)assemblerNetworkIdAndState.State != AssemblerState.Active)
            {
                continue;
            }

            float power = networkServes[assemblerNetworkIdAndState.Index];
            ref OptimizedAssembler assembler = ref optimizedAssemblers[k];
            ref AssemblerRecipe recipeData = ref assemblerRecipes[assembler.assemblerRecipeIndex];
            assembler.UpdateNeeds(ref recipeData);

            ref bool replicating = ref assemblerReplicatings[k];
            ref int extraPowerRatios = ref assemblerExtraPowerRatios[k];
            assemblerNetworkIdAndState.State = (int)assembler.Update(power,
                                                                     productRegister,
                                                                     consumeRegister,
                                                                     ref recipeData,
                                                                     ref replicating,
                                                                     ref extraPowerRatios);
        }
    }

    public void UpdatePower(int[] assemblerPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisThreadNetworkPowerConsumption,
                            int _usedThreadCnt,
                            int _curThreadIdx,
                            int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, assemblerPowerConsumerTypeIndexes.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates = _assemblerNetworkIdAndStates;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        for (int j = _start; j < _end; j++)
        {
            int networkIndex = assemblerNetworkIdAndStates[j].Index;
            int powerConsumerTypeIndex = assemblerPowerConsumerTypeIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisThreadNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, assemblerReplicatings[j], assemblerExtraPowerRatios[j]);
        }
    }

    public long GetPowerConsumption(PowerConsumerType powerConsumerType, bool assemblerReplicating, int assemblerExtraPowerRatio)
    {
        return powerConsumerType.GetRequiredEnergy(assemblerReplicating, 1000 + assemblerExtraPowerRatio);
    }

    public void InitializeAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        List<NetworkIdAndState<AssemblerState>> assemblerNetworkIdAndStates = [];
        List<OptimizedAssembler> optimizedAssemblers = [];
        List<bool> assemblerReplicatings = [];
        List<int> assemblerExtraPowerRatios = [];
        Dictionary<AssemblerRecipe, int> assemblerRecipeToIndex = [];
        List<AssemblerRecipe> assemblerRecipes = [];
        Dictionary<int, int> assemblerIdToOptimizedIndex = [];

        for (int i = 0; i < planet.factorySystem.assemblerCursor; i++)
        {
            ref AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            if (assembler.recipeId == 0)
            {
                continue;
            }

            AssemblerRecipe assemblerRecipe = new AssemblerRecipe(assembler.recipeId,
                                                                  assembler.recipeType,
                                                                  assembler.timeSpend,
                                                                  assembler.extraTimeSpend,
                                                                  assembler.productive,
                                                                  assembler.requires,
                                                                  assembler.requireCounts,
                                                                  assembler.products,
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
            optimizedAssemblers.Add(new OptimizedAssembler(assemblerRecipeIndex, ref assembler));
            assemblerReplicatings.Add(assembler.replicating);
            assemblerExtraPowerRatios.Add(assembler.extraPowerRatio);
            optimizedPowerSystemBuilder.AddAssembler(ref assembler, networkIndex);


            // set it here so we don't have to set it in the update loop.
            // Need to remember to update it when the assemblers recipe is changed.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates.ToArray();
        _assemblerRecipes = assemblerRecipes.ToArray();
        _optimizedAssemblers = optimizedAssemblers.ToArray();
        _assemblerReplicatings = assemblerReplicatings.ToArray();
        _assemblerExtraPowerRatios = assemblerExtraPowerRatios.ToArray();
        _assemblerIdToOptimizedIndex = assemblerIdToOptimizedIndex;
    }
}
