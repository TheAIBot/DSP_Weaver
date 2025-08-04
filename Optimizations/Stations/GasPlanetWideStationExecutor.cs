using System.Collections.Generic;
using Weaver.Optimizations.Miners;

namespace Weaver.Optimizations.Stations;

internal sealed class GasPlanetWideStationExecutor
{
    private OptimizedStation[] _optimizedStations = null!;

    public void StationGameTick(PlanetFactory planet, long time, ref MiningFlags miningFlags)
    {
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
        float logisticShipSailSpeedModified = history.logisticShipSailSpeedModified;
        float shipWarpSpeed = history.logisticShipWarpDrive ? history.logisticShipWarpSpeedModified : logisticShipSailSpeedModified;
        int logisticShipCarries = history.logisticShipCarries;
        StationComponent[] gStationPool = transport.gameData.galacticTransport.stationPool;
        AstroData[] astrosData = transport.gameData.galaxy.astrosData;
        VectorLF3 relativePos = transport.gameData.relativePos;
        UnityEngine.Quaternion relativeRot = transport.gameData.relativeRot;
        double num3 = history.miningSpeedScale;
        double num4 = transport.collectorsWorkCost;
        double gasTotalHeat = transport.planet.gasTotalHeat;
        float collectSpeedRate = gasTotalHeat - num4 <= 0.0 ? 1f : (float)((num3 * gasTotalHeat - num4) / (gasTotalHeat - num4));
        bool starmap = UIGame.viewMode == EViewMode.Starmap;
        OptimizedStation[] optimizedStations = _optimizedStations;
        GameTick_SandboxMode();
        long additionalEnergyConsumption = 0;
        for (int i = 0; i < optimizedStations.Length; i++)
        {
            OptimizedStation station = optimizedStations[i];
            station.UpdateCollection(collectSpeedRate, productRegister, ref miningFlags);
            additionalEnergyConsumption += transport.collectorWorkEnergyPerTick;

            station.stationComponent.InternalTickRemote(transport.factory, num2, logisticShipSailSpeedModified, shipWarpSpeed, logisticShipCarries, gStationPool, astrosData, ref relativePos, ref relativeRot, starmap, consumeRegister);
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

    public void Initialize(PlanetFactory planet)
    {
        List<OptimizedStation> optimizedStations = [];

        for (int stationIndex = 1; stationIndex < planet.transport.stationCursor; stationIndex++)
        {
            StationComponent? station = planet.transport.stationPool[stationIndex];
            if (station == null || station.id != stationIndex)
            {
                continue;
            }

            optimizedStations.Add(new OptimizedStation(station, [], null));
            planet.entityNeeds[station.entityId] = station.needs;
        }

        _optimizedStations = optimizedStations.ToArray();
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