using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class SolarSystemDysonSphereWorkChunk : IWorkChunk
{
    private readonly DysonSphere _dysonSphere;

    public SolarSystemDysonSphereWorkChunk(DysonSphere dysonSphere)
    {
        _dysonSphere = dysonSphere; 
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.DysonSphere, workerIndex);
        DysonSphereBeforeGameTick(_dysonSphere, workerIndex, time);
        DeepProfiler.EndSample(DPEntry.DysonSphere, workerIndex);
    }

    // So i have to copy the code here because i must replace the DeepProfiler.(Begin/End)Sample calls
    // so they point to the correct thread.
    // Alternative way to solve it would be assign workerIndex to a thread local variable and then
    // replace the DeepProfiler methods with a new method that retrieves the workerIndex from thread local
    // storage. A lot more difficult but at least i don't have to copy all the code.
    private static void DysonSphereBeforeGameTick(DysonSphere dysonSphere, int workerIndex, long times)
    {

        dysonSphere.energyReqCurrentTick = 0L;
        dysonSphere.energyGenCurrentTick = 0L;
        dysonSphere.energyGenOriginalCurrentTick = 0L;
        dysonSphere.swarm.energyGenCurrentTick = dysonSphere.swarm.sailCount * dysonSphere.energyGenPerSail;
        dysonSphere.energyGenCurrentTick += dysonSphere.swarm.energyGenCurrentTick;
        dysonSphere.grossRadius = dysonSphere.swarm.grossRadius;
        DeepProfiler.BeginSample(DPEntry.DysonShell, workerIndex);
        int epn = (int)dysonSphere.energyGenPerNode;
        int epf = (int)dysonSphere.energyGenPerFrame;
        for (int i = 0; i < 10; i++)
        {
            DysonSphereLayer dysonSphereLayer = dysonSphere.layersSorted[i];
            if (dysonSphereLayer == null)
            {
                continue;
            }
            if (dysonSphereLayer.grossRadius > dysonSphere.grossRadius)
            {
                dysonSphere.grossRadius = dysonSphereLayer.grossRadius;
            }
            dysonSphereLayer.energyGenCurrentTick = 0L;
            long num = 0L;
            DysonNode[] nodePool = dysonSphereLayer.nodePool;
            DysonShell[] shellPool = dysonSphereLayer.shellPool;
            for (int j = 1; j < dysonSphereLayer.nodeCursor; j++)
            {
                if (nodePool[j] != null && nodePool[j].id == j)
                {
                    num += nodePool[j].EnergyGenCurrentTick(epn, epf);
                }
            }
            for (int k = 1; k < dysonSphereLayer.shellCursor; k++)
            {
                if (shellPool[k] != null && shellPool[k].id == k)
                {
                    num += shellPool[k].cellPoint * dysonSphere.energyGenPerShell;
                }
            }
            dysonSphereLayer.energyGenCurrentTick = num;
            dysonSphere.energyGenCurrentTick += num;
        }
        DeepProfiler.EndSample(DPEntry.DysonShell, workerIndex);
        dysonSphere.energyGenOriginalCurrentTick = dysonSphere.energyGenCurrentTick;
        dysonSphere.energyGenCurrentTick = (long)((double)dysonSphere.energyGenCurrentTick * dysonSphere.energyDFHivesDebuffCoef);
    }
}

internal sealed class SolarSystemWorkManager
{
    private readonly List<PlanetWorkManager> _planetWorkManager = [];
    private readonly List<IWorkNode> _planetWorkNodes = [];
    private IWorkNode? _dysonWorkNode = null;
    private IWorkNode? _solarSystemWorkNode = null;

    public void AddPlanet(PlanetWorkManager planetWorkManager)
    {
        _planetWorkManager.Add(planetWorkManager);
        _solarSystemWorkNode?.Dispose();
        _solarSystemWorkNode = null;
    }

    public void AddDysonSphere(DysonSphere dysonSphere)
    {
        if (_dysonWorkNode != null)
        {
            throw new InvalidOperationException("Attempted to add dyson sphere but solar system already has a dyson sphere.");
        }

        _dysonWorkNode = new WorkLeaf([ new SolarSystemDysonSphereWorkChunk(dysonSphere)]);
        _solarSystemWorkNode?.Dispose();
        _solarSystemWorkNode = null;
    }

