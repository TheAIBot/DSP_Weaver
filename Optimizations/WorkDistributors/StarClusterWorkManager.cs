using System;
using System.Collections.Generic;
using System.Threading;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class StarClusterWorkManager : IDisposable
{
    private readonly HashSet<DysonSphere> _assignedSpheres = [];
    private readonly Dictionary<PlanetFactory, PlanetWorkManager> _planetToWorkManagers = [];
    private readonly List<SolarSystemWorkManager> _solarSystemWorkManagers = [];
    private readonly Dictionary<StarData, SolarSystemWorkManager> _starToWorkManagers = [];
    private readonly List<IWorkNode> _solarSystemWorkNodes = [];
    private RootWorkNode? _rootWorkNode;

    public int Parallelism { get; private set; } = -1;

    public void UpdateListOfPlanets(GameLogic gameLogic, PlanetFactory?[] allPlanets, DysonSphere[] dysonSpheres, int parallelism)
    {
        Parallelism = parallelism;

        for (int i = 0; i < allPlanets.Length; i++)
        {
            PlanetFactory? planet = allPlanets[i];
            if (planet == null)
            {
                continue;
            }

            if (_planetToWorkManagers.ContainsKey(planet))
            {
                continue;
            }

            IOptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(planet);
            PlanetWorkManager planetWorkManager = new PlanetWorkManager(gameLogic, planet, optimizedPlanet);
            _planetToWorkManagers.Add(planet, planetWorkManager);

            if (!_starToWorkManagers.TryGetValue(planet.planet.star, out SolarSystemWorkManager? solarSystemWorkManager))
            {
                solarSystemWorkManager = new SolarSystemWorkManager();
                _solarSystemWorkManagers.Add(solarSystemWorkManager);
                _starToWorkManagers.Add(planet.planet.star, solarSystemWorkManager);
            }

            solarSystemWorkManager.AddPlanet(planetWorkManager);
            _rootWorkNode?.Dispose();
            _rootWorkNode = null;
        }

        for (int i = 0; i < _solarSystemWorkManagers.Count; i++)
        {
            if (_solarSystemWorkManagers[i].UpdateSolarSystemWork(parallelism))
            {
                _rootWorkNode?.Dispose();
                _rootWorkNode = null;
            }
        }

        for (int i = 0; i < dysonSpheres.Length; i++)
        {
            DysonSphere? dysonSphere = dysonSpheres[i];
            if (dysonSphere == null)
            {
                continue;
            }

            if (!_assignedSpheres.Add(dysonSphere))
            {
                continue;
            }

            if (!_starToWorkManagers.TryGetValue(dysonSphere.starData, out SolarSystemWorkManager? solarSystemWorkManager))
            {
                solarSystemWorkManager = new SolarSystemWorkManager();
                _solarSystemWorkManagers.Add(solarSystemWorkManager);
                _starToWorkManagers.Add(dysonSphere.starData, solarSystemWorkManager);

            }

            _assignedSpheres.Add(dysonSphere);
            solarSystemWorkManager.AddDysonSphere(dysonSphere);
            _rootWorkNode?.Dispose();
            _rootWorkNode = null;
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

            WeaverFixes.Logger.LogMessage($"Star cluster size: {_solarSystemWorkNodes.Count}");
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

    public IEnumerable<IWorkChunk> GetAllWorkChunks()
    {
        if (_rootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _rootWorkNode.GetAllWorkChunks();
    }

    public void Dispose()
    {

    }
}
