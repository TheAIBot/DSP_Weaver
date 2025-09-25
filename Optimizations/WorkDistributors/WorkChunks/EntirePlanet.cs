using System;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class EntirePlanet : IWorkChunk
{
    private readonly PlanetWidePower _planetWidePower;
    private readonly SubFactoryGameTick _subFactory;
    private readonly PostSubFactoryStep _postSubFactoryStep;

    public EntirePlanet(OptimizedTerrestrialPlanet optimizedPlanet, OptimizedSubFactory subFactory, SubFactoryPowerConsumption subFactoryPowerConsumption)
    {
        _planetWidePower = new PlanetWidePower(optimizedPlanet);
        _subFactory = new SubFactoryGameTick(subFactory, subFactoryPowerConsumption);
        _postSubFactoryStep = new PostSubFactoryStep(optimizedPlanet);
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _planetWidePower.Execute(workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
        _subFactory.Execute(workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
        _postSubFactoryStep.Execute(workerIndex, singleThreadedCodeLock, localPlanet, time, playerPosition);
    }
}
