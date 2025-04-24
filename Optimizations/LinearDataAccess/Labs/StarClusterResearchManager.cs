using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.Labs;

internal sealed class StarClusterResearchManager
{
    private readonly Queue<ResearchedTech> _tickResearchedTech = [];

    public void AddResearchedTech(int researchTechId, int previousTechLevel, TechState state, TechProto proto)
    {
        lock (_tickResearchedTech)
        {
            _tickResearchedTech.Enqueue(new ResearchedTech(researchTechId, previousTechLevel, state, proto));
        }
    }

    /// <summary>
    /// This can only run in the main UI thread because it calls unity UI API.
    /// </summary>
    public void UIThreadUnlockResearchedTechnologies(GameHistoryData history)
    {
        bool hasResearchedAnything = false;
        foreach (ResearchedTech technology in _tickResearchedTech)
        {
            if (technology.State.unlocked)
            {
                hasResearchedAnything = true;
                history.techStates[technology.ResearchTechId] = technology.State;
                for (int l = 0; l < technology.Proto.UnlockRecipes.Length; l++)
                {
                    history.UnlockRecipe(technology.Proto.UnlockRecipes[l]);
                }
                for (int m = 0; m < technology.Proto.UnlockFunctions.Length; m++)
                {
                    history.UnlockTechFunction(technology.Proto.UnlockFunctions[m], technology.Proto.UnlockValues[m], technology.PreviousTechLevel);
                }
                for (int n = 0; n < technology.Proto.AddItems.Length; n++)
                {
                    history.GainTechAwards(technology.Proto.AddItems[n], technology.Proto.AddItemCounts[n]);
                }
                history.NotifyTechUnlock(technology.ResearchTechId, technology.PreviousTechLevel);
            }
            if (technology.State.curLevel > technology.PreviousTechLevel)
            {
                hasResearchedAnything = true;
                history.techStates[technology.ResearchTechId] = technology.State;
                for (int num6 = 0; num6 < technology.Proto.UnlockFunctions.Length; num6++)
                {
                    history.UnlockTechFunction(technology.Proto.UnlockFunctions[num6], technology.Proto.UnlockValues[num6], technology.PreviousTechLevel);
                }
                for (int num7 = 0; num7 < technology.Proto.AddItems.Length; num7++)
                {
                    history.GainTechAwards(technology.Proto.AddItems[num7], technology.Proto.AddItemCounts[num7]);
                }
                history.NotifyTechUnlock(technology.ResearchTechId, technology.PreviousTechLevel);
            }
        }

        _tickResearchedTech.Clear();

        if (hasResearchedAnything)
        {
            // Optimized planets never change their settings so reoptimization is required
            // if the technology changes any configuration in the factory
            OptimizedStarCluster.ReOptimizeAllPlanets();
        }
    }
}