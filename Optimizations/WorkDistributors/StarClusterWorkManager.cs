using System;
using System.Collections.Generic;
using System.Linq;
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
    private RootWorkNode? _dysonSphereAttachRootWorkNode;

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
            _dysonSphereAttachRootWorkNode?.Dispose();
            _dysonSphereAttachRootWorkNode = null;
        }

        // Dyson attach work also needs to be refreshed at a regular internal
        // because the launch of sails and rockets will change over time but that
        // does not invalidate the work node by itself
        const int ticksBetweenDysonAttachWorkForcedUpdate = 240;
        if (GameMain.gameTick % ticksBetweenDysonAttachWorkForcedUpdate == 0)
        {
            _dysonSphereAttachRootWorkNode?.Dispose();
            _dysonSphereAttachRootWorkNode = null;
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

        if (_dysonSphereAttachRootWorkNode == null)
        {
            var dysonShereAttachWorkChunks = _assignedSpheres.SelectMany(x => GetWorkChunksForDysonSphereAttach(x, parallelism)).ToArray();
            if (dysonShereAttachWorkChunks.Length == 0)
            {
                _dysonSphereAttachRootWorkNode = new RootWorkNode(new DummyWorkDoneImmediatelyNode());
            }
            else
            {
                _dysonSphereAttachRootWorkNode = new RootWorkNode(new WorkLeaf(dysonShereAttachWorkChunks));
            }

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

    public RootWorkNode GetDysonSphereAttachRootWorkNode()
    {
        if (_dysonSphereAttachRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }

        return _dysonSphereAttachRootWorkNode;
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

        if (_dysonSphereAttachRootWorkNode == null)
        {
            throw new InvalidOperationException();
        }
        _dysonSphereAttachRootWorkNode.Reset();
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

    private static IEnumerable<IWorkChunk> GetWorkChunksForDysonSphereAttach(DysonSphere dysonSphere, int maxParallelism)
    {
        const int minimumWorkPerCore = 2_000;
        int workChunkCount = (Math.Max(dysonSphere.swarm.bulletCursor, dysonSphere.rocketCursor) + (minimumWorkPerCore - 1)) / minimumWorkPerCore;
        workChunkCount = Math.Min(workChunkCount, maxParallelism);
        workChunkCount = Math.Max(workChunkCount, 1); // Always at least one work chunk for any recently launched sails/rockets
        for (int i = 0; i < workChunkCount; i++)
        {
            yield return new DysonSphereAttach(dysonSphere, i, workChunkCount);
        }
    }
}
