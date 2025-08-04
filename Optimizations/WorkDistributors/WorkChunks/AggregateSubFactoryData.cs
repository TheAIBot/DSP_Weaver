using System;
using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PostSubFactoryStep : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;
    private WorkStep? _workStep;

    public PostSubFactoryStep(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        workerTimings.StartTimer();
        _optimizedPlanet.TransportGameTick(time, playerPosition);
        workerTimings.RecordTime(WorkType.TransportData);

        workerTimings.StartTimer();
        _optimizedPlanet.DigitalSystemStep();
        workerTimings.RecordTime(WorkType.Digital);

        //workerTimings.StartTimer();
        _optimizedPlanet.AggregateSubFactoryDataStep(time);
        //workerTimings.RecordTime(WorkType.Statistics);
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