    public bool UpdateSolarSystemWork(int parallelism)
    {
        for (int i = 0; i < _planetWorkManager.Count; i++)
        {
            if (_planetWorkManager[i].UpdatePlanetWork(parallelism))
            {
                _solarSystemWorkNode?.Dispose();
                _solarSystemWorkNode = null;
            }
        }

        // Whatever lets just update this every time the solar system work changes.
        if (_solarSystemWorkNode == null)
        {
            _planetWorkNodes.Clear();
            for (int i = 0; i < _planetWorkManager.Count; i++)
            {
                if (_planetWorkManager[i].TryGetPlanetWork(out IWorkNode? planetWorkNode))
                {
                    _planetWorkNodes.Add(planetWorkNode);
                }
            }
        }

        if (_solarSystemWorkNode == null)
        {
            List<IWorkNode[]> solarSystemWork = [];
            if (_dysonWorkNode != null)
            {
                solarSystemWork.Add([_dysonWorkNode]);
            }

            if (_planetWorkNodes.Count > 0)
            {
                solarSystemWork.Add(_planetWorkNodes.ToArray());
            }

            if (solarSystemWork.Count == 0)
            {
                _solarSystemWorkNode = new NoWorkNode();
            }
            else
            {
                _solarSystemWorkNode = new WorkNode(solarSystemWork.ToArray());
            }
            return true;
        }

        return false;
    }

    public bool TryGetSolarSystemWork([NotNullWhen(true)] out IWorkNode? solarSystemWorkNode)
    {
        if (_solarSystemWorkNode == null)
        {
            solarSystemWorkNode = null;
            return false;
        }

        if (_solarSystemWorkNode is NoWorkNode)
        {
            solarSystemWorkNode = null;
            return false;
        }

        solarSystemWorkNode = _solarSystemWorkNode;
        return true;
    }

    public IEnumerable<PlanetWorkStatistics> GetPlanetWorkStatistics()
    {
        foreach (var planetWorkManager in _planetWorkManager)
        {
            PlanetWorkStatistics? planetStatistics = planetWorkManager.GetPlanetWorkStatistics();
            if (planetStatistics == null)
            {
                continue;
            }

            yield return planetStatistics.Value;
        }
    }
}

internal sealed class PlanetWorkManager
{
    private readonly IOptimizedPlanet _optimizedPlanet;
    private readonly IWorkNode _prePlanetFactoryWork;
    private IWorkNode? _workNode = null;

    public PlanetWorkManager(GameLogic gameLogic, PlanetFactory planet, IOptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
        _prePlanetFactoryWork = new WorkLeaf([new PrePlanetFactorySteps(gameLogic, planet)]);
    }

    public bool UpdatePlanetWork(int parallelism)
    {
        IWorkNode updatedWorkNode = _optimizedPlanet.GetMultithreadedWork(parallelism);
        if (_workNode != updatedWorkNode)
        {
            _workNode?.Dispose();
            if (updatedWorkNode is NoWorkNode)
            {
                _workNode = updatedWorkNode;
            }
            else
            {
                _workNode = new WorkNode([[_prePlanetFactoryWork], [updatedWorkNode]]);
            }

            return true;
        }

        return false;
    }

    public bool TryGetPlanetWork([NotNullWhen(true)] out IWorkNode? planetWorkNode)
    {
        if (_workNode == null)
        {
            planetWorkNode = null;
            return false;
        }

        if (_workNode is NoWorkNode)
        {
            planetWorkNode = null;
            return false;
        }

        planetWorkNode = _workNode;
        return true;
    }

    public PlanetWorkStatistics? GetPlanetWorkStatistics()
    {
        if (_workNode == null)
        {
            return null;
        }

        if (_workNode is NoWorkNode)
        {
            return null;
        }

        int totalWorkChunks = _workNode.GetWorkChunkCount();
        return new PlanetWorkStatistics(-1, totalWorkChunks);
    }
}
