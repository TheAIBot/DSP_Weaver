using System.Collections.Generic;
using System.Threading;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class StarClusterWorkManager
{
    private readonly List<PlanetWorkManager> _planetWorkManagers = [];
    private readonly Dictionary<PlanetFactory, PlanetWorkManager> _planetToWorkManagers = [];
    private int _planetsWithWorkScheduledCount;
    private int _planetsNotCompletedCount;

    public int Parallelism { get; private set; } = -1;

    public void UpdateListOfPlanets(PlanetFactory?[] allPlanets, PlanetFactory?[] planetsToUpdate, int parallelism)
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

            IOptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(planet);
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
            if (!workManager.UpdatePlanetWork(parallelism))
            {
                continue;
            }

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

        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            if (_planetWorkManagers[i] == null)
            {
                continue;
            }

            PlanetWorkPlan? planetWorkPlan = TryGetWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            PlanetWorkPlan? planetWorkPlan = TryWaitForWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

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

    public StarClusterWorkStatistics GetStartClusterStatistics()
    {
        List<PlanetWorkStatistics> planetWorkStatistics = [];
        foreach (var planetWorkManager in _planetWorkManagers)
        {
            PlanetWorkStatistics? planetWorkStatistic = planetWorkManager.GetPlanetWorkStatistics();
            if (planetWorkStatistic == null)
            {
                continue;
            }

            planetWorkStatistics.Add(planetWorkStatistic.Value);
        }

        return new StarClusterWorkStatistics(planetWorkStatistics.ToArray());
    }

    private PlanetWorkPlan? TryGetWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        IWorkChunk? workChunk = planetWorkManager.TryGetWork(out bool canScheduleMoreWork);
        if (!canScheduleMoreWork)
        {
            int lastPlanetNotCompletedIndex = Interlocked.Decrement(ref _planetsNotCompletedCount);
            if (lastPlanetNotCompletedIndex > 0)
            {
                _planetWorkManagers[planetIndex] = _planetWorkManagers[lastPlanetNotCompletedIndex];
            }
        }
        if (workChunk == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workChunk);
    }

    private PlanetWorkPlan? TryWaitForWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        IWorkChunk? workChunk = planetWorkManager.TryWaitForWork();
        if (workChunk == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workChunk);
    }
}
