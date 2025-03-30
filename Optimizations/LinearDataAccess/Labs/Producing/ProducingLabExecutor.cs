using System;
using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Producing;

internal sealed class ProducingLabExecutor
{
    public NetworkIdAndState<LabState>[] _networkIdAndStates;
    public OptimizedProducingLab[] _optimizedLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public int[] _entityIds;
    public Dictionary<int, int> _labIdToOptimizedLabIndex;

    public void GameTickLabProduceMode(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, _optimizedLabs.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }

        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        for (int i = _start; i < _end; i++)
        {
            NetworkIdAndState<LabState> networkIdAndState = _networkIdAndStates[i];

            ref OptimizedProducingLab lab = ref optimizedLabs[i];
            ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
            lab.UpdateNeedsAssemble(in producingLabRecipe);

            float power = networkServes[networkIdAndState.Index];
            lab.InternalUpdateAssemble(power,
                                       productRegister,
                                       consumeRegister,
                                       in producingLabRecipe);
        }
    }

    public void GameTickLabOutputToNext(long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedProducingLab[] optimizedLabs = _optimizedLabs;
        ProducingLabRecipe[] producingLabRecipes = _producingLabRecipes;
        int num = 0;
        int num2 = 0;
        for (int i = (int)(GameMain.gameTick % 5); i < _optimizedLabs.Length; i += 5)
        {
            if (num == _curThreadIdx)
            {
                ref OptimizedProducingLab lab = ref optimizedLabs[i];
                ref readonly ProducingLabRecipe producingLabRecipe = ref producingLabRecipes[lab.producingLabRecipeIndex];
                lab.UpdateOutputToNext(optimizedLabs, in producingLabRecipe);
            }
            num2++;
            if (num2 >= _minimumMissionCnt)
            {
                num2 = 0;
                num++;
                num %= _usedThreadCnt;
            }
        }
    }

    public void Initialize(PlanetFactory planet)
    {
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedProducingLab> optimizedLabs = [];
        Dictionary<ProducingLabRecipe, int> producingLabRecipeToRecipeIndex = [];
        List<ProducingLabRecipe> producingLabRecipes = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];

        for (int i = 0; i < planet.factorySystem.labCursor; i++)
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            if (lab.id != i)
            {
                continue;
            }

            if (lab.researchMode)
            {
                continue;
            }

            if (lab.recipeId == 0)
            {
                continue;
            }

            int? nextLabIndex = null;
            if (planet.factorySystem.labPool[lab.nextLabId].id != 0 &&
                planet.factorySystem.labPool[lab.nextLabId].id == lab.nextLabId)
            {
                nextLabIndex = lab.nextLabId;
            }

            var producingLabRecipe = new ProducingLabRecipe(in lab);
            if (!producingLabRecipeToRecipeIndex.TryGetValue(producingLabRecipe, out int producingLabRecipeIndex))
            {
                producingLabRecipeToRecipeIndex.Add(producingLabRecipe, producingLabRecipes.Count);
                producingLabRecipeIndex = producingLabRecipes.Count;
                producingLabRecipes.Add(producingLabRecipe);
            }

            labIdToOptimizedLabIndex.Add(i, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedProducingLab(producingLabRecipeIndex, nextLabIndex, ref lab));
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, planet.powerSystem.consumerPool[lab.pcId].networkId));
            entityIds.Add(lab.entityId);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
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
        _producingLabRecipes = producingLabRecipes.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
    }
}
