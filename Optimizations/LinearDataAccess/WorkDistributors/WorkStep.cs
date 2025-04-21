using System;
using System.Threading;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class WorkStep : IDisposable
{
    private readonly ManualResetEventSlim _waitForCompletion;
    private readonly IWorkChunk[] _workChunks;
    private int _scheduledCount;
    private int _completedCount;

    public WorkStep(IWorkChunk[] workChunks)
    {
        _waitForCompletion = new(false);
        _workChunks = workChunks;
        _scheduledCount = 0;
        _completedCount = 0;

        foreach (IWorkChunk workChunk in workChunks)
        {
            workChunk.TieToWorkStep(this);
        }
    }

    public IWorkChunk TryGetWork(out bool canNoLongerProvideWork)
    {
        if (_scheduledCount >= _workChunks.Length)
        {
            canNoLongerProvideWork = true;
            return null;
        }

        int workChunkIndex = Interlocked.Increment(ref _scheduledCount) - 1;
        if (workChunkIndex >= _workChunks.Length)
        {
            canNoLongerProvideWork = true;
            return null;
        }

        canNoLongerProvideWork = false;
        return _workChunks[workChunkIndex];
    }

    public void WaitForCompletion()
    {
        _waitForCompletion.Wait();
    }

    public bool CompleteWorkChunk()
    {
        int completedWorkChunks = Interlocked.Increment(ref _completedCount);
        if (completedWorkChunks > _workChunks.Length)
        {
            throw new InvalidOperationException("");
        }

        return completedWorkChunks == _workChunks.Length;
    }

    public void CompleteStep()
    {
        _waitForCompletion.Set();
    }

    public void Reset()
    {
        _waitForCompletion.Reset();
        _scheduledCount = 0;
        _completedCount = 0;
    }

    public void Dispose()
    {
        _waitForCompletion.Dispose();
    }
}
