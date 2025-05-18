using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Turrets;

internal sealed class TurretExecutorBuilder
{
    private readonly List<OptimizedTurret> _optimizedTurrets = [];

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           PlanetWideProductionRegisterBuilder planetWideProductionRegisterBuilder,
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

            int[] turretAmmunitionItemIds = ItemProto.turretNeeds[(uint)turret.ammoType];
            for (int i = 0; i < turretAmmunitionItemIds.Length; i++)
            {
                planetWideProductionRegisterBuilder.AdditionalItemsIdsToWatch(turretAmmunitionItemIds[i]);
            }


            _optimizedTurrets.Add(new OptimizedTurret(targetBelt, targetBeltOffset, turretIndex));
        }
    }

    public TurretExecutor Build()
    {
        return new TurretExecutor(_optimizedTurrets.OrderBy(x => x.turretIndex).ToArray());
    }
}
