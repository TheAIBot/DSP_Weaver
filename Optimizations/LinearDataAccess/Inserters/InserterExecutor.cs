﻿using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal static class CargoPathMethods
{
    // Takes CargoPath instead of belt id
    public static int TryPickItemAtRear(OptimizedCargoPath cargoPath, int filter, int[]? needs, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
        int cargoIdAtRear = cargoPath.GetCargoIdAtRear();
        if (cargoIdAtRear == -1)
        {
            return 0;
        }
        int item = cargoPath.cargoContainer.cargoPool[cargoIdAtRear].item;
        stack = cargoPath.cargoContainer.cargoPool[cargoIdAtRear].stack;
        inc = cargoPath.cargoContainer.cargoPool[cargoIdAtRear].inc;
        if (filter != 0)
        {
            if (item == filter)
            {
                cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                return item;
            }
        }
        else
        {
            if (needs == null)
            {
                cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                return item;
            }
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i] == item)
                {
                    cargoPath.TryRemoveItemAtRear(cargoIdAtRear);
                    return item;
                }
            }
        }
        stack = 1;
        inc = 0;
        return 0;
    }

    // Takes CargoPath instead of belt id
    public static int GetItemIdAtRear(OptimizedCargoPath cargoPath)
    {
        int cargoIdAtRear = cargoPath.GetCargoIdAtRear();
        if (cargoIdAtRear == -1)
        {
            return 0;
        }
        return cargoPath.cargoContainer.cargoPool[cargoIdAtRear].item;
    }
}

internal record struct PickFromProducingPlant(OptimizedItemId[] Products, int[] Produced)
{
    public override string ToString()
    {
        return $"""
            PickFromProducingPlant
            \t{nameof(Products)}: {string.Join(", ", Products ?? [])}
            \t{nameof(Produced)}: {string.Join(", ", Produced ?? [])}
            """;
    }
}

internal record struct ConnectionBelts(OptimizedCargoPath? PickFrom, OptimizedCargoPath? InsertInto)
{
    public override string ToString()
    {
        return $"""
            ConnectionBelts
            \t{nameof(PickFrom)}: {PickFrom}, {PickFrom?.pathLength}
            \t{nameof(InsertInto)}: {InsertInto}, {InsertInto?.pathLength}
            """;
    }
}

internal record struct InsertIntoConsumingPlant(OptimizedItemId[]? Requires, int[] Served, int[] IncServed)
{
    public override string ToString()
    {
        return $"""
            InsertIntoConsumingPlant
            \t{nameof(Requires)}: {string.Join(", ", Requires ?? [])}
            \t{nameof(Served)}: {string.Join(", ", Served ?? [])}
            \t{nameof(IncServed)}: {string.Join(", ", IncServed ?? [])}
            """;
    }
}

