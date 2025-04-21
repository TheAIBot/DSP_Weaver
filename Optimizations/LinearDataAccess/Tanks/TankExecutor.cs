using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Tanks;

internal sealed class TankExecutor
{
    public OptimizedTank[] _optimizedTanks;
    private Dictionary<int, int> _tankIdToOptimizedTankIndex;

    public void GameTick(PlanetFactory planet)
    {
        OptimizedTank[] optimizedTanks = _optimizedTanks;

        for (int i = 0; i < optimizedTanks.Length; i++)
        {
            ref OptimizedTank tank = ref optimizedTanks[i];
            tank.GameTick(planet, this);
            tank.TickOutput(planet, this);
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

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        List<OptimizedTank> optimizedTanks = [];
        Dictionary<int, int> tankIdToOptimizedTankIndex = [];

        foreach (int tankIndex in subFactoryGraph.GetAllNodes()
                                                 .Where(x => x.EntityTypeIndex.EntityType == EntityType.Tank)
                                                 .Select(x => x.EntityTypeIndex.Index)
                                                 .OrderBy(x => x))
        {
            ref readonly TankComponent tank = ref planet.factoryStorage.tankPool[tankIndex];
            if (tank.id != tankIndex)
            {
                continue;
            }

            CargoPath belt0 = tank.belt0 > 0 ? planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[tank.belt0].segPathId] : null;
            CargoPath belt1 = tank.belt1 > 0 ? planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[tank.belt1].segPathId] : null;
            CargoPath belt2 = tank.belt2 > 0 ? planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[tank.belt2].segPathId] : null;
            CargoPath belt3 = tank.belt3 > 0 ? planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[tank.belt3].segPathId] : null;

            tankIdToOptimizedTankIndex.Add(tank.id, optimizedTanks.Count);
            optimizedTanks.Add(new OptimizedTank(in tank, belt0, belt1, belt2, belt3));
        }

        _optimizedTanks = optimizedTanks.ToArray();
        _tankIdToOptimizedTankIndex = tankIdToOptimizedTankIndex;
    }
}
