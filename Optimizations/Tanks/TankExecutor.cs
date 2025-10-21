using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Tanks;

internal sealed class TankExecutor
{
    public OptimizedTank[] _optimizedTanks = null!;
    private Dictionary<int, int> _tankIdToOptimizedTankIndex = null!;

    public int Count => _optimizedTanks.Length;

    public void GameTick(OptimizedCargoPath[] optimizedCargoPaths)
    {
        OptimizedTank[] optimizedTanks = _optimizedTanks;

        for (int i = 0; i < optimizedTanks.Length; i++)
        {
            ref OptimizedTank tank = ref optimizedTanks[i];
            tank.GameTick(this, optimizedCargoPaths);
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

            beltExecutor.TryGetOptimizedCargoPathIndex(planet, tank.belt0, out int optimizedBelt0Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, tank.belt1, out int optimizedBelt1Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, tank.belt2, out int optimizedBelt2Index);
            beltExecutor.TryGetOptimizedCargoPathIndex(planet, tank.belt3, out int optimizedBelt3Index);

            int optimizedTankIndex = optimizedTanks.Count;
            tankIdToOptimizedTankIndex.Add(tank.id, optimizedTankIndex);
            optimizedTanks.Add(new OptimizedTank(in tank,
                                                 optimizedTankIndex,
                                                 optimizedBelt0Index,
                                                 optimizedBelt1Index,
                                                 optimizedBelt2Index,
                                                 optimizedBelt3Index));
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
