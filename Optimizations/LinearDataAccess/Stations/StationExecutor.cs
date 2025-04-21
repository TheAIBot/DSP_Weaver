using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Stations;

internal sealed class StationExecutor
{
    private int[] _stationIndexes;

    public void InputFromBelt(PlanetFactory planet, long time)
    {
        PlanetTransport transport = planet.transport;
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        SignData[] entitySignPool = planet.entitySignPool;
        bool active = (time + planet.index) % 30 == 0L;

        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateInputSlots(cargoTraffic, entitySignPool, active);
        }
    }

    public void OutputToBelt(PlanetFactory planet, long time)
    {
        PlanetTransport transport = planet.transport;
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        SignData[] entitySignPool = planet.entitySignPool;
        int stationPilerLevel = GameMain.history.stationPilerLevel;
        bool active = (time + planet.index) % 30 == 0L;

        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateOutputSlots(cargoTraffic, entitySignPool, stationPilerLevel, active);
        }
    }

    public void SandboxMode(PlanetFactory planet)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        PlanetTransport transport = planet.transport;
        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateKeepMode();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _stationIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Station)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();
    }
}
