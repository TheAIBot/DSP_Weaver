using System.Collections.Generic;
using System.Threading;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkLeaf : IWorkNode
{
    private readonly IWorkChunk[] _workNodes;
    private int _completedCount;
    private int _scheduledCount;

    public bool IsLeaf => true;

    public WorkLeaf(IWorkChunk[] workNodes) {  
        _workNodes = workNodes; 
    }

    public (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        int scheduledCount = Interlocked.Increment(ref _scheduledCount) - 1;
        if (scheduledCount >= _workNodes.Length)
        {
            return (false, false);
        }

        _workNodes[scheduledCount].Execute(workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
        int totalCompletedCount =  Interlocked.Increment(ref _completedCount);


        return (totalCompletedCount == _workNodes.Length, true);
    }

    public IEnumerable<IWorkChunk> GetAllWorkChunks()
    {
        return _workNodes;
    }

    public void Reset()
    {
        _completedCount = 0;
        _scheduledCount = 0;
    }

    public int GetWorkChunkCount() => _workNodes.Length;

    public void DeepDispose()
    {
    }

    public void Dispose()
    {
    }
}
