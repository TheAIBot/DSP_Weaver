using System;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PostSubFactoryStep : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;
    private WorkStep? _workStep;

    public PostSubFactoryStep(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerThread workerThread, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _optimizedPlanet.TransportGameTick(workerThread, time, playerPosition);
        _optimizedPlanet.DigitalSystemStep(workerThread);
        _optimizedPlanet.AggregateSubFactoryDataStep(workerThread, time);
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
