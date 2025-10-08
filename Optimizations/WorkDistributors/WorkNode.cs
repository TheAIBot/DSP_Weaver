using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

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
        int workStepIndex = _workStepIndex;
        if (workStepIndex == _actualWorkNodes.Length)
        {
            return (false, false);
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
        else
        {
            startWorkStepIndex = 0;
        }

        if (waitForWork)
        {
            int nextWorkStepIndex = workStepIndex + 1;
            if (nextWorkStepIndex < _actualWorkNodes.Length &&
                _actualWorkNodes[nextWorkStepIndex][0]?.IsLeaf == true && // While this does not garuantee that all nodes are leafs then this is good enough for now... probably
                _scheduledWorkIndex[nextWorkStepIndex] <= _actualWorkNodes[nextWorkStepIndex].Length)
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

                            return (false, hasDoneAnyWork);
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
                    int nextWorkStep = Interlocked.Increment(ref _workStepIndex);
                    _workNodeBarriers[workStepIndex].Set();
                    if (_actualWorkNodes.Length == nextWorkStep)
                    {
                        return (true, hasDoneAnyWork);
                    }
                }
            }
        }

        return (false, hasDoneAnyWork);
    }

    public IEnumerable<IWorkChunk> GetAllWorkChunks()
    {
        return _preservedWorkNodes.SelectMany(x => x.Where(y => y is not NoWorkNode).SelectMany(y => y.GetAllWorkChunks()));
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
