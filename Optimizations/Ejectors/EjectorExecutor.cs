using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Ejectors;

internal sealed class EjectorExecutor
{
    public OptimizedEjector[] _optimizedEjectors = null!;
    private ReadonlyArray<short> _optimizedBulletItemId = default;
    private ReadonlyArray<int> _ejectorNetworkIds = default;
    public EjectorBulletData[] _ejectorBulletDatas = null!;
    private int[] _directions = null!;
    private ReadonlyArray<int> _incLevels = default;
    private Dictionary<int, int> _ejectorIdToOptimizedEjectorIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;
    public const int SoleEjectorNeedsIndex = 0;

    public int Count => _optimizedEjectors.Length;

    public int GetOptimizedEjectorIndex(int ejectorId)
    {
        return _ejectorIdToOptimizedEjectorIndex[ejectorId];
    }

    public void GameTick(PlanetFactory planet,
                         long time,
                         ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                         ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption,
                         int[] consumeRegister,
                         SubFactoryNeeds subFactoryNeeds)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;
        OptimizedEjector[] ejectors = _optimizedEjectors;
        EjectorBulletData[] ejectorBulletDatas = _ejectorBulletDatas;
        int[] directions = _directions;
        ReadonlyArray<int> incLevels = _incLevels;
        ReadonlyArray<short> optimizedBulletItemId = _optimizedBulletItemId;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Ejector);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;

        DysonSwarm? swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        bool runEjectorUpdate = false;
        if (swarm != null)
        {
            for (int i = 0; i < swarm.orbitCursor; i++)
            {
                if (swarm.orbits[i].enabled)
                {
                    runEjectorUpdate = true;
                    break;
                }
            }
        }

        ReadonlyArray<int> ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndex = 0; ejectorIndex < ejectors.Length; ejectorIndex++)
        {
            int networkIndex = ejectorNetworkIds[ejectorIndex];
            ref int direction = ref directions[ejectorIndex];
            int incLevel = incLevels[ejectorIndex];

            if (runEjectorUpdate)
            {
                ref EjectorBulletData ejectorBulletData = ref ejectorBulletDatas[ejectorIndex];
                short optimizedBulletId = optimizedBulletItemId[ejectorIndex];
                int needsOffset = groupNeeds.GetObjectNeedsIndex(ejectorIndex);
                float power3 = networkServes[networkIndex];

                ejectors[ejectorIndex].InternalUpdate(power3,
                                                      time,
                                                      swarm,
                                                      astroPoses,
                                                      optimizedBulletId,
                                                      consumeRegister,
                                                      componentsNeeds,
                                                      needsOffset,
                                                      ref ejectorBulletData,
                                                      ref direction,
                                                      incLevel);
            }

            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndex, networkIndex, direction, incLevel);
        }
    }

    public void UpdatePower(PlanetFactory planet,
                            ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                            ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        ReadonlyArray<int> ejectorNetworkIds = _ejectorNetworkIds;
        int[] directions = _directions;
        ReadonlyArray<int> incLevels = _incLevels;

        for (int ejectorIndex = 0; ejectorIndex < directions.Length; ejectorIndex++)
        {
            int networkIndex = ejectorNetworkIds[ejectorIndex];
            int direction = directions[ejectorIndex];
            int incLevel = incLevels[ejectorIndex];
            UpdatePower(ejectorPowerConsumerTypeIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, ejectorIndex, networkIndex, direction, incLevel);
        }
    }

    private static void UpdatePower(ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                                    ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int ejectorIndex,
                                    int networkIndex,
                                    int direction,
                                    int incLevel)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, direction, incLevel);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                                                                         ReadonlyArray<PowerConsumerType> powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] directions = _directions;
        ReadonlyArray<int> incLevels = _incLevels;
        ReadonlyArray<int> prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int ejectorIndex = 0; ejectorIndex < directions.Length; ejectorIndex++)
        {
            int direction = directions[ejectorIndex];
            int incLevel = incLevels[ejectorIndex];
            UpdatePowerConsumptionPerPrototype(ejectorPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               ejectorIndex,
                                               direction,
                                               incLevel);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                                                           ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                                           ReadonlyArray<int> prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int ejectorIndex,
                                                           int direction,
                                                           int incLevel)
    {
        int powerConsumerTypeIndex = ejectorPowerConsumerTypeIndexes[ejectorIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[ejectorIndex]] += GetPowerConsumption(powerConsumerType, direction, incLevel);
    }

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        OptimizedEjector[] optimizedEjectors = _optimizedEjectors;
        EjectorBulletData[] ejectorBulletDatas = _ejectorBulletDatas;
        int[] directions = _directions;
        EjectorComponent[] ejectors = planet.factorySystem.ejectorPool;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Ejector);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        short[] needsPatterns = subFactoryNeeds.NeedsPatterns;

        for (int i = 1; i < planet.factorySystem.ejectorCursor; i++)
        {
            if (!_ejectorIdToOptimizedEjectorIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            EjectorBulletData ejectorBulletData = ejectorBulletDatas[optimizedIndex];
            int direction = directions[optimizedIndex];
            ref OptimizedEjector optimizedEjector = ref optimizedEjectors[optimizedIndex];
            optimizedEjector.Save(ref ejectors[i],
                                  groupNeeds,
                                  componentsNeeds,
                                  needsPatterns,
                                  optimizedIndex,
                                  ejectorBulletData,
                                  direction);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        List<short> optimizedBulletItemId = [];
        List<int> ejectorNetworkIds = [];
        List<EjectorBulletData> ejectorBulletDatas = [];
        List<int> directions = [];
        List<int> incLevels = [];
        List<OptimizedEjector> optimizedEjectors = [];
        Dictionary<int, int> ejectorIdToOptimizedEjectorIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.Ejector);

        foreach (int ejectorIndex in subFactoryGraph.GetAllNodes()
                                                    .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                                    .Select(x => x.EntityTypeIndex.Index)
                                                    .OrderBy(x => x))
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorIndex];

            optimizedBulletItemId.Add(subFactoryProductionRegisterBuilder.AddConsume(ejector.bulletId).OptimizedItemIndex);
            int networkIndex = planet.powerSystem.consumerPool[ejector.pcId].networkId;
            ejectorNetworkIds.Add(networkIndex);
            ejectorBulletDatas.Add(new EjectorBulletData(ejector.bulletId, ejector.bulletCount, ejector.bulletInc));
            directions.Add(ejector.direction);
            incLevels.Add(ejector.incLevel);
            ejectorIdToOptimizedEjectorIndex.Add(ejector.id, optimizedEjectors.Count);
            optimizedEjectors.Add(new OptimizedEjector(in ejector));

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
            needsBuilder.AddNeeds(ejector.needs, [ejector.bulletId]);

            subFactoryPowerSystemBuilder.AddEjector(in ejector, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[ejector.entityId]);
        }

        _optimizedBulletItemId = universeStaticDataBuilder.DeduplicateArrayUnmanaged(optimizedBulletItemId);
        _ejectorNetworkIds = universeStaticDataBuilder.DeduplicateArrayUnmanaged(ejectorNetworkIds);
        _ejectorBulletDatas = ejectorBulletDatas.ToArray();
        _directions = directions.ToArray();
        _incLevels = universeStaticDataBuilder.DeduplicateArrayUnmanaged(incLevels);
        _optimizedEjectors = optimizedEjectors.ToArray();
        _ejectorIdToOptimizedEjectorIndex = ejectorIdToOptimizedEjectorIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(universeStaticDataBuilder);
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, int direction, int incLevel)
    {
        return powerConsumerType.GetRequiredEnergy(direction != 0, 1000 + Cargo.powerTable[incLevel]);
    }
}
