using System;
using System.Linq;

namespace Weaver.FatoryGraphs;

internal sealed class ExecutableGraph<TExecuteAction>
    where TExecuteAction : IExecutableGraphAction
{
    private readonly PlanetFactory _planet;
    private readonly Graph _graph;
    private readonly EntityType _entityType;
    private readonly TExecuteAction _action;
    private int[] _indexes = null;

    public ExecutableGraph(PlanetFactory planet, Graph graph, EntityType entityType, TExecuteAction action)
    {
        _planet = planet;
        _graph = graph;
        _entityType = entityType;
        _action = action;
    }

    public void Prepare()
    {
        if (_indexes != null)
        {
            return;
        }

        _indexes = _graph.GetAllNodes()
                         .Where(x => x.EntityTypeIndex.EntityType == _entityType)
                         .Select(x => x.EntityTypeIndex.Index)
                         .OrderBy(x => x)
                         .ToArray();

        //WeaverFixes.Logger.LogInfo($"{_indexes.Length} indexes");
    }

    /// <summary>
    /// Original from FactorySystem.GameTickInserters
    /// </summary>
    public void Execute(long time)
    {
        if (_indexes == null)
        {
            throw new InvalidOperationException($"{nameof(_indexes)} is null.");
        }


        _action.Execute(time, _planet, _indexes);
    }
}
