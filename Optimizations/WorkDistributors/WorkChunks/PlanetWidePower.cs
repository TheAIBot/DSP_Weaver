using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;
    private WorkStep? _workStep;

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
        _optimizedPlanet.PowerStep(time);
        DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
    }

    public void TieToWorkStep(WorkStep workStep)
    {
        _workStep = workStep;
    }

    public bool Complete()
    {
        if (_workStep == null)
        {
            throw new InvalidOperationException("No work step was assigned.");
        }

        return _workStep.CompleteWorkChunk();
    }

    public void CompleteStep()
    {
        if (_workStep == null)
        {
            throw new InvalidOperationException("No work step was assigned.");
        }

        _workStep.CompleteStep();
    }
}
