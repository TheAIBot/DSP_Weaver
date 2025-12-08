using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PlanetWideDigitalSystem : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;

    public PlanetWideDigitalSystem(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _optimizedPlanet.DigitalSystemStep(workerIndex);
    }
}