using System;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class EntirePlanet : IWorkChunk
{
    private readonly PlanetWidePower _planetWidePower;
    private readonly SubFactoryGameTick _subFactory;
    private readonly PostSubFactoryStep _postSubFactoryStep;
    private WorkStep? _workStep;

    public EntirePlanet(OptimizedTerrestrialPlanet optimizedPlanet, OptimizedSubFactory subFactory, SubFactoryPowerConsumption subFactoryPowerConsumption)
    {
        _planetWidePower = new PlanetWidePower(optimizedPlanet);
        _subFactory = new SubFactoryGameTick(subFactory, subFactoryPowerConsumption);
        _postSubFactoryStep = new PostSubFactoryStep(optimizedPlanet);
    }

    public void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _planetWidePower.Execute(workerTimings, workerThreadExecutor, singleThreadedCodeLock, localPlanet, time, playerPosition);
        _subFactory.Execute(workerTimings, workerThreadExecutor, singleThreadedCodeLock, localPlanet, time, playerPosition);
        _postSubFactoryStep.Execute(workerTimings, workerThreadExecutor, singleThreadedCodeLock, localPlanet, time, playerPosition);
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
