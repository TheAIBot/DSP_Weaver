using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class PlanetWidePower : IWorkChunk
{
    private readonly OptimizedTerrestrialPlanet _optimizedPlanet;

    public PlanetWidePower(OptimizedTerrestrialPlanet optimizedPlanet)
    {
        _optimizedPlanet = optimizedPlanet;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
        _optimizedPlanet.BeforePowerStep(time);
        DeepProfiler.EndSample(DPEntry.PowerConsumer, workerIndex);

        DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
        _optimizedPlanet.PowerStep(time);
        DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
    }
}

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

        if (time > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DFGSystem, workerIndex);
            EnemyDFGroundSystem enemySystem = _planet.enemySystem;
            if (enemySystem.turrets.count > 0)
            {
                DeepProfiler.BeginSample(DPEntry.DFGTurret, workerIndex);
                enemySystem.GameTickLogic_Turret(time, _gameLogic.aggressiveLevel);
                DeepProfiler.EndSample(DPEntry.DFGTurret, workerIndex);
            }
            if (enemySystem.units.count > 0)
            {
                DeepProfiler.BeginSample(DPEntry.DFGUnit, workerIndex);
                enemySystem.GameTickLogic_Unit(time, _gameLogic.aggressiveLevel);
                DeepProfiler.EndSample(DPEntry.DFGUnit, workerIndex);
            }
            DeepProfiler.EndSample(DPEntry.DFGSystem, workerIndex);
        }

        if (_planet.constructionSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.Construction, workerIndex);
            bool isActive = _planet == localPlanet?.factory;
            _planet.constructionSystem.GameTick(time, isActive);
            _planet.constructionSystem.ExcuteDeferredTargetChange();
            DeepProfiler.EndSample(DPEntry.Construction, workerIndex);
        }
    }
}