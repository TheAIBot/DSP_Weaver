using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PlanetWideTransport : IWorkChunk
{
    private readonly IOptimizedPlanet _optimizedPlanet;

    public PlanetWideTransport(IOptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _optimizedPlanet.TransportGameTick(workerIndex, time, playerPosition);
    }
}
