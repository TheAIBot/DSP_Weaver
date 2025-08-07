using System;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;
    private readonly SubFactoryPowerConsumption _subFactoryPowerConsumption;
    private WorkStep? _workStep;

    public SubFactoryGameTick(OptimizedSubFactory subFactory, SubFactoryPowerConsumption subFactoryPowerConsumption)
    {
        _subFactory = subFactory;
        _subFactoryPowerConsumption = subFactoryPowerConsumption;
    }

    public void Execute(WorkerThread workerThread, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _subFactory.GameTick(workerThread, time, _subFactoryPowerConsumption);
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
