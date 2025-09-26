using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class SolarSystemWorkManager
{
    private readonly List<PlanetWorkManager> _planetWorkManager = [];
    private readonly List<IWorkNode> _planetWorkNodes = [];
    private IWorkNode? _workNodes = null;

    public void AddPlanet(PlanetWorkManager planetWorkManager)
    {
        _planetWorkManager.Add(planetWorkManager);
    }

    public bool UpdateSolarSystemWork(int parallelism)
    {
        for (int i = 0; i < _planetWorkManager.Count; i++)
        {
            if (_planetWorkManager[i].UpdatePlanetWork(parallelism))
            {
                _workNodes?.Dispose();
                _workNodes = null;
            }
        }

        if (_workNodes == null)
        {
            _planetWorkNodes.Clear();
            for (int i = 0; i < _planetWorkManager.Count; i++)
            {
                if (_planetWorkManager[i].TryGetPlanetWork(out IWorkNode? planetWorkNode))
                {
                    _planetWorkNodes.Add(planetWorkNode);
                }
            }

            if (_planetWorkNodes.Count == 0)
            {
                _workNodes = new NoWorkNode();
                return true;
            }

            WeaverFixes.Logger.LogMessage($"solar system size: {_planetWorkNodes.Count}");
            _workNodes = new WorkNode([_planetWorkNodes.ToArray()]);
            return true;
        }

        return false;
    }

    public bool TryGetSolarSystemWork([NotNullWhen(true)] out IWorkNode? solarSystemWorkNode)
    {
        if (_workNodes == null)
        {
            solarSystemWorkNode = null;
            return false;
        }

        if (_workNodes is NoWorkNode)
        {
            solarSystemWorkNode = null;
            return false;
        }

        solarSystemWorkNode = _workNodes;
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
    private IWorkNode? _workNode = null;

    public PlanetWorkManager(IOptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public bool UpdatePlanetWork(int parallelism)
    {
        IWorkNode updatedWorkNode = _optimizedPlanet.GetMultithreadedWork(parallelism);
        if (_workNode != updatedWorkNode)
        {
            _workNode?.Dispose();
            _workNode = updatedWorkNode;
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
