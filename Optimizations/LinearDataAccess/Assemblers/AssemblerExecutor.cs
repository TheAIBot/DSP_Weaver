using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

internal sealed class AssemblerExecutor
{
    public void GameTick(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedAssembler[] optimizedAssemblers = optimizedPlanet._optimizedAssemblers;
        AssemblerRecipe[] assemblerRecipes = optimizedPlanet._assemblerRecipes;
        bool[] assemblerReplicatings = optimizedPlanet._assemblerReplicatings;
        int[] assemblerExtraPowerRatios = optimizedPlanet._assemblerExtraPowerRatios;

        if (!WorkerThreadExecutor.CalculateMissionIndex(0, optimizedAssemblers.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        for (int k = _start; k < _end; k++)
        {
            ref NetworkIdAndState<AssemblerState> assemblerNetworkIdAndState = ref optimizedPlanet._assemblerNetworkIdAndStates[k];
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

    public void UpdatePower(OptimizedPlanet optimizedPlanet,
                            int[] assemblerPowerConsumerTypeIndexes,
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

        OptimizedAssembler[] optimizedAssemblers = optimizedPlanet._optimizedAssemblers;
        NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates = optimizedPlanet._assemblerNetworkIdAndStates;
        bool[] assemblerReplicatings = optimizedPlanet._assemblerReplicatings;
        int[] assemblerExtraPowerRatios = optimizedPlanet._assemblerExtraPowerRatios;
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
}
