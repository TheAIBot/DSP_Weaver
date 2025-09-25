using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal interface IWorkNode : IDisposable
{
    bool IsLeaf { get; }
    (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition);

    void Reset();
    int GetWorkChunkCount();
    void DeepDispose();
}

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

internal sealed class WorkNode : IWorkNode
{
    private readonly IWorkNode[][] _preservedWorkNodes;
    private readonly IWorkNode?[][] _actualWorkNodes;
    private readonly ManualResetEvent[] _workNodeBarriers;
    private readonly int[] _scheduledWorkIndex;
    private readonly int[] _completedWorkIndex;
    private int _workStepIndex;

    public bool IsLeaf => false;

    public WorkNode(IWorkNode[][] workNodes)
    {
        if (workNodes.Any(x => x.Length == 0))
        {
            throw new InvalidOperationException("");
        }
        _preservedWorkNodes = workNodes;
        _actualWorkNodes = _preservedWorkNodes.Select(x => x.ToArray()).ToArray();
        _workNodeBarriers = _actualWorkNodes.Select(_ => new ManualResetEvent(false)).ToArray();
        _scheduledWorkIndex = new int[workNodes.Length];
        _completedWorkIndex = new int[workNodes.Length];
        _workStepIndex = 0;
    }

    public (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool hasDoneAnyWork = false;
        // Work though work steps
        while (_workStepIndex < _actualWorkNodes.Length)
        {
            bool completedWorkStep = false;
            int workStepIndex = _workStepIndex;
            if (workStepIndex == _actualWorkNodes.Length)
            {
                break;
            }
            int startWorkStepIndex = _scheduledWorkIndex[workStepIndex];
            int workNodeCount = _actualWorkNodes[workStepIndex].Length;
            if (startWorkStepIndex < workNodeCount)
            {
                startWorkStepIndex = Interlocked.Increment(ref _scheduledWorkIndex[workStepIndex]) - 1;
                if (startWorkStepIndex >= workNodeCount)
                {
                    startWorkStepIndex = 0;
                }
            }

            if (waitForWork)
            {
                int nextWorkStepIndex = workStepIndex + 1;
                if (nextWorkStepIndex < _actualWorkNodes.Length &&
                    _actualWorkNodes[nextWorkStepIndex][0]?.IsLeaf == true && // While this does not garuantee that all nodes are leafs then this is good enough for now... probably
                    _scheduledWorkIndex[nextWorkStepIndex] < _actualWorkNodes[nextWorkStepIndex].Length)
                {
                    int scheduleFutureWork = Interlocked.Increment(ref _scheduledWorkIndex[nextWorkStepIndex]) - 1;
                    int nextWorkNodeCount = _actualWorkNodes[nextWorkStepIndex].Length;
                    if (scheduleFutureWork < nextWorkNodeCount)
                    {
                        IWorkNode? workNode = _actualWorkNodes[nextWorkStepIndex][scheduleFutureWork];
                        if (workNode != null)
                        {
                            _workNodeBarriers[workStepIndex].WaitOne();
                            (bool isNodeComplete, bool hasDoneWorkInChildNode) = workNode.TryDoWork(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
                            hasDoneAnyWork |= hasDoneWorkInChildNode;
                            if (isNodeComplete)
                            {
                                _actualWorkNodes[nextWorkStepIndex][scheduleFutureWork] = null;

                                int totalCompletedWorkIndex = Interlocked.Increment(ref _completedWorkIndex[nextWorkStepIndex]);
                                if (totalCompletedWorkIndex == nextWorkNodeCount)
                                {
                                    int nextWorkStep = Interlocked.Increment(ref _workStepIndex);
                                    _workNodeBarriers[nextWorkStepIndex].Set();
                                    if (_actualWorkNodes.Length == nextWorkStep)
                                    {
                                        return (true, hasDoneAnyWork);
                                    }
                                }

                                // break out to attempt to do work without waiting for it first
                                break;
                            }
                        }

                    }
                }
            }

            for (int i = startWorkStepIndex; i < workNodeCount; i++)
            {
                IWorkNode? workNode = _actualWorkNodes[workStepIndex][i];
                if (workNode == null)
                {
                    continue;
                }

                (bool isNodeComplete, bool hasDoneWorkInChildNode) = workNode.TryDoWork(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
                hasDoneAnyWork |= hasDoneWorkInChildNode;
                if (isNodeComplete)
                {
                    _actualWorkNodes[workStepIndex][i] = null;

                    int totalCompletedWorkIndex = Interlocked.Increment(ref _completedWorkIndex[workStepIndex]);
                    if (totalCompletedWorkIndex == workNodeCount)
                    {
                        completedWorkStep = true;
                        int nextWorkStep = Interlocked.Increment(ref _workStepIndex);
                        _workNodeBarriers[workStepIndex].Set();
                        if (_actualWorkNodes.Length == nextWorkStep)
                        {
                            return (true, hasDoneAnyWork);
                        }
                    }
                }
            }

            if (!completedWorkStep)
            {
                break;
            }
        }

        return (false, hasDoneAnyWork);
    }

    public void Reset()
    {
        for (int i = 0; i < _preservedWorkNodes.Length; i++)
        {
            Array.Copy(_preservedWorkNodes[i], _actualWorkNodes[i], _preservedWorkNodes[i].Length);
            _workNodeBarriers[i].Reset();

            for (int workNodeIndex = 0; workNodeIndex < _preservedWorkNodes[i].Length; workNodeIndex++)
            {
                _preservedWorkNodes[i][workNodeIndex].Reset();
            }
        }

        Array.Clear(_scheduledWorkIndex, 0, _scheduledWorkIndex.Length);
        Array.Clear(_completedWorkIndex, 0, _scheduledWorkIndex.Length);
        _workStepIndex = 0;
    }

    public int GetWorkChunkCount() => _preservedWorkNodes.Sum(x => x.Sum(static int (IWorkNode y) => y.GetWorkChunkCount()));

    public void DeepDispose()
    {
        Dispose();
        for (int i = 0; i < _preservedWorkNodes.Length; i++)
        {
            for (int y = 0; y < _preservedWorkNodes[i].Length; y++)
            {
                _preservedWorkNodes[i][y].DeepDispose();
            }
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _workNodeBarriers.Length; i++)
        {
            _workNodeBarriers[i].Dispose();
        }
    }
}

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


internal sealed class NoWorkNode : IWorkNode
{
    public bool IsLeaf => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();

    public int GetWorkChunkCount() => throw new NotImplementedException();

    public void Reset() => throw new NotImplementedException();

    public (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition) => throw new NotImplementedException();
    public void DeepDispose() => throw new NotImplementedException();
}