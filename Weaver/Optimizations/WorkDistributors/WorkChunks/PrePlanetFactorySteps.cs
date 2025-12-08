namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PrePlanetFactorySteps : IWorkChunk
{
    private readonly GameLogic _gameLogic;
    private readonly PlanetFactory _planet;

    public PrePlanetFactorySteps(GameLogic gameLogic, PlanetFactory planet)
    {
        _gameLogic = gameLogic;
        _planet = planet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        if (time > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DFGSystem, workerIndex);
            DeepProfiler.BeginSample(DPEntry.DFGBase, workerIndex);
            _planet.enemySystem.GameTickLogic_Prepare();
            _planet.enemySystem.GameTickLogic_Base(_gameLogic.aggressiveLevel);
            DeepProfiler.EndSample(DPEntry.DFGBase, workerIndex);
            DeepProfiler.EndSample(DPEntry.DFGSystem, workerIndex);
        }

        // Local planet updates compute buffer so it is done on the main thread instead
        if (_planet.constructionSystem != null && _planet.planet != localPlanet)
        {
            DeepProfiler.BeginSample(DPEntry.Construction, workerIndex);
            bool isActive = _planet == localPlanet?.factory;
            _planet.constructionSystem.GameTick(time, isActive);
            _planet.constructionSystem.ExcuteDeferredTargetChange();
            DeepProfiler.EndSample(DPEntry.Construction, workerIndex);
        }
    }
}