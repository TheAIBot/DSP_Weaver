using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

namespace Weaver.Optimizations.LinearDataAccess;

internal interface IOptimizedPlanet
{
    OptimizedPlanetStatus Status { get; }
    int OptimizeDelayInTicks { get; set; }
    void Save();
    void Initialize();
    bool RequestDysonSpherePower();
    void TransportGameTick(long time, UnityEngine.Vector3 playerPos);
    WorkStep[] GetMultithreadedWork(int maxParallelism);
}
