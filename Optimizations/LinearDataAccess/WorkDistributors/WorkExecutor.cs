using System;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly WorkerThreadExecutor _workerThreadExecutor;
    private readonly object _singleThreadedCodeLock;
    private readonly WorkerTimings _workerTimings;


    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerThreadExecutor = workerThreadExecutor;
        _singleThreadedCodeLock = singleThreadedCodeLock;
        _workerTimings = new WorkerTimings();
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

                workChunk.Execute(_workerTimings, time);

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

    public double[] GetWorkTypeTimings() => _workerTimings.WorkTypeTimings;

    public void Reset()
    {
        _workerTimings.Reset();
    }
}
