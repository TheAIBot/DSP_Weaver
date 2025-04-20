using System.Threading;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class PlanetWorkManager
{
    private WorkStep[] _workSteps;
    private int _currentWorkStepIndex;

    public PlanetFactory Planet { get; }
    public OptimizedPlanet OptimizedPlanet { get; }

    public PlanetWorkManager(PlanetFactory planet, OptimizedPlanet optimizedPlanet)
    {
        Planet = planet;
        OptimizedPlanet = optimizedPlanet;
    }

    public void UpdatePlanetWork(int parallelism)
    {
        WorkStep[] updatedWorkSteps = OptimizedPlanet.GetMultithreadedWork(parallelism);
        if (_workSteps != updatedWorkSteps && _workSteps != null)
        {
            for (int i = 0; i < _workSteps.Length; i++)
            {
                _workSteps[i].Dispose();
            }
        }

        _workSteps = updatedWorkSteps;
    }

    public IWorkChunk? TryGetWork(out bool canScheduleMoreWork)
    {
        int currentWorkTrackerIndex = _currentWorkStepIndex;
        if (currentWorkTrackerIndex == _workSteps.Length)
        {
            canScheduleMoreWork = false;
            return null;
        }

        WorkStep workStep = _workSteps[currentWorkTrackerIndex];
        IWorkChunk? workChunk = workStep.TryGetWork(out bool canNoLongerProvideWork);
        if (canNoLongerProvideWork)
        {
            canScheduleMoreWork = false;
        }
        canScheduleMoreWork = true;

        return workChunk;
    }

    public IWorkChunk? TryWaitForWork()
    {
        int currentWorkStepIndex = _currentWorkStepIndex;
        if (currentWorkStepIndex == _workSteps.Length)
        {
            return null;
        }

        int nextWorkStepIndex = currentWorkStepIndex + 1;
        if (nextWorkStepIndex >= _workSteps.Length)
        {
            return null;
        }

        WorkStep currentWorkStep = _workSteps[currentWorkStepIndex];
        WorkStep nextWorkStep = _workSteps[nextWorkStepIndex];
        IWorkChunk? workChunk = nextWorkStep.TryGetWork(out bool canNoLongerProvideWork);
        if (workChunk == null)
        {
            return workChunk;
        }

        currentWorkStep.WaitForCompletion();
        return workChunk;
    }

    public void CompleteWork(IWorkChunk workChunk)
    {
        if (workChunk.Complete())
        {
            Interlocked.Increment(ref _currentWorkStepIndex);
            workChunk.CompleteStep();
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _workSteps.Length; i++)
        {
            _workSteps[i].Reset();
        }
        _currentWorkStepIndex = 0;
    }
}
