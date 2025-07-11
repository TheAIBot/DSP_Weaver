﻿using System.Collections.Generic;
using Weaver.Optimizations.LinearDataAccess.Miners;
using Weaver.Optimizations.LinearDataAccess.Stations;
using Weaver.Optimizations.LinearDataAccess.Statistics;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedGasPlanet : IOptimizedPlanet
{
    private readonly PlanetFactory _planet;
    private GasPlanetWideStationExecutor _planetWideStationExecutor = null!;
    private OptimizedPlanetWideProductionStatistics _optimizedPlanetWideProductionStatistics = null!;

    private WorkStep[]? _workSteps;
    private int _workStepsParallelism;

    public OptimizedGasPlanet(PlanetFactory planet)
    {
        _planet = planet;
    }

    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    public static bool IsGasPlanet(PlanetFactory planet)
    {
        return planet.planet.type == EPlanetType.Gas;
    }

    public void Initialize()
    {
        _planetWideStationExecutor = new GasPlanetWideStationExecutor();
        _planetWideStationExecutor.Initialize(_planet);

        var planetWideProductionRegisterBuilder = new PlanetWideProductionRegisterBuilder(_planet);
        for (int i = 0; i < _planet.planet.gasItems.Length; i++)
        {
            planetWideProductionRegisterBuilder.AdditionalProductItemsIdToWatch(_planet.planet.gasItems[i]);
        }

        _optimizedPlanetWideProductionStatistics = planetWideProductionRegisterBuilder.Build();

        Status = OptimizedPlanetStatus.Running;
        _workSteps = null;
        _workStepsParallelism = -1;
    }

    public void Save()
    {
        _workSteps = null;
        _workStepsParallelism = -1;
        Status = OptimizedPlanetStatus.Stopped;
    }

    public void TransportGameTick(long time, UnityEngine.Vector3 playerPos)
    {
        var miningFlags = new MiningFlags();
        _planetWideStationExecutor.StationGameTick(_planet, time, ref miningFlags);

        _planet._miningFlag |= miningFlags.MiningFlag;
        _planet._veinMiningFlag |= miningFlags.VeinMiningFlag;

        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        _optimizedPlanetWideProductionStatistics.UpdateStatistics(time, productRegister, consumeRegister);
    }

    public WorkStep[] GetMultithreadedWork(int maxParallelism)
    {
        if (_workSteps == null || _workStepsParallelism != maxParallelism)
        {
            _workSteps = CreateMultithreadedWork(maxParallelism);
            _workStepsParallelism = maxParallelism;
        }

        return _workSteps;
    }

    private WorkStep[] CreateMultithreadedWork(int maxParallelism)
    {
        if (Status == OptimizedPlanetStatus.Stopped)
        {
            return CreateParallelWorkForNonRunningOptimizedPlanet(maxParallelism);
        }

        List<WorkStep> workSteps = [];
        workSteps.Add(new WorkStep([new PlanetWideTransport(this)]));
        return workSteps.ToArray();
    }

    private WorkStep[] CreateParallelWorkForNonRunningOptimizedPlanet(int maxParallelism)
    {
        List<WorkStep> work = [];

        int stationCount = _planet.transport.stationCursor;
        if (stationCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.TransportData, 1)));
        }

        return work.ToArray();
    }
}
