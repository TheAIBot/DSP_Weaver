using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;

    public PlanetWidePower(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
        _optimizedPlanet.BeforePowerStep(time);
        DeepProfiler.EndSample(DPEntry.PowerConsumer, workerIndex);

        DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
        _optimizedPlanet.PowerStep(time, workerIndex);
        DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
    }
}
