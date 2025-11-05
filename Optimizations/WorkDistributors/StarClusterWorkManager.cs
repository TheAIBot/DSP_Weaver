using System;
using System.Collections.Generic;
using System.Linq;
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
    private RootWorkNode? _factorySimulationRootWorkNode;
    private RootWorkNode? _defenseSystemTurretRootWorkNode;
    private RootWorkNode? _spaceHashRootWorkNode;

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
            _factorySimulationRootWorkNode?.Dispose();
            _factorySimulationRootWorkNode = null;
            _defenseSystemTurretRootWorkNode?.Dispose();
            _defenseSystemTurretRootWorkNode = null;
            _spaceHashRootWorkNode?.Dispose();
            _spaceHashRootWorkNode = null;
        }

        for (int i = 0; i < _solarSystemWorkManagers.Count; i++)
        {
            if (_solarSystemWorkManagers[i].UpdateSolarSystemWork(parallelism))
            {
                _factorySimulationRootWorkNode?.Dispose();
                _factorySimulationRootWorkNode = null;
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
            _factorySimulationRootWorkNode?.Dispose();
            _factorySimulationRootWorkNode = null;
        }

        if (_factorySimulationRootWorkNode == null)
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
                _factorySimulationRootWorkNode = new RootWorkNode(new DummyWorkDoneImmediatelyNode());
            }
            else
            {
                WeaverFixes.Logger.LogMessage($"Star cluster size: {_solarSystemWorkNodes.Count}");
                _factorySimulationRootWorkNode = new RootWorkNode(new WorkNode([_solarSystemWorkNodes.ToArray()]));
            }
        }

        if (_defenseSystemTurretRootWorkNode == null)
        {
            List<DefenseSystemTurret> _defenseSystemTurretWork = [];
            for (int i = 0; i < allPlanets.Length; i++)
            {
                PlanetFactory? planet = allPlanets[i];
                if (planet == null)
                {
                    continue;
                }

                _defenseSystemTurretWork.Add(new DefenseSystemTurret(planet.defenseSystem));
            }

            _defenseSystemTurretRootWorkNode = new RootWorkNode(new WorkLeaf(_defenseSystemTurretWork.ToArray()));
        }

        if (_spaceHashRootWorkNode == null)
        {
            Dictionary<StarData, List<DFSDynamicHashSystem>> starToSpaceHash = [];
            for (int i = 0; i < allPlanets.Length; i++)
            {
                PlanetFactory? planet = allPlanets[i];
                if (planet == null)
                {
                    continue;
                }

                if (!starToSpaceHash.TryGetValue(planet.planet.star, out List<DFSDynamicHashSystem>? starSystemSpaceHashes))
                {
                    starSystemSpaceHashes = [];
                    starToSpaceHash.Add(planet.planet.star, starSystemSpaceHashes);
                }

                starSystemSpaceHashes.Add(planet.spaceHashSystemDynamic);
            }

            _spaceHashRootWorkNode = new RootWorkNode(new WorkLeaf(starToSpaceHash.Values.Select(x => new StarSystemSpaceHashWork(x)).ToArray()));
        }
    }

    public RootWorkNode GetFactorySimulationRootWorkNode()
    {
        if (_factorySimulationRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _factorySimulationRootWorkNode;
    }

    public RootWorkNode GetDefenseSystemTurretRootWorkNode()
    {
        if (_defenseSystemTurretRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _defenseSystemTurretRootWorkNode;
    }

    public RootWorkNode GetSpaceHashRootWorkNode()
    {
        if (_spaceHashRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _spaceHashRootWorkNode;
    }

    public void Reset()
    {
        if (_factorySimulationRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }
        _factorySimulationRootWorkNode.Reset();

        if (_defenseSystemTurretRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }
        _defenseSystemTurretRootWorkNode.Reset();

        if (_spaceHashRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }
        _spaceHashRootWorkNode.Reset();
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
        if (_factorySimulationRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _factorySimulationRootWorkNode.GetAllWorkChunks();
    }

    public void Dispose()
    {

    }
}
