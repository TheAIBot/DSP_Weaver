using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations;

internal interface IOptimizedPlanet
{
    OptimizedPlanetStatus Status { get; }
    int OptimizeDelayInTicks { get; set; }
    void Save();
    void Initialize(UniverseStaticDataBuilder universeStaticDataBuilder);
    void TransportGameTick(int workerIndex, long time, UnityEngine.Vector3 playerPos);
    IWorkNode GetMultithreadedWork(int maxParallelism);
}
