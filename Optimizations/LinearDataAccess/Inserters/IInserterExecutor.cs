using System;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal interface IInserterExecutor
{
    int inserterCount { get; }

    void Initialize(PlanetFactory planet, Func<InserterComponent, bool> inserterSelector);

    int GetUnoptimizedInserterIndex(int optimizedInserterIndex);

    void GameTickInserters(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, int _start, int _end);
}

internal interface IInserterExecutor<T> : IInserterExecutor
    where T : IInserter<T>
{
    T Create(ref readonly InserterComponent inserter, int grade);
}
