using UnityEngine;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class SolarSystemDysonSphereWorkChunk : IWorkChunk
{
    private readonly DysonSphere _dysonSphere;

    public SolarSystemDysonSphereWorkChunk(DysonSphere dysonSphere)
    {
        _dysonSphere = dysonSphere; 
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.DysonSphere, workerIndex);
        _dysonSphere.BeforeGameTick(time);
        DeepProfiler.EndSample(DPEntry.DysonSphere, workerIndex);
    }
}
