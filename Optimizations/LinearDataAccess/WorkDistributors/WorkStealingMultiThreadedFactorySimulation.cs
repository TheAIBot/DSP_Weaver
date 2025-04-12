using System.Threading.Tasks;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class WorkStealingMultiThreadedFactorySimulation
{
    private readonly HighStopwatch _stopWatch = new();
    private readonly object _singleThreadedCodeLock = new();
    private readonly PerformanceMonitorUpdater _performanceMonitorUpdater = PerformanceMonitorUpdater.Create();
    private StarClusterWorkManager _starClusterWorkManager;
    private WorkExecutor[] _workExecutors;

    public void Simulate()
    {
        MultithreadSystem multithreadSystem = GameMain.multithreadSystem;
        if (_starClusterWorkManager == null)
        {
            _starClusterWorkManager = new StarClusterWorkManager(GameMain.data.factories, multithreadSystem.usedThreadCnt);
        }
        if (_starClusterWorkManager.Parallelism != multithreadSystem.usedThreadCnt)
        {
            _starClusterWorkManager.SetMaxWorkParallelism(multithreadSystem.usedThreadCnt);
        }
        if (_workExecutors == null || _workExecutors.Length != multithreadSystem.usedThreadCnt)
        {
            _workExecutors = new WorkExecutor[multithreadSystem.usedThreadCnt];
            for (int i = 0; i < _workExecutors.Length; i++)
            {
                _workExecutors[i] = new WorkExecutor(_starClusterWorkManager, multithreadSystem.workerThreadExecutors[i], _singleThreadedCodeLock);
            }
        }

        _starClusterWorkManager.Reset();
        for (int i = 0; i < _workExecutors.Length; i++)
        {
            _workExecutors[i].Reset();
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        _stopWatch.Begin();
        Parallel.ForEach(_workExecutors, parallelOptions, workExecutor => workExecutor.Execute(GameMain.localPlanet, GameMain.gameTick));
        double totalTime = _stopWatch.duration;

        _performanceMonitorUpdater.UpdateTimings(totalTime, _workExecutors);
    }

    public void Clear()
    {
        _starClusterWorkManager = null;
        _workExecutors = null;
    }


}
