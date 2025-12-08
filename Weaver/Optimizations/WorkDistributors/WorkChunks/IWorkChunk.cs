namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal interface IWorkChunk
{
    void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition);
}
