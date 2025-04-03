using System;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal interface IInserterExecutor
{
    int inserterCount { get; }

    void Initialize(PlanetFactory planet, OptimizedPlanet optimizedPlanet, Func<InserterComponent, bool> inserterSelector, OptimizedPowerSystemInserterBuilder optimizedPowerSystemInserterBuilder);

    int GetUnoptimizedInserterIndex(int optimizedInserterIndex);

    void GameTickInserters(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, int _start, int _end);

    void UpdatePower(OptimizedPlanet optimizedPlanet,
                     int[] inserterPowerConsumerIndexes,
                     PowerConsumerType[] powerConsumerTypes,
                     long[] thisThreadNetworkPowerConsumption,
                     int _usedThreadCnt,
                     int _curThreadIdx,
                     int _minimumMissionCnt);
}

internal interface IInserterExecutor<T> : IInserterExecutor
    where T : struct, IInserter<T>
{
    T Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade);
}
