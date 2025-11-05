using System.Collections.Generic;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class StarSystemSpaceHashWork : IWorkChunk
{
    private readonly List<DFSDynamicHashSystem> _spaceHashSystems;

    public StarSystemSpaceHashWork(List<DFSDynamicHashSystem> spaceHashSystems)
    {
        _spaceHashSystems = spaceHashSystems;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.GroundDefenseSystem, workerIndex);
        for (int i = 0; i < _spaceHashSystems.Count; i++)
        {
            _spaceHashSystems[i].GameTick();
        }
        DeepProfiler.EndSample(DPEntry.GroundDefenseSystem, workerIndex);
    }
}