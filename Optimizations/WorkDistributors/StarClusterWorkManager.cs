using System;
using System.Collections.Generic;
using System.Threading;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class StarClusterWorkManager : IDisposable
{
    private readonly Dictionary<PlanetFactory, PlanetWorkManager> _planetToWorkManagers = [];
    private readonly List<SolarSystemWorkManager> _solarSystemWorkManagers = [];
    private readonly Dictionary<StarData, SolarSystemWorkManager> _starToWorkManagers = [];
    private readonly List<IWorkNode> _solarSystemWorkNodes = [];
    private RootWorkNode? _rootWorkNode;

    public int Parallelism { get; private set; } = -1;

    public void UpdateListOfPlanets(PlanetFactory?[] allPlanets, int parallelism)
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
            PlanetWorkManager planetWorkManager = new PlanetWorkManager(optimizedPlanet);
            _planetToWorkManagers.Add(planet, planetWorkManager);

            if (!_starToWorkManagers.TryGetValue(planet.planet.star, out SolarSystemWorkManager? solarSystemWorkManager))
            {
                solarSystemWorkManager = new SolarSystemWorkManager();
                _solarSystemWorkManagers.Add(solarSystemWorkManager);
                _starToWorkManagers.Add(planet.planet.star, solarSystemWorkManager);
            }

            solarSystemWorkManager.AddPlanet(planetWorkManager);
        }

        for (int i = 0; i < _solarSystemWorkManagers.Count; i++)
        {
            if (_solarSystemWorkManagers[i].UpdateSolarSystemWork(parallelism))
            {
                _rootWorkNode?.Dispose();
                _rootWorkNode = null;
            }
        }

        if (_rootWorkNode == null)
        {
            _solarSystemWorkNodes.Clear();
            for (int i = 0; i < _solarSystemWorkManagers.Count; i++)
            {
                if (_solarSystemWorkManagers[i].TryGetSolarSystemWork(out IWorkNode? solarSystemWorkNode))
                {
                    _solarSystemWorkNodes.Add(solarSystemWorkNode);
                }
            }

            if (_solarSystemWorkNodes.Count == 0)
            {
                _rootWorkNode = new RootWorkNode(new WorkNode([]));
                return;
            }

            _rootWorkNode = new RootWorkNode(new WorkNode([_solarSystemWorkNodes.ToArray()]));
        }
    }

    public RootWorkNode GetRootWorkNode()
    {
        if (_rootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _rootWorkNode;
    }

    public void Reset()
    {
        if (_rootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        _rootWorkNode.Reset();
    }

    public StarClusterWorkStatistics GetStarClusterStatistics()
    {
        List<PlanetWorkStatistics> planetWorkStatistics = [];
        foreach (var solarSystemWorkManager in _solarSystemWorkManagers)
        {
            planetWorkStatistics.AddRange(solarSystemWorkManager.GetPlanetWorkStatistics());
        }

        return new StarClusterWorkStatistics(planetWorkStatistics.ToArray());
    }

    public void Dispose()
    {

    }
}
