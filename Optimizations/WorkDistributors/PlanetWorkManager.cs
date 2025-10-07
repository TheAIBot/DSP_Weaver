using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class PlanetWorkManager
{
    private readonly IOptimizedPlanet _optimizedPlanet;
    private readonly IWorkNode _prePlanetFactoryWork;
    private IWorkNode? _factoryWorkNode = null;
    private IWorkNode? _planetWorkNode = null;

    public PlanetWorkManager(GameLogic gameLogic, PlanetFactory planet, IOptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
        _prePlanetFactoryWork = new WorkLeaf([new PrePlanetFactorySteps(gameLogic, planet)]);
    }

    public bool UpdatePlanetWork(int parallelism)
    {
        IWorkNode updatedFactoryWorkNode = _optimizedPlanet.GetMultithreadedWork(parallelism);
        if (_factoryWorkNode != updatedFactoryWorkNode)
        {
            _factoryWorkNode?.Dispose();
            _planetWorkNode?.Dispose();
            _factoryWorkNode = updatedFactoryWorkNode;
            if (updatedFactoryWorkNode is NoWorkNode)
            {
                _planetWorkNode = _prePlanetFactoryWork;
            }
            else
            {
                _planetWorkNode = new WorkNode([[_prePlanetFactoryWork], [_factoryWorkNode]]);
            }

            return true;
        }

        return false;
    }

    public bool TryGetPlanetWork([NotNullWhen(true)] out IWorkNode? planetWorkNode)
    {
        if (_planetWorkNode == null)
        {
            planetWorkNode = null;
            return false;
        }

        if (_planetWorkNode is NoWorkNode)
        {
            planetWorkNode = null;
            return false;
        }

        planetWorkNode = _planetWorkNode;
        return true;
    }

    public PlanetWorkStatistics? GetPlanetWorkStatistics()
    {
        if (_planetWorkNode == null)
        {
            return null;
        }

        if (_planetWorkNode is NoWorkNode)
        {
            return null;
        }

        int totalWorkChunks = _planetWorkNode.GetWorkChunkCount();
        return new PlanetWorkStatistics(-1, totalWorkChunks);
    }
}
