using System;
using System.Collections.Generic;
using System.Linq;

namespace Weaver.Optimizations.LinearDataAccess.Stations;

internal sealed class PlanetWideStationExecutorBuilder
{
    private readonly List<(OptimizedStation Station, int NetworkIndex)> _optimizedStationWithNetworkIds = [];

    public void AddOptimizedStations(List<OptimizedStation> optimizedStations, List<int> networkIds)
    {
        if (optimizedStations.Count != networkIds.Count)
        {
            throw new ArgumentException($"{nameof(optimizedStations)} and {nameof(networkIds)} were not same length. {nameof(optimizedStations)}: {optimizedStations.Count},{nameof(networkIds)}: {networkIds.Count}");
        }

        for (int i = 0; i < optimizedStations.Count; i++)
        {
            _optimizedStationWithNetworkIds.Add((optimizedStations[i], networkIds[i]));
        }
    }

    public PlanetWideStationExecutor Build()
    {
        OptimizedStation[] optimizedStations = new OptimizedStation[_optimizedStationWithNetworkIds.Count];
        int[] networkIds = new int[_optimizedStationWithNetworkIds.Count];
        int index = 0;
        foreach (var optimizedStationWithNetworkId in _optimizedStationWithNetworkIds.OrderBy(x => x.Station.stationComponent.id))
        {
            optimizedStations[index] = optimizedStationWithNetworkId.Station;
            networkIds[index] = optimizedStationWithNetworkId.NetworkIndex;
            index++;
        }

        return new PlanetWideStationExecutor(optimizedStations, networkIds);
    }
}
