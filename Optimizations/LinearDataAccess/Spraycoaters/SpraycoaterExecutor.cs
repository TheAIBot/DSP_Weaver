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
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int SpraycoaterCount => _optimizedSpraycoaters.Length;

    public void GameTick(PlanetFactory planet,
                         int[] spraycoaterPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] consumeRegister)
    {

        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        OptimizedItemId[] incItemIds = _incItemIds;
        int[] spraycoaterNetworkIds = _spraycoaterNetworkIds;
        for (int spraycoaterIndex = 0; spraycoaterIndex < optimizedSpraycoaters.Length; spraycoaterIndex++)
        {
            ref bool isSpraycoatingItem = ref isSpraycoatingItems[spraycoaterIndex];
            ref int sprayTime = ref sprayTimes[spraycoaterIndex];
            optimizedSpraycoaters[spraycoaterIndex].InternalUpdate(incItemIds, consumeRegister, ref isSpraycoatingItems[spraycoaterIndex], ref sprayTimes[spraycoaterIndex]);

            int networkIndex = spraycoaterNetworkIds[spraycoaterIndex];
            UpdatePower(spraycoaterPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, spraycoaterIndex, networkIndex, isSpraycoatingItems[spraycoaterIndex], sprayTimes[spraycoaterIndex]);
        }
    }

    public void UpdatePower(int[] spraycoaterPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] spraycoaterNetworkIds = _spraycoaterNetworkIds;
        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        for (int spraycoaterIndex = 0; spraycoaterIndex < spraycoaterNetworkIds.Length; spraycoaterIndex++)
        {
            int networkIndex = spraycoaterNetworkIds[spraycoaterIndex];
            UpdatePower(spraycoaterPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, spraycoaterIndex, networkIndex, isSpraycoatingItems[spraycoaterIndex], sprayTimes[spraycoaterIndex]);
        }
    }

    private static void UpdatePower(int[] spraycoaterPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int spraycoaterIndex,
                                    int networkIndex,
                                    bool isSpraycoatingItem,
                                    int sprayTime)
    {
        int powerConsumerTypeIndex = spraycoaterPowerConsumerTypeIndexes[spraycoaterIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, isSpraycoatingItem, sprayTime);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] spraycoaterPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        bool[] isSpraycoatingItems = _isSpraycoatingItems;
        int[] sprayTimes = _sprayTimes;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int spraycoaterIndex = 0; spraycoaterIndex < isSpraycoatingItems.Length; spraycoaterIndex++)
        {
            bool isSpraycoatingItem = isSpraycoatingItems[spraycoaterIndex];
            int sprayTime = sprayTimes[spraycoaterIndex];
            UpdatePowerConsumptionPerPrototype(spraycoaterPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               spraycoaterIndex,
                                               isSpraycoatingItem,
                                               sprayTime);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] spraycoaterPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int spraycoaterIndex,
                                                           bool isSpraycoatingItem,
                                                           int sprayTime)
    {
        int powerConsumerTypeIndex = spraycoaterPowerConsumerTypeIndexes[spraycoaterIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[spraycoaterIndex]] += GetPowerConsumption(powerConsumerType, isSpraycoatingItem, sprayTime);
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
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> spraycoaterNetworkIds = [];
        List<OptimizedSpraycoater> optimizedSpraycoaters = [];
        List<bool> isSpraycoatingItems = [];
        List<int> sprayTimes = [];
        Dictionary<int, int> spraycoaterIdToOptimizedSpraycoaterIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
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

            subFactoryPowerSystemBuilder.AddSpraycoater(in spraycoater, networkId);
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
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[spraycoater.entityId]);
        }

        incItemIds ??= [];
        _incItemIds = subFactoryProductionRegisterBuilder.AddConsume(incItemIds);

        _spraycoaterNetworkIds = spraycoaterNetworkIds.ToArray();
        _optimizedSpraycoaters = optimizedSpraycoaters.ToArray();
        _isSpraycoatingItems = isSpraycoatingItems.ToArray();
        _sprayTimes = sprayTimes.ToArray();
        _spraycoaterIdToOptimizedSpraycoaterIndex = spraycoaterIdToOptimizedSpraycoaterIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, bool isSprayCoatingItem, int sprayTime)
    {
        return powerConsumerType.GetRequiredEnergy(sprayTime < 10000 && isSprayCoatingItem);
    }
}