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
        if (workNodes.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workNodes), workNodes, "There must be at least 1 work for the work to be valid.");
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
        int workStepIndex;
        do
        {
            workStepIndex = _workStepIndex;
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
                (bool isNodeComplete, bool didAnyWork) = TryFindWorkToWaitFor(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition, workStepIndex);
                if (didAnyWork)
                {
                    return (isNodeComplete, didAnyWork);
                }
            }


            for (int i = startWorkStepIndex; i < workNodeCount; i++)
            {
                IWorkNode? workNode = _actualWorkNodes[workStepIndex][i];
                if (workNode == null)
                {
                    continue;
                }

                (bool isNodeComplete, bool didAnyWork) = TryDoWorkInNode(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition, workStepIndex, i, workNodeCount, workNode);
                if (isNodeComplete)
                {
                    return (true, true);
                }

                hasDoneAnyWork |= didAnyWork;
            }
        } while (workStepIndex != _workStepIndex);

        return (false, hasDoneAnyWork);
    }

    private (bool isNodeComplete, bool didAnyWork) TryFindWorkToWaitFor(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition, int workStepIndex)
    {
        int nextWorkStepIndex = workStepIndex + 1;
        if (nextWorkStepIndex >= _actualWorkNodes.Length)
        {
            return (false, false);
        }

        // While this does not garuantee that all nodes are leafs then this is good enough for now... probably
        if (_actualWorkNodes[nextWorkStepIndex][0]?.IsLeaf != true)
        {
            return (false, false);
        }

        if (_scheduledWorkIndex[nextWorkStepIndex] > _actualWorkNodes[nextWorkStepIndex].Length)
        {
            return (false, false);
        }

        int scheduleFutureWork = Interlocked.Increment(ref _scheduledWorkIndex[nextWorkStepIndex]) - 1;
        int nextWorkNodeCount = _actualWorkNodes[nextWorkStepIndex].Length;
        if (scheduleFutureWork >= nextWorkNodeCount)
        {
            return (false, false);
        }

        IWorkNode? workNode = _actualWorkNodes[nextWorkStepIndex][scheduleFutureWork];
        if (workNode == null)
        {
            return (false, false);
        }

        //WeaverFixes.Logger.LogMessage("Thread waiting for ongoing work to complete.");
        _workNodeBarriers[workStepIndex].WaitOne();
        return TryDoWorkInNode(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition, nextWorkStepIndex, scheduleFutureWork, nextWorkNodeCount, workNode);
    }

    private (bool isNodeComplete, bool didAnyWork) TryDoWorkInNode(bool waitForWork, 
                                                                   int workerIndex, 
                                                                   object singleThreadedCodeLock, 
                                                                   PlanetData localPlanet, 
                                                                   long time, 
                                                                   UnityEngine.Vector3 playerPosition, 
                                                                   int workStepIndex, 
                                                                   int nodeIndex, 
                                                                   int nextWorkNodeCount, 
                                                                   IWorkNode workNode)
    {
        (bool isNodeComplete, bool hasDoneWorkInChildNode) = workNode.TryDoWork(waitForWork, workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
        if (!isNodeComplete)
        {
            return (false, hasDoneWorkInChildNode);
        }

        _actualWorkNodes[workStepIndex][nodeIndex] = null;

        int totalCompletedWorkIndex = Interlocked.Increment(ref _completedWorkIndex[workStepIndex]);
        if (totalCompletedWorkIndex != nextWorkNodeCount)
        {
            return (false, true);
        }

        int nextWorkStep = Interlocked.Increment(ref _workStepIndex);
        _workNodeBarriers[workStepIndex].Set();
        return (_actualWorkNodes.Length == nextWorkStep, true);
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
