using System.Collections.Generic;
using System.Threading;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class StarClusterWorkManager
{
    private List<PlanetWorkManager> _planetWorkManagers = [];
    private Dictionary<PlanetFactory, PlanetWorkManager> _planetToWorkManagers = [];
    private int _planetsWithWorkScheduledCount;
    private int _planetsNotCompletedCount;

    public int Parallelism { get; private set; } = -1;

    public void UpdateListOfPlanets(PlanetFactory?[] allPlanets, PlanetFactory[] planetsToUpdate, int parallelism)
    {
        Parallelism = parallelism;

        foreach (PlanetFactory? planet in allPlanets)
        {
            if (planet == null)
            {
                continue;
            }

            if (_planetToWorkManagers.ContainsKey(planet))
            {
                continue;
            }

            OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(planet);
            _planetToWorkManagers.Add(planet, new PlanetWorkManager(planet, optimizedPlanet));
        }

        _planetWorkManagers.Clear();
        foreach (PlanetFactory? planet in planetsToUpdate)
        {
            if (planet == null)
            {
                continue;
            }

            PlanetWorkManager workManager = _planetToWorkManagers[planet];
            workManager.UpdatePlanetWork(parallelism);
            _planetWorkManagers.Add(workManager);
        }
    }

    public PlanetWorkPlan? TryGetWork()
    {
        int planetsNotCompletedCount = _planetsNotCompletedCount;
        while (_planetsWithWorkScheduledCount < planetsNotCompletedCount)
        {
            int planetIndex = Interlocked.Increment(ref _planetsWithWorkScheduledCount) - 1;
            if (planetIndex >= planetsNotCompletedCount)
            {
                break;
            }

            PlanetWorkPlan? planetWorkPlan = TryGetWork(planetIndex);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //while (true)
        //{
        bool hasIncompleteWork = false;
        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            if (_planetWorkManagers[i] == null)
            {
                continue;
            }
            hasIncompleteWork = true;

            PlanetWorkPlan? planetWorkPlan = TryGetWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //if (!hasIncompleteWork)
        //{
        //    break;
        //}

        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            PlanetWorkPlan? planetWorkPlan = TryWaitForWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }
        //}

        return null;
    }

    public void Reset()
    {
        for (int i = 0; i < _planetWorkManagers.Count; i++)
        {
            _planetWorkManagers[i].Reset();
        }

        _planetsWithWorkScheduledCount = 0;
        _planetsNotCompletedCount = _planetWorkManagers.Count;
    }

    private PlanetWorkPlan? TryGetWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryGetWork(out bool canScheduleMoreWork);
        if (!canScheduleMoreWork)
        {
            int lastPlanetNotCompletedIndex = Interlocked.Decrement(ref _planetsNotCompletedCount);
            if (lastPlanetNotCompletedIndex > 0)
            {
                _planetWorkManagers[planetIndex] = _planetWorkManagers[lastPlanetNotCompletedIndex];
            }
        }
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }

    private PlanetWorkPlan? TryWaitForWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryWaitForWork();
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }
}
