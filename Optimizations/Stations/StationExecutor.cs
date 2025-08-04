﻿using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Miners;

namespace Weaver.Optimizations.Stations;

internal sealed class StationExecutor
{
    private OptimizedStation[] _optimizedStations = null!;
    private int[] _networkIds = null!;

    public void InputFromBelt()
    {
        OptimizedStation[] optimizedStations = _optimizedStations;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].UpdateInputSlots();
        }
    }

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
        int[] consumeRegister = factoryProductionStat.consumeRegister;
        float[] networkServes = transport.powerSystem.networkServes;
        float logisticDroneSpeedModified = history.logisticDroneSpeedModified;
        int logisticDroneCarries = history.logisticDroneCarries;
        float logisticShipSailSpeedModified = history.logisticShipSailSpeedModified;
        float shipWarpSpeed = history.logisticShipWarpDrive ? history.logisticShipWarpSpeedModified : logisticShipSailSpeedModified;
        int logisticShipCarries = history.logisticShipCarries;
        StationComponent[] gStationPool = transport.gameData.galacticTransport.stationPool;
        AstroData[] astrosData = transport.gameData.galaxy.astrosData;
        VectorLF3 relativePos = transport.gameData.relativePos;
        UnityEngine.Quaternion relativeRot = transport.gameData.relativeRot;
        bool starmap = UIGame.viewMode == EViewMode.Starmap;
        OptimizedVeinMiner<StationMinerOutput>[] stationMiners = stationVeinMinerExecutor._optimizedMiners;
        OptimizedStation[] optimizedStations = _optimizedStations;
        int[] networkIds = _networkIds;
        GameTick_SandboxMode();
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            OptimizedStation station = optimizedStations[i];

            float power = networkServes[networkIds[i]];
            station.stationComponent.InternalTickLocal(transport.factory, num, power, logisticDroneSpeedModified, logisticDroneCarries, transport.stationPool);
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
    }

    public void OutputToBelt()
    {
        OptimizedStation[] optimizedStations = _optimizedStations;
        int stationPilerLevel = GameMain.history.stationPilerLevel;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            optimizedStations[i].UpdateOutputSlots(stationPilerLevel);
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

            OptimizedCargoPath?[]? belts = null;
            for (int i = 0; i < station.slots.Length; i++)
            {
                if (beltExecutor.TryOptimizedCargoPath(planet, station.slots[i].beltId, out OptimizedCargoPath? belt))
                {
                    belts ??= new OptimizedCargoPath[station.slots.Length];
                    belts[i] = belt;
                }
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
