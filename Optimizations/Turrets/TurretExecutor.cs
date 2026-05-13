using System.Collections.Generic;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Turrets;

internal sealed class TurretExecutor
{
    private readonly OptimizedTurret[] _optimizedTurrets;
    private readonly Dictionary<int, int> _turretIdToOptimizedTurretIndex;
    private readonly Dictionary<BeltIndex, object> _beltLocks;

    public TurretExecutor(OptimizedTurret[] optimizedTurrets,
                          Dictionary<int, int> turretIdToOptimizedTurretIndex,
                          Dictionary<BeltIndex, object> beltLocks)
    {
        _optimizedTurrets = optimizedTurrets;
        _turretIdToOptimizedTurretIndex = turretIdToOptimizedTurretIndex;
        _beltLocks = beltLocks;
    }

    public void TurretBeltUpdate(ref TurretComponent turret)
    {
        int optimizedTurretIndex = _turretIdToOptimizedTurretIndex[turret.id];
        ref OptimizedTurret optimizedTurret = ref _optimizedTurrets[optimizedTurretIndex];

        optimizedTurret.BeltUpdate(ref turret, _beltLocks);
    }
}