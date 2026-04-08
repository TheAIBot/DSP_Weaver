using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class SolarSystemWorkManager
{
    private readonly List<PlanetWorkManager> _planetWorkManager = [];
    private readonly List<IWorkNode> _planetWorkNodes = [];
    private readonly List<IWorkChunk> _planetRayReceiverEnergyRequests = [];
    private DysonSphere? _dysonSphere;
    private List<DysonSphereAttach> _dysonSphereAttachChunks = [];
    private IWorkNode? _dysonWorkNode;
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

        _dysonSphere = dysonSphere;
        _dysonWorkNode = new SingleWorkLeaf(new SolarSystemDysonSphereWorkChunk(dysonSphere));
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
            _planetRayReceiverEnergyRequests.Clear();
            for (int i = 0; i < _planetWorkManager.Count; i++)
            {
                if (_planetWorkManager[i].TryGetPlanetWork(out IWorkNode? planetWorkNode))
                {
                    _planetWorkNodes.Add(planetWorkNode);

                    if (_planetWorkManager[i].OptimizedPlanet is OptimizedTerrestrialPlanet terrestrialPlanet)
                    {
                        _planetRayReceiverEnergyRequests.Add(new DysonSpherePowerRequest(terrestrialPlanet));
                    }
                }
            }
        }

        if (_dysonSphere != null)
        {
            int dysonSphereAttachChunkCount = GetWorkChunkCountForDysonSphereAttach(_dysonSphere, parallelism);
            if (dysonSphereAttachChunkCount != _dysonSphereAttachChunks.Count)
            {
                _solarSystemWorkNode?.Dispose();
                _solarSystemWorkNode = null;
                _dysonSphereAttachChunks.Clear();
                _dysonSphereAttachChunks.AddRange(GetWorkChunksForDysonSphereAttach(_dysonSphere, parallelism));
            }
        }


        if (_solarSystemWorkNode == null)
        {
            List<IWorkNode[]> solarSystemWork = [];
            if (_dysonWorkNode != null)
            {
                solarSystemWork.Add([_dysonWorkNode]);
            }

            if (_planetRayReceiverEnergyRequests.Count > 0)
            {
                solarSystemWork.Add(_planetRayReceiverEnergyRequests.Select(x => new SingleWorkLeaf(x)).ToArray());
            }

            if (_planetWorkNodes.Count > 0)
            {
                solarSystemWork.Add(_planetWorkNodes.ToArray());
            }

            if (_dysonSphere != null)
            {
                solarSystemWork.Add([new SingleWorkLeaf(new DysonSphereGameUpdate(_dysonSphere))]);
                solarSystemWork.Add(_dysonSphereAttachChunks.Select(x => new SingleWorkLeaf(x)).ToArray());
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

    private static IEnumerable<DysonSphereAttach> GetWorkChunksForDysonSphereAttach(DysonSphere dysonSphere, int maxParallelism)
    {
        int workChunkCount = GetWorkChunkCountForDysonSphereAttach(dysonSphere, maxParallelism);
        for (int i = 0; i < workChunkCount; i++)
        {
            yield return new DysonSphereAttach(dysonSphere, i, workChunkCount);
        }
    }

    private static int GetWorkChunkCountForDysonSphereAttach(DysonSphere dysonSphere, int maxParallelism)
    {
        const int minimumWorkPerCore = 2_000;
        int workChunkCount = (Math.Max(dysonSphere.swarm.bulletCursor, dysonSphere.rocketCursor) + (minimumWorkPerCore - 1)) / minimumWorkPerCore;
        workChunkCount = Math.Min(workChunkCount, maxParallelism);
        workChunkCount = Math.Max(workChunkCount, 1); // Always at least one work chunk for any recently launched sails/rockets
        return workChunkCount;
    }
}
