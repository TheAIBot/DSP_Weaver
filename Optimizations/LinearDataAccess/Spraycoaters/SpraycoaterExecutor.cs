using System.Collections.Generic;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Spraycoaters;

internal sealed class SpraycoaterExecutor
{
    private int[] _spraycoaterNetworkIds;
    private OptimizedSpraycoater[] _optimizedSpraycoaters;
    private bool[] _isSpraycoatingItems;
    private int[] _sprayTimes;

    public void SpraycoaterGameTick(PlanetFactory planet)
    {
        int[] consumeRegister = GameMain.statistics.production.factoryStatPool[planet.index].consumeRegister;
        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        for (int i = 0; i < optimizedSpraycoaters.Length; i++)
        {
            optimizedSpraycoaters[i].InternalUpdate(consumeRegister, ref isSpraycoatingItems[i], ref sprayTimes[i]);
        }
    }

    public void UpdatePower(OptimizedPlanet optimizedPlanet,
                            int[] spraycoaterPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisThreadNetworkPowerConsumption,
                            int _usedThreadCnt,
                            int _curThreadIdx,
                            int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, spraycoaterPowerConsumerTypeIndexes.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        int[] spraycoaterNetworkIds = _spraycoaterNetworkIds;
        bool[] assemblerReplicatings = optimizedPlanet._assemblerReplicatings;
        int[] assemblerExtraPowerRatios = optimizedPlanet._assemblerExtraPowerRatios;
        for (int j = _start; j < _end; j++)
        {
            int networkIndex = spraycoaterNetworkIds[j];
            int powerConsumerTypeIndex = spraycoaterPowerConsumerTypeIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisThreadNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, assemblerReplicatings[j], assemblerExtraPowerRatios[j]);
        }
    }

    public long GetPowerConsumption(PowerConsumerType powerConsumerType, bool isSprayCoatingItem, int sprayTime)
    {
        return powerConsumerType.GetRequiredEnergy(sprayTime < 10000 && isSprayCoatingItem);
    }

    public void Initialize(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        List<int> spraycoaterNetworkIds = [];
        List<OptimizedSpraycoater> optimizedSpraycoaters = [];
        List<bool> isSpraycoatingItems = [];
        List<int> sprayTimes = [];

        for (int i = 1; i < planet.cargoTraffic.spraycoaterCursor; i++)
        {
            ref readonly SpraycoaterComponent spraycoater = ref planet.cargoTraffic.spraycoaterPool[i];
            if (spraycoater.id != i)
            {
                continue;
            }

            if (spraycoater.incBeltId == 0 && spraycoater.cargoBeltId == 0)
            {
                continue;
            }

            BeltComponent? incommingBeltComponent = default;
            CargoPath incommingCargoPath = null;
            int incommingBeltSegIndexPlusSegPivotOffset = 0;
            if (spraycoater.incBeltId > 0)
            {
                incommingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.incBeltId];
                incommingCargoPath = planet.cargoTraffic.GetCargoPath(incommingBeltComponent.Value.segPathId);
                incommingBeltSegIndexPlusSegPivotOffset = incommingBeltComponent.Value.segIndex + incommingBeltComponent.Value.segPivotOffset;
            }

            BeltComponent? outgoingBeltComponent = default;
            CargoPath outgoingCargoPath = null;
            int outgoingBeltSegIndexPlusSegPivotOffset = 0;
            int outgoingBeltSpeed = 0;
            if (spraycoater.cargoBeltId > 0)
            {
                outgoingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.cargoBeltId];
                outgoingCargoPath = planet.cargoTraffic.GetCargoPath(outgoingBeltComponent.Value.segPathId);
                outgoingBeltSegIndexPlusSegPivotOffset = outgoingBeltComponent.Value.segIndex + outgoingBeltComponent.Value.segPivotOffset;
                outgoingBeltSpeed = outgoingBeltComponent.Value.speed;
            }

            int[] incItemIds = LDB.models.Select(planet.cargoTraffic.factory.entityPool[spraycoater.entityId].modelIndex).prefabDesc.incItemId;
            bool isSpraycoatingItem = spraycoater.cargoBeltItemId != 0;

            int networkId = planet.powerSystem.consumerPool[spraycoater.pcId].networkId;
            PowerNetwork powerNetwork = networkId != 0 ? planet.powerSystem.netPool[networkId] : null;

            spraycoaterNetworkIds.Add(networkId);
            optimizedSpraycoaters.Add(new OptimizedSpraycoater(incommingBeltSegIndexPlusSegPivotOffset,
                                                               incommingCargoPath,
                                                               incItemIds,
                                                               outgoingCargoPath,
                                                               outgoingBeltSegIndexPlusSegPivotOffset,
                                                               outgoingBeltSpeed,
                                                               powerNetwork,
                                                               in spraycoater));
            isSpraycoatingItems.Add(spraycoater.cargoBeltItemId != 0);
            sprayTimes.Add(spraycoater.sprayTime);
        }

        _spraycoaterNetworkIds = spraycoaterNetworkIds.ToArray();
        _optimizedSpraycoaters = optimizedSpraycoaters.ToArray();
        _isSpraycoatingItems = isSpraycoatingItems.ToArray();
        _sprayTimes = sprayTimes.ToArray();
    }
}