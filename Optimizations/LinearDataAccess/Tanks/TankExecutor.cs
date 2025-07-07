using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Tanks;

internal sealed class TankExecutor
{
    public OptimizedTank[] _optimizedTanks = null!;
    private Dictionary<int, int> _tankIdToOptimizedTankIndex = null!;

    public void GameTick()
    {
        OptimizedTank[] optimizedTanks = _optimizedTanks;

        for (int i = 0; i < optimizedTanks.Length; i++)
        {
            ref OptimizedTank tank = ref optimizedTanks[i];
            tank.GameTick(this);
            tank.TickOutput(this);
            if (tank.fluidCount == 0)
            {
                tank.fluidId = 0;
            }
        }
    }

    public void Save(PlanetFactory planet)
    {
        TankComponent[] tanks = planet.factoryStorage.tankPool;
        OptimizedTank[] optimizedTanks = _optimizedTanks;
        for (int i = 1; i < planet.factoryStorage.tankCursor; i++)
        {
            if (!_tankIdToOptimizedTankIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedTanks[optimizedIndex].Save(ref tanks[i]);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        List<OptimizedTank> optimizedTanks = [];
        Dictionary<int, int> tankIdToOptimizedTankIndex = [];

        int[] tankIndexes = subFactoryGraph.GetAllNodes()
                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Tank)
                                           .Select(x => x.EntityTypeIndex.Index)
                                           .OrderBy(x => x)
                                           .ToArray();

        for (int i = 0; i < tankIndexes.Length; i++)
        {
            int tankIndex = tankIndexes[i];
            ref readonly TankComponent tank = ref planet.factoryStorage.tankPool[tankIndex];
            if (tank.id != tankIndex)
            {
                continue;
            }

            beltExecutor.TryOptimizedCargoPath(planet, tank.belt0, out OptimizedCargoPath? optimizedBelt0);
            beltExecutor.TryOptimizedCargoPath(planet, tank.belt1, out OptimizedCargoPath? optimizedBelt1);
            beltExecutor.TryOptimizedCargoPath(planet, tank.belt2, out OptimizedCargoPath? optimizedBelt2);
            beltExecutor.TryOptimizedCargoPath(planet, tank.belt3, out OptimizedCargoPath? optimizedBelt3);

            int optimizedTankIndex = optimizedTanks.Count;
            tankIdToOptimizedTankIndex.Add(tank.id, optimizedTankIndex);
            optimizedTanks.Add(new OptimizedTank(in tank,
                                                 optimizedTankIndex,
                                                 optimizedBelt0,
                                                 optimizedBelt1,
                                                 optimizedBelt2,
                                                 optimizedBelt3));
        }

        for (int i = 0; i < tankIndexes.Length; i++)
        {
            int tankIndex = tankIndexes[i];
            ref readonly TankComponent tank = ref planet.factoryStorage.tankPool[tankIndex];
            if (tank.id != tankIndex)
            {
                continue;
            }

            int? optimizedNextTankIndex = null;
            if (tank.nextTankId > 0)
            {
                optimizedNextTankIndex = tankIdToOptimizedTankIndex[tank.nextTankId];
            }

            int? optimizedLastTankIndex = null;
            if (tank.lastTankId > 0)
            {
                optimizedLastTankIndex = tankIdToOptimizedTankIndex[tank.lastTankId];
            }

            optimizedTanks[tankIdToOptimizedTankIndex[tank.id]] = new OptimizedTank(optimizedLastTankIndex, optimizedNextTankIndex, optimizedTanks[tankIdToOptimizedTankIndex[tank.id]]);
        }

        _optimizedTanks = optimizedTanks.ToArray();
        _tankIdToOptimizedTankIndex = tankIdToOptimizedTankIndex;
    }
}
