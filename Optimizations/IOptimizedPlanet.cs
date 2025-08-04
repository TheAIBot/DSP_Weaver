using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations;

internal interface IOptimizedPlanet
{
    OptimizedPlanetStatus Status { get; }
    int OptimizeDelayInTicks { get; set; }
    void Save();
    void Initialize();
    void TransportGameTick(long time, UnityEngine.Vector3 playerPos);
    WorkStep[] GetMultithreadedWork(int maxParallelism);
}
