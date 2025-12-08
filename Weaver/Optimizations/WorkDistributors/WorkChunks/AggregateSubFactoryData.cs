using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PostSubFactoryStep : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;

    public PostSubFactoryStep(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _optimizedPlanet.TransportGameTick(workerIndex, time, playerPosition);
        _optimizedPlanet.DigitalSystemStep(workerIndex);
        _optimizedPlanet.AggregateSubFactoryDataStep(workerIndex, time);
    }
}
