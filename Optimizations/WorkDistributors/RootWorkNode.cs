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
        while (!_isWorkDone)
        {
            (bool isNodeComplete, bool didAnyWork) = _workNode.TryDoWork(false, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
            if (isNodeComplete)
            {
                _isWorkDone = true;
                break;
            }

            if (!didAnyWork)
            {
                (isNodeComplete, didAnyWork) = _workNode.TryDoWork(true, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
                if (isNodeComplete)
                {
                    _isWorkDone = true;
                    break;
                }

                if (!didAnyWork)
                {
                    // If we couldn't wait for any work then there must no longer be enough work for all the threads.
                    // It is an assumption that parallelism will not increase in the future which is why we break out here.
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
