namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class DysonSphereGameUpdate : IWorkChunk
{
    private readonly DysonSphere _dysonSphere;

    public DysonSphereGameUpdate(DysonSphere dysonSphere)
    {
        _dysonSphere = dysonSphere;
    }

    // First part of DysonSphere.GameTick that can run multi threaded. Rest is single threaded and runs in ExecutePostFactorySingleThreadedSteps
    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DysonSphere dysonSphere = _dysonSphere;
        DeepProfiler.BeginSample(DPEntry.DysonShell, workerIndex, dysonSphere.starData.id);
        for (int i = 0; i < 10; i++)
        {
            if (dysonSphere.layersSorted[i] != null)
            {
                dysonSphere.layersSorted[i].GameTick(time);
            }
        }
        DeepProfiler.EndSample(DPEntry.DysonShell, workerIndex);
    }
}
