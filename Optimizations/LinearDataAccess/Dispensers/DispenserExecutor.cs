using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Dispensers;

internal sealed class DispenserExecutor
{
    private int[] _dispenserIndexes;

    public void SandboxMode(PlanetFactory planet)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        PlanetTransport transport = planet.transport;
        for (int dispenserIndexIndex = 0; dispenserIndexIndex < _dispenserIndexes.Length; dispenserIndexIndex++)
        {
            int dispenserIndex = _dispenserIndexes[dispenserIndexIndex];
            transport.dispenserPool[dispenserIndex].UpdateKeepMode();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _dispenserIndexes = subFactoryGraph.GetAllNodes()
                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Dispenser)
                                           .Select(x => x.EntityTypeIndex.Index)
                                           .OrderBy(x => x)
                                           .ToArray();
    }
}
