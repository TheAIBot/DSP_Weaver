using System;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedPlanet _optimizedPlanet;
    private WorkStep _workStep;

    public PlanetWidePower(OptimizedPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.PowerStep(time);
        workerTimings.RecordTime(WorkType.Power);
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
