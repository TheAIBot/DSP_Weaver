using System;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class PlanetWideBeforePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWideBeforePower(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.BeforePowerStep(time);
        workerTimings.RecordTime(WorkType.BeforePower);
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
