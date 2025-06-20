using System;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;
    private readonly OptimizedPowerSystem _optimizedPowerSystem;
    private WorkStep? _workStep;

    public SubFactoryGameTick(OptimizedSubFactory subFactory, OptimizedPowerSystem optimizedPowerSystem)
    {
        _subFactory = subFactory;
        _optimizedPowerSystem = optimizedPowerSystem;
    }

    public void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _subFactory.GameTick(workerTimings, time, _optimizedPowerSystem);
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
