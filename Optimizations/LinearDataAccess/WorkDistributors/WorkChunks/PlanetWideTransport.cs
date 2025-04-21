using System;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class PlanetWideTransport : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWideTransport(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.TransportStep(time);
        workerTimings.RecordTime(WorkType.TransportData);
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
