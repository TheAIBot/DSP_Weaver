using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

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
