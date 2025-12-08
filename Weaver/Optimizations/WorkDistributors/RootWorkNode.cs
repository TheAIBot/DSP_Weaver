using System;
using System.Collections.Generic;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class RootWorkNode : IDisposable
{
    private readonly IWorkNode _workNode;
    private volatile bool _isWorkDone = false;

    public RootWorkNode(IWorkNode workNode)
    {
        _workNode = workNode;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        if (_isWorkDone)
        {
            return;
        }

        (bool isNodeComplete, _) = _workNode.TryDoWork(false, false, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
        if (isNodeComplete)
        {
            _isWorkDone = true;
            //WeaverFixes.Logger.LogMessage($"Done with root work: {workerIndex}");
            return;
        }

        while (!_isWorkDone)
        {
            (isNodeComplete, bool didAnyWork) = _workNode.TryDoWork(false, true, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
            if (isNodeComplete)
            {
                _isWorkDone = true;
                //WeaverFixes.Logger.LogMessage($"Done with root work: {workerIndex}");
                break;
            }

            if (!didAnyWork)
            {
                (isNodeComplete, didAnyWork) = _workNode.TryDoWork(true, true, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
                if (isNodeComplete)
                {
                    _isWorkDone = true;
                    //WeaverFixes.Logger.LogMessage($"Done with root work: {workerIndex}");
                    break;
                }

                if (!didAnyWork)
                {
                    // If we couldn't wait for any work then there must no longer be enough work for all the threads.
                    // It is an assumption that parallelism will not increase in the future which is why we break out here.
                    //WeaverFixes.Logger.LogMessage($"Failed to find more work in work tree: {workerIndex}");
                    break;
                }
            }
        }
    }
    
    public IEnumerable<IWorkChunk> GetAllWorkChunks()
    {
        return _workNode.GetAllWorkChunks();
    }

    public void Reset()
    {
        _workNode.Reset();
        _isWorkDone = false;
    }

    public void DeepDispose()
    {
        _workNode?.DeepDispose();
    }

    public void Dispose()
    {
        _workNode?.Dispose();
    }
}
