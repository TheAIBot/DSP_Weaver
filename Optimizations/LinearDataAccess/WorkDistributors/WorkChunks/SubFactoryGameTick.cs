using System;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;
    private WorkStep _workStep;

    public SubFactoryGameTick(OptimizedSubFactory subFactory)
    {
        _subFactory = subFactory;
    }

    public void Execute(WorkerTimings workerTimings, long time)
    {
        _subFactory.GameTick(workerTimings, time);
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
