using UnityEngine;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class DysonSpherePowerRequest : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;

    public DysonSpherePowerRequest(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition)
    {
        _optimizedPlanet.RequestDysonSpherePower(workerIndex);
    }
}