internal sealed class InserterExecutor<T>
    where T : struct, IInserter<T>
{
    private T[] _optimizedInserters = null!;
    private OptimizedInserterStage[] _optimizedInserterStages = null!;
    private InserterGrade[] _inserterGrades = null!;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates = null!;
    public InserterConnections[] _inserterConnections = null!;
    public int[]?[] _inserterConnectionNeeds = null!;
    public PickFromProducingPlant[] _pickFromProducingPlants = null!;
    public ConnectionBelts[] _connectionBelts = null!;
    public InsertIntoConsumingPlant[] _insertIntoConsumingPlants = null!;
    public Dictionary<int, int> _inserterIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    private readonly NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    private readonly OptimizedFuelGenerator[][] _generatorSegmentToOptimizedFuelGenerators;
    private readonly OptimizedItemId[]?[]? _fuelNeeds;

    public int InserterCount => _optimizedInserters.Length;

    public InserterExecutor(NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates,
                            NetworkIdAndState<LabState>[] producingLabNetworkIdAndStates,
                            NetworkIdAndState<LabState>[] researchingLabNetworkIdAndStates,
                            OptimizedFuelGenerator[][] generatorSegmentToOptimizedFuelGenerators,
                            OptimizedItemId[]?[]? fuelNeeds)
    {
        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates;
        _producingLabNetworkIdAndStates = producingLabNetworkIdAndStates;
        _researchingLabNetworkIdAndStates = researchingLabNetworkIdAndStates;
        _generatorSegmentToOptimizedFuelGenerators = generatorSegmentToOptimizedFuelGenerators;
        _fuelNeeds = fuelNeeds;
    }

    public void GameTickInserters(PlanetFactory planet,
                                  int[] inserterPowerConsumerIndexes,
                                  PowerConsumerType[] powerConsumerTypes,
                                  long[] thisSubFactoryNetworkPowerConsumption)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;

        for (int inserterIndex = 0; inserterIndex < _inserterNetworkIdAndStates.Length; inserterIndex++)
        {
            ref OptimizedInserterStage optimizedInserterStage = ref _optimizedInserterStages[inserterIndex];
            ref NetworkIdAndState<InserterState> networkIdAndState = ref _inserterNetworkIdAndStates[inserterIndex];
            InserterState inserterState = (InserterState)networkIdAndState.State;
            if (inserterState != InserterState.Active)
            {
                if (inserterState == InserterState.InactivePickFrom)
                {
                    if (!IsObjectPickFromActive(inserterIndex))
                    {
                        UpdatePower(inserterPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, inserterIndex, networkIdAndState.Index, optimizedInserterStage);
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
                else if (inserterState == InserterState.InactiveInsertInto)
                {
                    if (!IsObjectInsertIntoActive(inserterIndex))
                    {
                        UpdatePower(inserterPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, inserterIndex, networkIdAndState.Index, optimizedInserterStage);
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
            }

            float power2 = networkServes[networkIdAndState.Index];
            ref T optimizedInserter = ref _optimizedInserters[inserterIndex];
            InserterGrade inserterGrade = _inserterGrades[optimizedInserter.grade];
            optimizedInserter.Update(planet,
                                     this,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     inserterGrade,
                                     ref optimizedInserterStage);

            UpdatePower(inserterPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, inserterIndex, networkIdAndState.Index, optimizedInserterStage);
        }
    }

    public void UpdatePower(int[] inserterPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        NetworkIdAndState<InserterState>[] inserterNetworkIdAndStates = _inserterNetworkIdAndStates;
        for (int j = 0; j < _optimizedInserters.Length; j++)
        {
            int networkIndex = inserterNetworkIdAndStates[j].Index;
            OptimizedInserterStage optimizedInserterStage = _optimizedInserterStages[j];
            UpdatePower(inserterPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, j, networkIndex, optimizedInserterStage);
        }
    }

    private static void UpdatePower(int[] inserterPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int inserterIndex,
                                    int networkIndex,
                                    OptimizedInserterStage optimizedInserterStage)
    {
        int powerConsumerTypeIndex = inserterPowerConsumerIndexes[inserterIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, optimizedInserterStage);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] inserterPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int inserterIndex = 0; inserterIndex < _optimizedInserters.Length; inserterIndex++)
        {
            OptimizedInserterStage optimizedInserterStage = _optimizedInserterStages[inserterIndex];
            UpdatePowerConsumptionPerPrototype(inserterPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               inserterIndex,
                                               optimizedInserterStage);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] inserterPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int inserterIndex,
                                                           OptimizedInserterStage optimizedInserterStage)
    {
        int powerConsumerTypeIndex = inserterPowerConsumerIndexes[inserterIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[inserterIndex]] += GetPowerConsumption(powerConsumerType, optimizedInserterStage);
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, OptimizedInserterStage stage)
    {
        return powerConsumerType.GetRequiredEnergy(stage == OptimizedInserterStage.Sending || stage == OptimizedInserterStage.Returning);
    }

    private bool IsObjectPickFromActive(int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].PickFrom;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return (AssemblerState)_assemblerNetworkIdAndStates[objectIndex.Index].State == AssemblerState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ProducingLab)
        {
            return (LabState)_producingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if pick from is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private bool IsObjectInsertIntoActive(int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].InsertInto;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return (AssemblerState)_assemblerNetworkIdAndStates[objectIndex.Index].State == AssemblerState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ProducingLab)
        {
            return (LabState)_producingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ResearchingLab)
        {
            return (LabState)_researchingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if insert into is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    public int PickFrom(PlanetFactory planet,
                        ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                        int inserterIndex,
                        int offset,
                        int filter,
                        int[]? needs,
                        out byte stack,
                        out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = _inserterConnections[inserterIndex].PickFrom;
        int objectIndex = typedObjectIndex.Index;

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ConnectionBelts connectionBelts = _connectionBelts[inserterIndex];
            if (connectionBelts.PickFrom == null)
            {
                throw new InvalidOperationException($"{nameof(connectionBelts.PickFrom)} was null.");
            }

            if (needs == null)
            {
                if (filter != 0)
                {
                    return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, filter, out stack, out inc);
                }
                return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, out stack, out inc);
            }
            return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, filter, needs, out stack, out inc);
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = (AssemblerState)_assemblerNetworkIdAndStates[objectIndex].State;
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveOutputFull)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactivePickFrom;
                return 0;
            }

            PickFromProducingPlant producingPlant = _pickFromProducingPlants[inserterIndex];
            OptimizedItemId[] products = producingPlant.Products;
            int[] produced = producingPlant.Produced;

            int num = products.Length;
            switch (num)
            {
                case 1:
                    if (produced[0] > 0 && products[0].ItemIndex > 0 && (filter == 0 || filter == products[0].ItemIndex) && (needs == null || needs[0] == products[0].ItemIndex || needs[1] == products[0].ItemIndex || needs[2] == products[0].ItemIndex || needs[3] == products[0].ItemIndex || needs[4] == products[0].ItemIndex || needs[5] == products[0].ItemIndex))
                    {
                        produced[0]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[0].ItemIndex;
                    }
                    break;
                case 2:
                    if ((filter == products[0].ItemIndex || filter == 0) && produced[0] > 0 && products[0].ItemIndex > 0 && (needs == null || needs[0] == products[0].ItemIndex || needs[1] == products[0].ItemIndex || needs[2] == products[0].ItemIndex || needs[3] == products[0].ItemIndex || needs[4] == products[0].ItemIndex || needs[5] == products[0].ItemIndex))
                    {
                        produced[0]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[0].ItemIndex;
                    }
                    if ((filter == products[1].ItemIndex || filter == 0) && produced[1] > 0 && products[1].ItemIndex > 0 && (needs == null || needs[0] == products[1].ItemIndex || needs[1] == products[1].ItemIndex || needs[2] == products[1].ItemIndex || needs[3] == products[1].ItemIndex || needs[4] == products[1].ItemIndex || needs[5] == products[1].ItemIndex))
                    {
                        produced[1]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[1].ItemIndex;
                    }
                    break;
                default:
                    {
                        for (int i = 0; i < num; i++)
                        {
                            if ((filter == products[i].ItemIndex || filter == 0) && produced[i] > 0 && products[i].ItemIndex > 0 && (needs == null || needs[0] == products[i].ItemIndex || needs[1] == products[i].ItemIndex || needs[2] == products[i].ItemIndex || needs[3] == products[i].ItemIndex || needs[4] == products[i].ItemIndex || needs[5] == products[i].ItemIndex))
                            {
                                produced[i]--;
                                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                                return products[i].ItemIndex;
                            }
                        }
                        break;
                    }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[objectIndex];
            int bulletId = ejector.bulletId;
            int bulletCount = ejector.bulletCount;
            if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && (needs == null || needs[0] == bulletId || needs[1] == bulletId || needs[2] == bulletId || needs[3] == bulletId || needs[4] == bulletId || needs[5] == bulletId))
            {
                ejector.TakeOneBulletUnsafe(out inc);
                return bulletId;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            ref SiloComponent silo = ref planet.factorySystem.siloPool[objectIndex];
            int bulletId2 = silo.bulletId;
            int bulletCount2 = silo.bulletCount;
            if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && (needs == null || needs[0] == bulletId2 || needs[1] == bulletId2 || needs[2] == bulletId2 || needs[3] == bulletId2 || needs[4] == bulletId2 || needs[5] == bulletId2))
            {
                silo.TakeOneBulletUnsafe(out inc);
                return bulletId2;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            int inc2;
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            StorageComponent storageComponent2 = storageComponent;
            if (storageComponent != null)
            {
                storageComponent = storageComponent.topStorage;
                while (storageComponent != null)
                {
                    if (storageComponent.lastEmptyItem != 0 && storageComponent.lastEmptyItem != filter)
                    {
                        int itemId = filter;
                        int count = 1;
                        bool flag;
                        if (needs == null)
                        {
                            storageComponent.TakeTailItems(ref itemId, ref count, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                            inc = (byte)inc2;
                            flag = count == 1;
                        }
                        else
                        {
                            bool flag2 = storageComponent.TakeTailItems(ref itemId, ref count, needs, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                            inc = (byte)inc2;
                            flag = count == 1 || flag2;
                        }
                        if (count == 1)
                        {
                            storageComponent.lastEmptyItem = -1;
                            return itemId;
                        }
                        if (!flag)
                        {
                            storageComponent.lastEmptyItem = filter;
                        }
                    }
                    if (storageComponent == storageComponent2)
                    {
                        break;
                    }
                    storageComponent = storageComponent.previousStorage;
                    continue;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            int inc2;
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (stationComponent != null)
            {
                int _itemId = filter;
                int _count = 1;
                if (needs == null)
                {
                    stationComponent.TakeItem(ref _itemId, ref _count, out inc2);
                    inc = (byte)inc2;
                }
                else
                {
                    stationComponent.TakeItem(ref _itemId, ref _count, needs, out inc2);
                    inc = (byte)inc2;
                }
                if (_count == 1)
                {
                    return _itemId;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ProducingLab)
        {
            LabState labState = (LabState)_producingLabNetworkIdAndStates[objectIndex].State;
            if (labState != LabState.Active &&
                labState != LabState.InactiveOutputFull)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactivePickFrom;
                return 0;
            }

            PickFromProducingPlant producingPlant = _pickFromProducingPlants[inserterIndex];
            OptimizedItemId[] products2 = producingPlant.Products;
            int[] produced2 = producingPlant.Produced;
            if (products2 == null || produced2 == null)
            {
                return 0;
            }
            for (int j = 0; j < products2.Length; j++)
            {
                if (produced2[j] > 0 && products2[j].ItemIndex > 0 && (filter == 0 || filter == products2[j].ItemIndex) && (needs == null || needs[0] == products2[j].ItemIndex || needs[1] == products2[j].ItemIndex || needs[2] == products2[j].ItemIndex || needs[3] == products2[j].ItemIndex || needs[4] == products2[j].ItemIndex || needs[5] == products2[j].ItemIndex))
                {
                    produced2[j]--;
                    _producingLabNetworkIdAndStates[objectIndex].State = (int)LabState.Active;
                    return products2[j].ItemIndex;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.FuelPowerGenerator)
        {
            ref OptimizedFuelGenerator fuelGenerator = ref _generatorSegmentToOptimizedFuelGenerators[offset][objectIndex];
            if (fuelGenerator.fuelCount <= 8)
            {
                int result = fuelGenerator.PickFuelFrom(filter, out int inc2).ItemIndex;
                inc = (byte)inc2;
                return result;
            }
            return 0;
        }

        return 0;
    }

    public int InsertInto(PlanetFactory planet,
                          ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                          int inserterIndex,
                          int[]? entityNeeds,
                          int offset,
                          int itemId,
                          byte itemCount,
                          byte itemInc,
                          out byte remainInc)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = _inserterConnections[inserterIndex].InsertInto;
        int objectIndex = typedObjectIndex.Index;

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ConnectionBelts connectionBelts = _connectionBelts[inserterIndex];
            if (connectionBelts.InsertInto == null)
            {
                throw new InvalidOperationException($"{nameof(connectionBelts.InsertInto)} was null.");
            }

            if (connectionBelts.InsertInto.TryInsertItem(offset, itemId, itemCount, itemInc))
            {
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = (AssemblerState)_assemblerNetworkIdAndStates[objectIndex].State;
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveInputMissing)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactiveInsertInto;
                return 0;
            }

            if (entityNeeds == null)
            {
                throw new InvalidOperationException($"Array from {nameof(entityNeeds)} should only be null if assembler is inactive which the above if statement should have caught.");
            }
            InsertIntoConsumingPlant insertIntoConsumingPlant = _insertIntoConsumingPlants[inserterIndex];
            OptimizedItemId[]? requires = insertIntoConsumingPlant.Requires;
            int[] served = insertIntoConsumingPlant.Served;
            int[] incServed = insertIntoConsumingPlant.IncServed;
            if (requires == null)
            {
                throw new InvalidOperationException($"{nameof(requires)} was null.");
            }

            int num = requires.Length;
            if (0 < num && requires[0].ItemIndex == itemId)
            {
                served[0] += itemCount;
                incServed[0] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1].ItemIndex == itemId)
            {
                served[1] += itemCount;
                incServed[1] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2].ItemIndex == itemId)
            {
                served[2] += itemCount;
                incServed[2] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3].ItemIndex == itemId)
            {
                served[3] += itemCount;
                incServed[3] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4].ItemIndex == itemId)
            {
                served[4] += itemCount;
                incServed[4] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5].ItemIndex == itemId)
            {
                served[5] += itemCount;
                incServed[5] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            if (entityNeeds == null)
            {
                throw new InvalidOperationException("Need was null for active ejector.");
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.ejectorPool[objectIndex].bulletId == itemId)
            {
                planet.factorySystem.ejectorPool[objectIndex].bulletCount += itemCount;
                planet.factorySystem.ejectorPool[objectIndex].bulletInc += itemInc;
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            if (entityNeeds == null)
            {
                throw new InvalidOperationException("Need was null for active silo.");
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.siloPool[objectIndex].bulletId == itemId)
            {
                planet.factorySystem.siloPool[objectIndex].bulletCount += itemCount;
                planet.factorySystem.siloPool[objectIndex].bulletInc += itemInc;
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ProducingLab)
        {
            LabState labState = (LabState)_producingLabNetworkIdAndStates[objectIndex].State;
            if (labState != LabState.Active &&
                labState != LabState.InactiveInputMissing)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactiveInsertInto;
                return 0;
            }

            if (entityNeeds == null)
            {
                throw new InvalidOperationException($"Array from {nameof(entityNeeds)} should only be null if producing lab is inactive which the above if statement should have caught.");
            }
            InsertIntoConsumingPlant insertIntoConsumingPlant = _insertIntoConsumingPlants[inserterIndex];
            OptimizedItemId[]? requires2 = insertIntoConsumingPlant.Requires;
            int[] served = insertIntoConsumingPlant.Served;
            int[] incServed = insertIntoConsumingPlant.IncServed;
            if (requires2 == null)
            {
                return 0;
            }
            int num3 = requires2.Length;
            for (int i = 0; i < num3; i++)
            {
                if (requires2[i].ItemIndex == itemId)
                {
                    served[i] += itemCount;
                    incServed[i] += itemInc;
                    remainInc = 0;
                    _producingLabNetworkIdAndStates[objectIndex].State = (int)LabState.Active;
                    return itemCount;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.ResearchingLab)
        {
            LabState labState = (LabState)_researchingLabNetworkIdAndStates[objectIndex].State;
            if (labState != LabState.Active &&
                labState != LabState.InactiveInputMissing)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactiveInsertInto;
                return 0;
            }

            if (entityNeeds == null)
            {
                throw new InvalidOperationException($"Array from {nameof(entityNeeds)} should only be null if researching lab is inactive which the above if statement should have caught.");
            }
            InsertIntoConsumingPlant insertIntoConsumingPlant = _insertIntoConsumingPlants[inserterIndex];
            int[] matrixServed = insertIntoConsumingPlant.Served;
            int[] matrixIncServed = insertIntoConsumingPlant.IncServed;
            if (matrixServed == null)
            {
                return 0;
            }
            int num2 = itemId - 6001;
            if (num2 >= 0 && num2 < 6)
            {
                matrixServed[num2] += 3600 * itemCount;
                matrixIncServed[num2] += 3600 * itemInc;
                remainInc = 0;
                _researchingLabNetworkIdAndStates[objectIndex].State = (int)LabState.Active;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            while (storageComponent != null)
            {
                if (storageComponent.lastFullItem != itemId)
                {
                    int num4 = ((planet.entityPool[storageComponent.entityId].battleBaseId != 0) ? storageComponent.AddItemFilteredBanOnly(itemId, itemCount, itemInc, out var remainInc2) : storageComponent.AddItem(itemId, itemCount, itemInc, out remainInc2, useBan: true));
                    remainInc = (byte)remainInc2;
                    if (num4 == itemCount)
                    {
                        storageComponent.lastFullItem = -1;
                    }
                    else
                    {
                        storageComponent.lastFullItem = itemId;
                    }
                    if (num4 != 0 || storageComponent.nextStorage == null)
                    {
                        return num4;
                    }
                }
                storageComponent = storageComponent.nextStorage;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            if (entityNeeds == null)
            {
                throw new InvalidOperationException("Need was null for active station.");
            }
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
            {
                stationComponent.warperCount += itemCount;
                remainInc = 0;
                return itemCount;
            }
            StationStore[] storage = stationComponent.storage;
            for (int j = 0; j < entityNeeds.Length && j < storage.Length; j++)
            {
                if (entityNeeds[j] == itemId && storage[j].itemId == itemId)
                {
                    storage[j].count += itemCount;
                    storage[j].inc += itemInc;
                    remainInc = 0;
                    return itemCount;
                }
            }

            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.FuelPowerGenerator)
        {
            ref OptimizedFuelGenerator fuelGenerator = ref _generatorSegmentToOptimizedFuelGenerators[offset][objectIndex];
            if (itemId == fuelGenerator.fuelId.ItemIndex)
            {
                if (fuelGenerator.fuelCount < 10)
                {
                    ref short fuelCount = ref fuelGenerator.fuelCount;
                    fuelCount += itemCount;
                    ref short fuelInc = ref fuelGenerator.fuelInc;
                    fuelInc += itemInc;
                    remainInc = 0;
                    return itemCount;
                }
                return 0;
            }
            if (fuelGenerator.fuelId.ItemIndex == 0)
            {
                if (_fuelNeeds == null)
                {
                    throw new InvalidOperationException($"{nameof(_fuelNeeds)} was null when inserter attempted to insert into {nameof(EntityType.FuelPowerGenerator)}.");
                }
                OptimizedItemId[]? array = _fuelNeeds[fuelGenerator.fuelMask];
                if (array == null || array.Length == 0)
                {
                    return 0;
                }
                for (int k = 0; k < array.Length; k++)
                {
                    if (array[k].ItemIndex == itemId)
                    {
                        fuelGenerator.SetNewFuel(array[k], itemCount, itemInc);
                        remainInc = 0;
                        return itemCount;
                    }
                }
                return 0;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Splitter)
        {
            throw new InvalidOperationException("Assumption that sorters can not interact with splitters is incorrect.");
        }

        return 0;
    }

    public void Save(PlanetFactory planet)
    {
        InserterComponent[] inserters = planet.factorySystem.inserterPool;
        T[] optimizedInserters = _optimizedInserters;
        OptimizedInserterStage[] optimizedInserterStages = _optimizedInserterStages;
        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            if (!_inserterIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedInserters[optimizedIndex].Save(ref inserters[i], ToEInserterStage(optimizedInserterStages[optimizedIndex]));
        }
    }

    public void Initialize(PlanetFactory planet,
                           OptimizedSubFactory subFactory,
                           Graph subFactoryGraph,
                           Func<InserterComponent, bool> inserterSelector,
                           OptimizedPowerSystemInserterBuilder optimizedPowerSystemInserterBuilder,
                           BeltExecutor beltExecutor)
    {
        List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates = [];
        List<InserterConnections> inserterConnections = [];
        List<int[]?> inserterConnectionNeeds = [];
        List<InserterGrade> inserterGrades = [];
        Dictionary<InserterGrade, int> inserterGradeToIndex = [];
        List<T> optimizedInserters = [];
        List<OptimizedInserterStage> optimizedInserterStages = [];
        List<PickFromProducingPlant> pickFromProducingPlants = [];
        List<ConnectionBelts> connectionBelts = [];
        List<InsertIntoConsumingPlant> insertIntoConsumingPlants = [];
        Dictionary<int, int> inserterIdToOptimizedIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

        foreach (int inserterIndex in subFactoryGraph.GetAllNodes()
                                                     .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                                     .Select(x => x.EntityTypeIndex.Index)
                                                     .OrderBy(x => x))
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[inserterIndex];
            if (!inserterSelector(inserter))
            {
                continue;
            }

            InserterState? inserterState = null;
            TypedObjectIndex pickFrom = new TypedObjectIndex(EntityType.None, 0);
            if (inserter.pickTarget != 0)
            {
                pickFrom = subFactory.GetAsGranularTypedObjectIndex(inserter.pickTarget, planet);
                if (pickFrom.EntityType == EntityType.None)
                {
                    continue;
                }
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
                continue;
            }

            TypedObjectIndex insertInto = new TypedObjectIndex(EntityType.None, 0);
            int[]? insertIntoNeeds = null;
            if (inserter.insertTarget != 0)
            {
                insertInto = subFactory.GetAsGranularTypedObjectIndex(inserter.insertTarget, planet);
                if (insertInto.EntityType == EntityType.None)
                {
                    continue;
                }

                insertIntoNeeds = OptimizedSubFactory.GetEntityNeeds(planet, inserter.insertTarget);
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
                continue;
            }

            if (pickFrom.EntityType == EntityType.None || insertInto.EntityType == EntityType.None)
            {
                continue;
            }

            inserterIdToOptimizedIndex.Add(inserterIndex, optimizedInserters.Count);
            int networkIndex = planet.powerSystem.consumerPool[inserter.pcId].networkId;
            inserterNetworkIdAndStates.Add(new NetworkIdAndState<InserterState>((int)(inserterState ?? InserterState.Active), networkIndex));
            inserterConnectionNeeds.Add(insertIntoNeeds);
            optimizedPowerSystemInserterBuilder.AddInserter(ref inserter, networkIndex);

            InserterGrade inserterGrade;

            // Need to check when i need to update this again.
            // Probably bi direction is related to some research.
            // Probably the same for stack output.
            byte b = (byte)GameMain.history.inserterStackCountObsolete;
            byte b2 = (byte)GameMain.history.inserterStackInput;
            byte stackOutput = (byte)GameMain.history.inserterStackOutput;
            bool inserterBidirectional = GameMain.history.inserterBidirectional;
            int delay = b > 1 ? 110000 : 0;
            int delay2 = b2 > 1 ? 40000 : 0;

            if (inserter.grade == 3)
            {
                inserterGrade = new InserterGrade(delay, b, 1, false);
            }
            else if (inserter.grade == 4)
            {
                inserterGrade = new InserterGrade(delay2, b2, stackOutput, inserterBidirectional);
            }
            else
            {
                inserterGrade = new InserterGrade(0, 1, 1, false);
            }

            if (!inserterGradeToIndex.TryGetValue(inserterGrade, out int inserterGradeIndex))
            {
                inserterGradeIndex = inserterGrades.Count;
                inserterGrades.Add(inserterGrade);
                inserterGradeToIndex.Add(inserterGrade, inserterGradeIndex);
            }

            OptimizedCargoPath? pickFromBelt = null;
            int pickFromOffset = inserter.pickOffset;
            if (pickFrom.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[pickFrom.Index];
                if (beltExecutor.TryOptimizedCargoPath(planet, pickFrom.Index, out pickFromBelt))
                {
                    pickFromOffset = GetCorrectedPickOffset(inserter.pickOffset, ref belt, pickFromBelt);
                }
                pickFromOffset += belt.pivotOnPath;
            }
            else if (pickFrom.EntityType == EntityType.FuelPowerGenerator)
            {
                if (pickFromOffset > 0 && planet.powerSystem.genPool[pickFromOffset].id == pickFromOffset)
                {
                    OptimizedFuelGeneratorLocation optimizedFuelGeneratorLocation = optimizedPowerSystemInserterBuilder.GetOptimizedFuelGeneratorLocation(pickFromOffset);
                    pickFromOffset = optimizedFuelGeneratorLocation.SegmentIndex;
                    pickFrom = new TypedObjectIndex(pickFrom.EntityType, optimizedFuelGeneratorLocation.Index);
                }
            }

            OptimizedCargoPath? insertIntoBelt = null;
            int insertIntoOffset = inserter.insertOffset;
            if (insertInto.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[insertInto.Index];
                if (beltExecutor.TryOptimizedCargoPath(planet, insertInto.Index, out insertIntoBelt))
                {
                    insertIntoOffset = GetCorrectedInsertOffset(inserter.insertOffset, ref belt, insertIntoBelt);
                }
                insertIntoOffset += belt.pivotOnPath;
            }
            else if (insertInto.EntityType == EntityType.FuelPowerGenerator)
            {
                if (planet.powerSystem.genPool[insertInto.Index].id == insertInto.Index)
                {
                    OptimizedFuelGeneratorLocation optimizedFuelGeneratorLocation = optimizedPowerSystemInserterBuilder.GetOptimizedFuelGeneratorLocation(insertInto.Index);
                    insertIntoOffset = optimizedFuelGeneratorLocation.SegmentIndex;
                    insertInto = new TypedObjectIndex(insertInto.EntityType, optimizedFuelGeneratorLocation.Index);
                }
            }

            inserterConnections.Add(new InserterConnections(pickFrom, insertInto));
            optimizedInserters.Add(default(T).Create(in inserter, pickFromOffset, insertIntoOffset, inserterGradeIndex));
            optimizedInserterStages.Add(ToOptimizedInserterStage(inserter.stage));
            connectionBelts.Add(new ConnectionBelts(pickFromBelt, insertIntoBelt));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[inserter.entityId]);

            if (pickFrom.EntityType == EntityType.Assembler)
            {
                ref readonly OptimizedAssembler assembler = ref subFactory._assemblerExecutor._optimizedAssemblers[pickFrom.Index];
                ref readonly AssemblerRecipe assemblerRecipe = ref subFactory._assemblerExecutor._assemblerRecipes[subFactory._assemblerExecutor._assemblerRecipeIndexes[pickFrom.Index]];
                pickFromProducingPlants.Add(new PickFromProducingPlant(assemblerRecipe.Products, assembler.produced));
            }
            else if (pickFrom.EntityType == EntityType.ProducingLab)
            {
                ref readonly OptimizedProducingLab lab = ref subFactory._optimizedProducingLabs[pickFrom.Index];
                ref readonly ProducingLabRecipe producingLabRecipe = ref subFactory._producingLabRecipes[lab.producingLabRecipeIndex];
                pickFromProducingPlants.Add(new PickFromProducingPlant(producingLabRecipe.Products, lab.produced));
            }
            else
            {
                pickFromProducingPlants.Add(default);
            }

            if (insertInto.EntityType == EntityType.Assembler)
            {
                ref readonly OptimizedAssembler assembler = ref subFactory._assemblerExecutor._optimizedAssemblers[insertInto.Index];
                ref readonly AssemblerRecipe assemblerRecipe = ref subFactory._assemblerExecutor._assemblerRecipes[subFactory._assemblerExecutor._assemblerRecipeIndexes[insertInto.Index]];
                ref readonly AssemblerNeeds assemblerNeeds = ref subFactory._assemblerExecutor._assemblersNeeds[insertInto.Index];
                insertIntoConsumingPlants.Add(new InsertIntoConsumingPlant(assemblerRecipe.Requires, assemblerNeeds.served, assembler.incServed));
            }
            else if (insertInto.EntityType == EntityType.ProducingLab)
            {
                ref readonly OptimizedProducingLab lab = ref subFactory._optimizedProducingLabs[insertInto.Index];
                ProducingLabRecipe producingLabRecipe = subFactory._producingLabRecipes[lab.producingLabRecipeIndex];
                insertIntoConsumingPlants.Add(new InsertIntoConsumingPlant(producingLabRecipe.Requires, lab.served, lab.incServed));
            }
            else if (insertInto.EntityType == EntityType.ResearchingLab)
            {
                ref readonly OptimizedResearchingLab lab = ref subFactory._optimizedResearchingLabs[insertInto.Index];
                insertIntoConsumingPlants.Add(new InsertIntoConsumingPlant(null, lab.matrixServed, lab.matrixIncServed));
            }
            else
            {
                insertIntoConsumingPlants.Add(default);
            }
        }

        _inserterNetworkIdAndStates = inserterNetworkIdAndStates.ToArray();
        _inserterConnections = inserterConnections.ToArray();
        _inserterConnectionNeeds = inserterConnectionNeeds.ToArray();
        _inserterGrades = inserterGrades.ToArray();
        _optimizedInserters = optimizedInserters.ToArray();
        _optimizedInserterStages = optimizedInserterStages.ToArray();
        _pickFromProducingPlants = pickFromProducingPlants.ToArray();
        _connectionBelts = connectionBelts.ToArray();
        _insertIntoConsumingPlants = insertIntoConsumingPlants.ToArray();
        _inserterIdToOptimizedIndex = inserterIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private void Print(int inserterIndex)
    {
        WeaverFixes.Logger.LogMessage(_optimizedInserters[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterNetworkIdAndStates[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterConnections[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterConnectionNeeds[inserterIndex]?.ToString());
        WeaverFixes.Logger.LogMessage(_pickFromProducingPlants[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_connectionBelts[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_insertIntoConsumingPlants[inserterIndex].ToString());
    }

    private static OptimizedInserterStage ToOptimizedInserterStage(EInserterStage inserterStage) => inserterStage switch
    {
        EInserterStage.Picking => OptimizedInserterStage.Picking,
        EInserterStage.Sending => OptimizedInserterStage.Sending,
        EInserterStage.Inserting => OptimizedInserterStage.Inserting,
        EInserterStage.Returning => OptimizedInserterStage.Returning,
        _ => throw new ArgumentOutOfRangeException(nameof(inserterStage))
    };

    private static EInserterStage ToEInserterStage(OptimizedInserterStage inserterStage) => inserterStage switch
    {
        OptimizedInserterStage.Picking => EInserterStage.Picking,
        OptimizedInserterStage.Sending => EInserterStage.Sending,
        OptimizedInserterStage.Inserting => EInserterStage.Inserting,
        OptimizedInserterStage.Returning => EInserterStage.Returning,
        _ => throw new ArgumentOutOfRangeException(nameof(inserterStage))
    };

    private static int GetCorrectedPickOffset(int pickOffset, ref readonly BeltComponent belt, OptimizedCargoPath cargoPath)
    {
        int num = belt.segPivotOffset + belt.segIndex;
        int num2 = num + pickOffset;
        if (num2 < 4)
        {
            num2 = 4;
        }
        if (num2 + 5 >= cargoPath.pathLength)
        {
            num2 = cargoPath.pathLength - 5 - 1;
        }
        return (short)(num2 - num);
    }

    private static int GetCorrectedInsertOffset(int insertOffset, ref readonly BeltComponent belt, OptimizedCargoPath cargoPath)
    {
        int num3 = belt.segPivotOffset + belt.segIndex;
        int num4 = num3 + insertOffset;
        if (num4 < 4)
        {
            num4 = 4;
        }
        if (num4 + 5 >= cargoPath.pathLength)
        {
            num4 = cargoPath.pathLength - 5 - 1;
        }
        return (short)(num4 - num3);
    }
}
