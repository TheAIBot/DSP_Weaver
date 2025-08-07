using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations;

internal interface IOptimizedPlanet
{
    OptimizedPlanetStatus Status { get; }
    int OptimizeDelayInTicks { get; set; }
    void Save();
    void Initialize();
    void TransportGameTick(WorkerThread workerThread, long time, UnityEngine.Vector3 playerPos);
    WorkStep[] GetMultithreadedWork(int maxParallelism);
}
