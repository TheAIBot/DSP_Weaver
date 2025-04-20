using System;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly WorkerThreadExecutor _workerThreadExecutor;
    private readonly object _singleThreadedCodeLock;
    private readonly HighStopwatch _stopWatch = new();
    private readonly double[] _workTypeTimings;

    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerThreadExecutor = workerThreadExecutor;
        _singleThreadedCodeLock = singleThreadedCodeLock;
        _workTypeTimings = new double[ArrayExtensions.GetEnumValuesEnumerable<WorkType>().Max(x => (int)x) + 1];
    }

    public void Execute(PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        int originalWorkerThreadIndex = _workerThreadExecutor.curThreadIdx;
        int originalWorkerUsedThreadCount = _workerThreadExecutor.usedThreadCnt;
        try
        {
            PlanetWorkManager planetWorkManager = null;
            while (true)
            {
                IWorkChunk? workChunk = null;
                if (planetWorkManager != null)
                {
                    workChunk = planetWorkManager.TryGetWork(out var _);
                    if (workChunk == null)
                    {
                        planetWorkManager = null;
                    }
                }

                if (workChunk == null)
                {
                    PlanetWorkPlan? planetWorkPlan = _starClusterWorkManager.TryGetWork();
                    if (planetWorkPlan == null)
                    {
                        break;
                    }

                    planetWorkManager = planetWorkPlan.Value.PlanetWorkManager;
                    workChunk = planetWorkPlan.Value.WorkChunk;
                }

                //_stopWatch.Begin();
                //bool recordTiming = true;

                workChunk.Execute(time);


                //if (recordTiming)
                //{
                //    _workTypeTimings[(int)workChunk.Value.WorkType] += _stopWatch.duration;
                //}

                planetWorkManager.CompleteWork(workChunk);
            }
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
            throw;
        }
        finally
        {
            _workerThreadExecutor.curThreadIdx = originalWorkerThreadIndex;
            _workerThreadExecutor.usedThreadCnt = originalWorkerUsedThreadCount;
        }
    }

    public double[] GetWorkTypeTimings() => _workTypeTimings;

    public void Reset()
    {
        Array.Clear(_workTypeTimings, 0, _workTypeTimings.Length);
    }
}
