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

        if (!WorkerThreadExecutor.CalculateMissionIndex(1, optimizedAssemblers.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        for (int k = _start; k < _end; k++)
        {
            if (optimizedPlanet._assemblerStates[k] != AssemblerState.Active)
            {
                continue;
            }

            float power = networkServes[optimizedPlanet._assemblerNetworkIds[k]];
            ref OptimizedAssembler assembler = ref optimizedAssemblers[k];
            ref AssemblerRecipe recipeData = ref assemblerRecipes[assembler.assemblerRecipeIndex];
            assembler.UpdateNeeds(ref recipeData);
            optimizedPlanet._assemblerStates[k] = assembler.Update(power, productRegister, consumeRegister, ref recipeData);
        }
    }
}
