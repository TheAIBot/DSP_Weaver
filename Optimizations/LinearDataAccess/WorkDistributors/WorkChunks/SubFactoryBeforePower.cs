using System;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class SubFactoryBeforePower : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly OptimizedPowerSystem _optimizedPowerSystem;
    private readonly OptimizedSubFactory _subFactory;
    private WorkStep? _workStep;

    public SubFactoryBeforePower(PlanetFactory planet,
                                 OptimizedPowerSystem optimizedPowerSystem,
                                 OptimizedSubFactory subFactory)
    {
        _planet = planet;
        _optimizedPowerSystem = optimizedPowerSystem;
        _subFactory = subFactory;
    }

    public void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        workerTimings.StartTimer();
        _optimizedPowerSystem.BeforePower(_planet, _subFactory);
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
