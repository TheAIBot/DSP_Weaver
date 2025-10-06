using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Weaver.Optimizations.WorkDistributors;

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
