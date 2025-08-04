using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal interface IWorkChunk
{
    void Execute(WorkerTimings workerTimings, WorkerThreadExecutor workerThreadExecutor, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition);
    void TieToWorkStep(WorkStep workStep);
    bool Complete();

    void CompleteStep();
}
