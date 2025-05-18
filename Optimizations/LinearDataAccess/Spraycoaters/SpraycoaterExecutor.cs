using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Spraycoaters;

internal sealed class SpraycoaterExecutor
{
    private int[] _spraycoaterNetworkIds = null!;
    private OptimizedSpraycoater[] _optimizedSpraycoaters = null!;
    private bool[] _isSpraycoatingItems = null!;
    private int[] _sprayTimes = null!;
    private OptimizedItemId[] _incItemIds = null!;
    public Dictionary<int, int> _spraycoaterIdToOptimizedSpraycoaterIndex = null!;

    public int SpraycoaterCount => _optimizedSpraycoaters.Length;

    public void GameTick(PlanetFactory planet, int[] consumeRegister)
    {

        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        OptimizedItemId[] incItemIds = _incItemIds;
        for (int i = 0; i < optimizedSpraycoaters.Length; i++)
        {
            optimizedSpraycoaters[i].InternalUpdate(incItemIds, consumeRegister, ref isSpraycoatingItems[i], ref sprayTimes[i]);
        }
    }

    public void UpdatePower(int[] spraycoaterPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        int[] spraycoaterNetworkIds = _spraycoaterNetworkIds;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        for (int j = 0; j < optimizedSpraycoaters.Length; j++)
        {
            int networkIndex = spraycoaterNetworkIds[j];
            int powerConsumerTypeIndex = spraycoaterPowerConsumerTypeIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, isSpraycoatingItems[j], sprayTimes[j]);
        }
    }

    public static long GetPowerConsumption(PowerConsumerType powerConsumerType, bool isSprayCoatingItem, int sprayTime)
    {
        return powerConsumerType.GetRequiredEnergy(sprayTime < 10000 && isSprayCoatingItem);
    }

    public void Save(PlanetFactory planet)
    {
        SpraycoaterComponent[] spraycoaters = planet.cargoTraffic.spraycoaterPool;
        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        for (int i = 1; i < planet.cargoTraffic.spraycoaterCursor; i++)
        {
            if (!_spraycoaterIdToOptimizedSpraycoaterIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedSpraycoaters[optimizedIndex].Save(ref spraycoaters[i], isSpraycoatingItems[optimizedIndex], sprayTimes[optimizedIndex]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           OptimizedSubFactory subFactory,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> spraycoaterNetworkIds = [];
        List<OptimizedSpraycoater> optimizedSpraycoaters = [];
        List<bool> isSpraycoatingItems = [];
        List<int> sprayTimes = [];
        Dictionary<int, int> spraycoaterIdToOptimizedSpraycoaterIndex = [];
        int[]? incItemIds = null;

        foreach (int spraycoaterIndex in subFactoryGraph.GetAllNodes()
                                                        .Where(x => x.EntityTypeIndex.EntityType == EntityType.SprayCoater)
                                                        .Select(x => x.EntityTypeIndex.Index)
                                                        .OrderBy(x => x))
        {
            ref readonly SpraycoaterComponent spraycoater = ref planet.cargoTraffic.spraycoaterPool[spraycoaterIndex];
            if (spraycoater.id != spraycoaterIndex)
            {
                continue;
            }

            if (spraycoater.incBeltId == 0 && spraycoater.cargoBeltId == 0)
            {
                continue;
            }

            BeltComponent? incommingBeltComponent = default;
            OptimizedCargoPath? incommingCargoPath = null;
            int incommingBeltSegIndexPlusSegPivotOffset = 0;
            if (spraycoater.incBeltId > 0)
            {
                incommingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.incBeltId];
                CargoPath? incommingBelt = planet.cargoTraffic.GetCargoPath(incommingBeltComponent.Value.segPathId);
                incommingCargoPath = incommingBelt != null ? beltExecutor.GetOptimizedCargoPath(incommingBelt) : null;
                incommingBeltSegIndexPlusSegPivotOffset = incommingBeltComponent.Value.segIndex + incommingBeltComponent.Value.segPivotOffset;
            }

            BeltComponent? outgoingBeltComponent = default;
            OptimizedCargoPath? outgoingCargoPath = null;
            int outgoingBeltSegIndexPlusSegPivotOffset = 0;
            int outgoingBeltSpeed = 0;
            if (spraycoater.cargoBeltId > 0)
            {
                outgoingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.cargoBeltId];
                CargoPath? outgoingbelt = planet.cargoTraffic.GetCargoPath(outgoingBeltComponent.Value.segPathId);
                outgoingCargoPath = outgoingbelt != null ? beltExecutor.GetOptimizedCargoPath(outgoingbelt) : null;
                outgoingBeltSegIndexPlusSegPivotOffset = outgoingBeltComponent.Value.segIndex + outgoingBeltComponent.Value.segPivotOffset;
                outgoingBeltSpeed = outgoingBeltComponent.Value.speed;
            }

            OptimizedItemId incItemId = default;
            if (spraycoater.incItemId != 0)
            {
                incItemId = subFactoryProductionRegisterBuilder.AddConsume(spraycoater.incItemId);
            }

            int[] newIncItemIds = LDB.models.Select(planet.cargoTraffic.factory.entityPool[spraycoater.entityId].modelIndex).prefabDesc.incItemId;
            if (incItemIds != null && !incItemIds.SequenceEqual(newIncItemIds))
            {
                throw new InvalidOperationException($"Assumption that {nameof(incItemIds)} is the same for all spray coaters is not correct.");
            }
            incItemIds = newIncItemIds;

            bool isSpraycoatingItem = spraycoater.cargoBeltItemId != 0;

            int networkId = planet.powerSystem.consumerPool[spraycoater.pcId].networkId;
            PowerNetwork? powerNetwork = networkId != 0 ? planet.powerSystem.netPool[networkId] : null;

            optimizedPowerSystemBuilder.AddSpraycoater(subFactory, in spraycoater, networkId);
            spraycoaterIdToOptimizedSpraycoaterIndex.Add(spraycoaterIndex, optimizedSpraycoaters.Count);
            spraycoaterNetworkIds.Add(networkId);
            optimizedSpraycoaters.Add(new OptimizedSpraycoater(incommingBeltSegIndexPlusSegPivotOffset,
                                                               incommingCargoPath,
                                                               outgoingCargoPath,
                                                               outgoingBeltSegIndexPlusSegPivotOffset,
                                                               outgoingBeltSpeed,
                                                               powerNetwork,
                                                               incItemId,
                                                               in spraycoater));
            isSpraycoatingItems.Add(spraycoater.cargoBeltItemId != 0);
            sprayTimes.Add(spraycoater.sprayTime);
        }

        incItemIds ??= [];
        _incItemIds = subFactoryProductionRegisterBuilder.AddConsume(incItemIds);

        _spraycoaterNetworkIds = spraycoaterNetworkIds.ToArray();
        _optimizedSpraycoaters = optimizedSpraycoaters.ToArray();
        _isSpraycoatingItems = isSpraycoatingItems.ToArray();
        _sprayTimes = sprayTimes.ToArray();
        _spraycoaterIdToOptimizedSpraycoaterIndex = spraycoaterIdToOptimizedSpraycoaterIndex;
    }
}