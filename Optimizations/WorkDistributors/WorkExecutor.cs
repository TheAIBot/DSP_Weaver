using System;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkExecutor
{
    private readonly StarClusterWorkManager _starClusterWorkManager;
    private readonly int _workerIndex;
    private readonly object _singleThreadedCodeLock;


    public WorkExecutor(StarClusterWorkManager starClusterWorkManager, int workerIndex, object singleThreadedCodeLock)
    {
        _starClusterWorkManager = starClusterWorkManager;
        _workerIndex = workerIndex;
        _singleThreadedCodeLock = singleThreadedCodeLock;
    }

    public void Execute(PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        try
        {
            RootWorkNode rootWorkNode = _starClusterWorkManager.GetRootWorkNode();
            rootWorkNode.Execute(_workerIndex, _singleThreadedCodeLock, localPlanet, time, playerPosition);
        }
        catch (Exception e)
        {
            WeaverFixes.Logger.LogError(e.Message);
            WeaverFixes.Logger.LogError(e.StackTrace);
            throw;
        }
    }
}
