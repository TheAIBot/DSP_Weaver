using Weaver.Optimizations.Miners;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Stations;
using Weaver.Optimizations.Statistics;
using Weaver.Optimizations.WorkDistributors;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations;

internal sealed class OptimizedGasPlanet : IOptimizedPlanet
{
    private readonly PlanetFactory _planet;
    private GasPlanetWideStationExecutor _planetWideStationExecutor = null!;
    private OptimizedPlanetWideProductionStatistics _optimizedPlanetWideProductionStatistics = null!;

    private IWorkNode? _workNodes;
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

    public void Initialize(UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        _planetWideStationExecutor = new GasPlanetWideStationExecutor();
        _planetWideStationExecutor.Initialize(_planet);

        var planetWideProductionRegisterBuilder = new PlanetWideProductionRegisterBuilder(_planet);
        for (int i = 0; i < _planet.planet.gasItems.Length; i++)
        {
            planetWideProductionRegisterBuilder.AdditionalProductItemsIdToWatch(_planet.planet.gasItems[i]);
        }

        _optimizedPlanetWideProductionStatistics = planetWideProductionRegisterBuilder.Build(universeStaticDataBuilder);

        Status = OptimizedPlanetStatus.Running;
        _workNodes = null;
        _workStepsParallelism = -1;
    }

    public void Save()
    {
        _workNodes = null;
        _workStepsParallelism = -1;
        Status = OptimizedPlanetStatus.Stopped;
    }

    public void TransportGameTick(int workerIndex, long time, UnityEngine.Vector3 playerPos)
    {
        var miningFlags = new MiningFlags();

        if (_planetWideStationExecutor.Count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Transport, workerIndex, _planet.planetId);
            DeepProfiler.BeginMajorSample(DPEntry.Station, workerIndex);
            _planetWideStationExecutor.StationGameTick(_planet, time, ref miningFlags);
            DeepProfiler.EndMajorSample(DPEntry.Station, workerIndex);
            DeepProfiler.EndSample(DPEntry.Transport, workerIndex);
        }

        _planet._miningFlag |= miningFlags.MiningFlag;
        _planet._veinMiningFlag |= miningFlags.VeinMiningFlag;

        DeepProfiler.BeginSample(DPEntry.Statistics, workerIndex);
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        _optimizedPlanetWideProductionStatistics.UpdateStatistics(time, productRegister, consumeRegister);
        DeepProfiler.EndSample(DPEntry.Statistics, workerIndex);
    }

    public IWorkNode GetMultithreadedWork(int maxParallelism)
    {
        if (_workNodes == null || _workStepsParallelism != maxParallelism)
        {
            _workNodes = CreateMultithreadedWork(maxParallelism);
            _workStepsParallelism = maxParallelism;
        }

        return _workNodes;
    }

    private IWorkNode CreateMultithreadedWork(int maxParallelism)
    {
        if (Status == OptimizedPlanetStatus.Stopped)
        {
            return CreateParallelWorkForNonRunningOptimizedPlanet(maxParallelism);
        }

        if (_planetWideStationExecutor.Count > 0)
        {
            return new WorkLeaf([new PlanetWideTransport(this)]);
        }

        return new NoWorkNode();
    }

    private IWorkNode CreateParallelWorkForNonRunningOptimizedPlanet(int maxParallelism)
    {
        if (_planet.transport.stationCursor > 0)
        {
            return new WorkLeaf(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.TransportData, 1));
        }

        return new NoWorkNode();
    }
}
