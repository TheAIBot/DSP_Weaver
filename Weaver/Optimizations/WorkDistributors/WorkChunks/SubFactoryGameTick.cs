using System;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class SubFactoryGameTick : IWorkChunk
{
    private readonly OptimizedSubFactory _subFactory;
    private readonly SubFactoryPowerConsumption _subFactoryPowerConsumption;

    public SubFactoryGameTick(OptimizedSubFactory subFactory, SubFactoryPowerConsumption subFactoryPowerConsumption)
    {
        _subFactory = subFactory;
        _subFactoryPowerConsumption = subFactoryPowerConsumption;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _subFactory.GameTick(workerIndex, time, _subFactoryPowerConsumption);
    }
}
