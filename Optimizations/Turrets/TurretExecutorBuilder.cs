using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Turrets;

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

            int targetBeltOffset = 0;
            if (beltExecutor.TryOptimizedCargoPath(planet, turret.targetBeltId, out OptimizedCargoPath? targetBelt))
            {
                targetBeltOffset = planet.cargoTraffic.beltPool[turret.targetBeltId].pivotOnPath;
            }

            int[] turretAmmunitionItemIds = ItemProto.turretNeeds[(uint)turret.ammoType];
            for (int i = 0; i < turretAmmunitionItemIds.Length; i++)
            {
                planetWideProductionRegisterBuilder.AdditionalConsumeItemsIdToWatch(turretAmmunitionItemIds[i]);
            }


            _optimizedTurrets.Add(new OptimizedTurret(targetBelt, targetBeltOffset, turretIndex));
        }
    }

    public TurretExecutor Build()
    {
        return new TurretExecutor(_optimizedTurrets.OrderBy(x => x.turretIndex).ToArray());
    }
}
