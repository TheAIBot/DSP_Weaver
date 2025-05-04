using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Miners;

namespace Weaver.Optimizations.LinearDataAccess.Stations;

internal sealed class StationExecutor
{
    private OptimizedStation[] _optimizedStations;
    private int[] _networkIds;

    public void StationGameTick(PlanetFactory planet, long time, VeinMinerExecutor<StationMinerOutput> stationVeinMinerExecutor, ref MiningFlags miningFlags)
    {
        int num = (int)(time % 1163962800);
        if (num < 0)
        {
            num += 1163962800;
        }
        int num2 = (int)(time % 60);
        if (num2 < 0)
        {
            num2 += 60;
        }
        PlanetTransport transport = planet.transport;
        GameHistoryData history = GameMain.history;
        FactoryProductionStat factoryProductionStat = GameMain.statistics.production.factoryStatPool[transport.factory.index];
        int[] productRegister = factoryProductionStat.productRegister;
        int[] consumeRegister = factoryProductionStat.consumeRegister;
        float[] networkServes = transport.powerSystem.networkServes;
        float logisticDroneSpeedModified = history.logisticDroneSpeedModified;
        int logisticDroneCarries = history.logisticDroneCarries;
        float logisticShipSailSpeedModified = history.logisticShipSailSpeedModified;
        float shipWarpSpeed = (history.logisticShipWarpDrive ? history.logisticShipWarpSpeedModified : logisticShipSailSpeedModified);
        int logisticShipCarries = history.logisticShipCarries;
        StationComponent[] gStationPool = transport.gameData.galacticTransport.stationPool;
        AstroData[] astrosData = transport.gameData.galaxy.astrosData;
        VectorLF3 relativePos = transport.gameData.relativePos;
        UnityEngine.Quaternion relativeRot = transport.gameData.relativeRot;
        double num3 = history.miningSpeedScale;
        double num4 = transport.collectorsWorkCost;
        double gasTotalHeat = transport.planet.gasTotalHeat;
        float collectSpeedRate = ((gasTotalHeat - num4 <= 0.0) ? 1f : ((float)((num3 * gasTotalHeat - num4) / (gasTotalHeat - num4))));
        bool starmap = UIGame.viewMode == EViewMode.Starmap;
        OptimizedVeinMiner<StationMinerOutput>[] stationMiners = stationVeinMinerExecutor._optimizedMiners;
        OptimizedStation[] optimizedStations = _optimizedStations;
        int[] networkIds = _networkIds;
        GameTick_SandboxMode();
        long additionalEnergyConsumption = 0;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            OptimizedStation station = optimizedStations[i];

            float power = networkServes[networkIds[i]];
            station.stationComponent.InternalTickLocal(transport.factory, num, power, logisticDroneSpeedModified, logisticDroneCarries, transport.stationPool);
            if (station.stationComponent.isCollector)
            {
                station.UpdateCollection(transport.factory, collectSpeedRate, productRegister, ref miningFlags);
                additionalEnergyConsumption += transport.collectorWorkEnergyPerTick;
            }
            if (station.stationComponent.isVeinCollector)
            {
                station.UpdateVeinCollection(stationMiners, ref miningFlags);
            }
            if (station.stationComponent.isStellar)
            {
                station.stationComponent.InternalTickRemote(transport.factory, num2, logisticShipSailSpeedModified, shipWarpSpeed, logisticShipCarries, gStationPool, astrosData, ref relativePos, ref relativeRot, starmap, consumeRegister);
            }
        }

        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].stationComponent.UpdateNeeds();
        }

        lock (factoryProductionStat)
        {
            factoryProductionStat.energyConsumption += additionalEnergyConsumption;
        }
    }

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

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           BeltExecutor beltExecutor,
                           VeinMinerExecutor<StationMinerOutput> stationVeinMinerExecutor)
    {
        List<OptimizedStation> optimizedStations = [];
        List<int> networkIds = [];

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

            int? optimizedMinerIndex = null;
            if (station.isVeinCollector)
            {
                optimizedMinerIndex = stationVeinMinerExecutor.GetOptimizedMinerIndexFromMinerId(station.minerId);
            }

            int networkIndex = planet.powerSystem.consumerPool[station.pcId].networkId;
            optimizedStations.Add(new OptimizedStation(station, belts, optimizedMinerIndex));
            networkIds.Add(networkIndex);
            planet.entityNeeds[station.entityId] = station.needs;
        }

        _optimizedStations = optimizedStations.ToArray();
        _networkIds = networkIds.ToArray();
    }

    private void GameTick_SandboxMode()
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        OptimizedStation[] optimizedStations = _optimizedStations;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].stationComponent.UpdateKeepMode();
        }
    }
}
