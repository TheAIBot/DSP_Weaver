using System.Diagnostics.CodeAnalysis;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class PlanetWorkManager
{
    private PlanetFactory _planet;
    private readonly IOptimizedPlanet _optimizedPlanet;
    private readonly SingleWorkLeaf _prePlanetFactoryWorkNode;
    private SingleWorkLeaf[]? _enemyGroundCombatWorkNodes;
    private IWorkNode? _factoryWorkNode = null;
    private IWorkNode? _planetWorkNode = null;

    public IOptimizedPlanet OptimizedPlanet => _optimizedPlanet;

    public PlanetWorkManager(GameLogic gameLogic, PlanetFactory planet, IOptimizedPlanet optimizedPlanet)
    {
        _planet = planet;
        _optimizedPlanet = optimizedPlanet;
        _prePlanetFactoryWorkNode = new SingleWorkLeaf(new PrePlanetFactorySteps(gameLogic, planet));
    }

    public bool UpdatePlanetWork(int parallelism)
    {
        bool needsToUpdate = false;
        int enemyGroundCombatParallelism = EnemyGroundCombat.GetParallelCount(_planet, parallelism);
        if (enemyGroundCombatParallelism > 0 &&
            (_enemyGroundCombatWorkNodes == null ||
             _enemyGroundCombatWorkNodes.Length != enemyGroundCombatParallelism))
        {
            if (_enemyGroundCombatWorkNodes != null)
            {
                for (int i = 0; i < _enemyGroundCombatWorkNodes.Length; i++)
                {
                    _enemyGroundCombatWorkNodes[i].Dispose();
                }
            }

            _enemyGroundCombatWorkNodes = new SingleWorkLeaf[enemyGroundCombatParallelism];
            for (int i = 0; i < _enemyGroundCombatWorkNodes.Length; i++)
            {
                _enemyGroundCombatWorkNodes[i] = new SingleWorkLeaf(new EnemyGroundCombat(_planet, i, parallelism));
            }

            needsToUpdate = true;
        }
        else if (_enemyGroundCombatWorkNodes != null &&
            enemyGroundCombatParallelism == 0)
        {
            for (int i = 0; i < _enemyGroundCombatWorkNodes.Length; i++)
            {
                _enemyGroundCombatWorkNodes[i].Dispose();
            }

            _enemyGroundCombatWorkNodes = null;
            needsToUpdate = true;
        }

        IWorkNode updatedFactoryWorkNode = _optimizedPlanet.GetMultithreadedWork(parallelism);
        if (_factoryWorkNode != updatedFactoryWorkNode)
        {
            _factoryWorkNode?.Dispose();
            _factoryWorkNode = updatedFactoryWorkNode;
            needsToUpdate = true;
        }

        if (needsToUpdate)
        {
            _planetWorkNode?.Dispose();

            if (updatedFactoryWorkNode is NoWorkNode && _enemyGroundCombatWorkNodes == null)
            {
                _planetWorkNode = _prePlanetFactoryWorkNode;
            }
            else if (updatedFactoryWorkNode is NoWorkNode && _enemyGroundCombatWorkNodes != null)
            {
                _planetWorkNode = new WorkNode([[_prePlanetFactoryWorkNode], _enemyGroundCombatWorkNodes]);
            }
            else
            {
                if (_enemyGroundCombatWorkNodes == null)
                {
                    _planetWorkNode = new WorkNode([[_prePlanetFactoryWorkNode], [_factoryWorkNode]]);
                }
                else
                {
                    _planetWorkNode = new WorkNode([[_prePlanetFactoryWorkNode], _enemyGroundCombatWorkNodes, [_factoryWorkNode]]);
                }
            }
        }

        return needsToUpdate;
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
