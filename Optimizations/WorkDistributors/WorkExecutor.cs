using System;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly WorkerThread _workerThread;
    private readonly object _singleThreadedCodeLock;


    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, WorkerThread workerThread, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerThread = workerThread;
        _singleThreadedCodeLock = singleThreadedCodeLock;
    }

    public void Execute(PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        try
        {
            PlanetWorkManager? planetWorkManager = null;
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

                workChunk.Execute(_workerThread, _singleThreadedCodeLock, localPlanet, time, playerPosition);

                planetWorkManager!.CompleteWork(workChunk);
            }
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
            throw;
        }
    }
}
