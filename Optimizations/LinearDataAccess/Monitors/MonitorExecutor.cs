using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Monitors;

internal sealed class MonitorExecutor
{
    private int[] _monitorIndexes;

    public void GameTick(PlanetFactory planet)
    {
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SpeakerComponent[] speakerPool = planet.digitalSystem.speakerPool;
        EntityData[] entityPool = planet.entityPool;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;
        CargoTraffic cargoTraffic = planet.cargoTraffic;

        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].InternalUpdate(cargoTraffic, sandboxToolsEnabled, entityPool, speakerPool, entityAnimPool);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _monitorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Monitor)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();
    }
}
