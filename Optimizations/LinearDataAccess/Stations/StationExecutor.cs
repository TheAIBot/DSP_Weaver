using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Stations;

internal sealed class StationExecutor
{
    private OptimizedStation[] _optimizedStations;

    public void InputFromBelt(PlanetFactory planet, long time)
    {
        OptimizedStation[] optimizedStations = _optimizedStations;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].UpdateInputSlots();
        }
    }

    public void OutputToBelt(PlanetFactory planet, long time)
    {
        OptimizedStation[] optimizedStations = _optimizedStations;
        int stationPilerLevel = GameMain.history.stationPilerLevel;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].UpdateOutputSlots(stationPilerLevel);
        }
    }

    public void SandboxMode(PlanetFactory planet)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        OptimizedStation[] optimizedStations = _optimizedStations;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].UpdateKeepMode();
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph, BeltExecutor beltExecutor)
    {
        List<OptimizedStation> optimizedStations = [];

        foreach (int stationIndex in subFactoryGraph.GetAllNodes()
                                                    .Where(x => x.EntityTypeIndex.EntityType == EntityType.Station)
                                                    .Select(x => x.EntityTypeIndex.Index)
                                                    .OrderBy(x => x))
        {
            StationComponent station = planet.transport.stationPool[stationIndex];
            if (station.id != stationIndex)
            {
                continue;
            }

            OptimizedCargoPath[] belts = new OptimizedCargoPath[station.slots.Length];
            for (int i = 0; i < belts.Length; i++)
            {
                CargoPath? belt = station.slots[i].beltId > 0 ? planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[station.slots[i].beltId].segPathId] : null;
                belts[i] = belt != null ? beltExecutor.GetOptimizedCargoPath(belt) : null;
            }

            optimizedStations.Add(new OptimizedStation(station, belts));
        }

        _optimizedStations = optimizedStations.ToArray();
    }
}
