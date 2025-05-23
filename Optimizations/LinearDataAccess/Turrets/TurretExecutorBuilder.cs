using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Turrets;

internal sealed class TurretExecutorBuilder
{
    private readonly List<OptimizedTurret> _optimizedTurrets = [];

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           BeltExecutor beltExecutor)
    {
        foreach (int turretIndex in subFactoryGraph.GetAllNodes()
                                                   .Where(x => x.EntityTypeIndex.EntityType == EntityType.Turret)
                                                   .Select(x => x.EntityTypeIndex.Index)
                                                   .OrderBy(x => x))
        {
            ref readonly TurretComponent turret = ref planet.defenseSystem.turrets.buffer[turretIndex];
            if (turret.id != turretIndex)
            {
                continue;
            }

            OptimizedCargoPath? targetBelt = null;
            int targetBeltOffset = 0;
            if (turret.targetBeltId > 0)
            {
                targetBeltOffset = planet.cargoTraffic.beltPool[turret.targetBeltId].pivotOnPath;
                CargoPath targetCargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[turret.targetBeltId].segPathId];
                targetBelt = beltExecutor.GetOptimizedCargoPath(targetCargoPath);
            }

            _optimizedTurrets.Add(new OptimizedTurret(targetBelt, targetBeltOffset, turretIndex));
        }
    }

    public TurretExecutor Build()
    {
        return new TurretExecutor(_optimizedTurrets.OrderBy(x => x.turretIndex).ToArray());
    }
}
