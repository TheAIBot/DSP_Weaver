using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal record struct PickFromProducingPlant(int[] Products, int[] Produced);

internal record struct ConnectionBelts(CargoPath PickFrom, CargoPath InsertInto);

internal record struct InsertIntoConsumingPlant(int[] Requires, int[] Served, int[] IncServed);

internal sealed class InserterExecutor<T>
    where T : struct, IInserter<T>
{
    private T[] _optimizedInserters;
    private OptimizedInserterStage[] _optimizedInserterStages;
    private InserterGrade[] _inserterGrades;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates;
    public InserterConnections[] _inserterConnections;
    public int[][] _inserterConnectionNeeds;
    public PickFromProducingPlant[] _pickFromProducingPlants;
    public ConnectionBelts[] _connectionBelts;
    public InsertIntoConsumingPlant[] _insertIntoConsumingPlants;
    public Dictionary<int, int> _inserterIdToOptimizedIndex;

    private readonly NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;

    public int InserterCount => _optimizedInserters.Length;

    public InserterExecutor(NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates,
        NetworkIdAndState<LabState>[] producingLabNetworkIdAndStates,
        NetworkIdAndState<LabState>[] researchingLabNetworkIdAndStates)
    {
        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates;
        _producingLabNetworkIdAndStates = producingLabNetworkIdAndStates;
        _researchingLabNetworkIdAndStates = researchingLabNetworkIdAndStates;
    }

    public T Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return default(T).Create(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public void GameTickInserters(PlanetFactory planet)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;

        for (int inserterIndex = 0; inserterIndex < _inserterNetworkIdAndStates.Length; inserterIndex++)
        {
            ref NetworkIdAndState<InserterState> networkIdAndState = ref _inserterNetworkIdAndStates[inserterIndex];
            InserterState inserterState = (InserterState)networkIdAndState.State;
            if (inserterState != InserterState.Active)
            {
                if (inserterState == InserterState.InactiveNoInserter ||
                    inserterState == InserterState.InactiveNotCompletelyConnected)
                {
                    continue;
                }
                else if (inserterState == InserterState.InactivePickFrom)
                {
                    if (!IsObjectPickFromActive(inserterIndex))
                    {
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
                else if (inserterState == InserterState.InactiveInsertInto)
                {
                    if (!IsObjectInsertIntoActive(inserterIndex))
                    {
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
            }

            float power2 = networkServes[networkIdAndState.Index];
            ref T optimizedInserter = ref _optimizedInserters[inserterIndex];
            InserterGrade inserterGrade = _inserterGrades[optimizedInserter.grade];
            ref OptimizedInserterStage optimizedInserterStage = ref _optimizedInserterStages[inserterIndex];
            optimizedInserter.Update(planet,
                                     this,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     inserterGrade,
                                     ref optimizedInserterStage);
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
            int powerConsumerTypeIndex = inserterPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            OptimizedInserterStage optimizedInserterStage = _optimizedInserterStages[j];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, optimizedInserterStage);
        }
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType, OptimizedInserterStage stage)
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
                        int[] needs,
                        out byte stack,
                        out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = _inserterConnections[inserterIndex].PickFrom;
        int objectIndex = typedObjectIndex.Index;
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ConnectionBelts connectionBelts = _connectionBelts[inserterIndex];
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
            int[] products = producingPlant.Products;
            int[] produced = producingPlant.Produced;

            int num = products.Length;
            switch (num)
            {
                case 1:
                    if (produced[0] > 0 && products[0] > 0 && (filter == 0 || filter == products[0]) && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        produced[0]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[0];
                    }
                    break;
                case 2:
                    if ((filter == products[0] || filter == 0) && produced[0] > 0 && products[0] > 0 && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        produced[0]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[0];
                    }
                    if ((filter == products[1] || filter == 0) && produced[1] > 0 && products[1] > 0 && (needs == null || needs[0] == products[1] || needs[1] == products[1] || needs[2] == products[1] || needs[3] == products[1] || needs[4] == products[1] || needs[5] == products[1]))
                    {
                        produced[1]--;
                        _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                        return products[1];
                    }
                    break;
                default:
                    {
                        for (int i = 0; i < num; i++)
                        {
                            if ((filter == products[i] || filter == 0) && produced[i] > 0 && products[i] > 0 && (needs == null || needs[0] == products[i] || needs[1] == products[i] || needs[2] == products[i] || needs[3] == products[i] || needs[4] == products[i] || needs[5] == products[i]))
                            {
                                produced[i]--;
                                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                                return products[i];
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
                        bool flag = false;
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
            int[] products2 = producingPlant.Products;
            int[] produced2 = producingPlant.Produced;
            if (products2 == null || produced2 == null)
            {
                return 0;
            }
            for (int j = 0; j < products2.Length; j++)
            {
                if (produced2[j] > 0 && products2[j] > 0 && (filter == 0 || filter == products2[j]) && (needs == null || needs[0] == products2[j] || needs[1] == products2[j] || needs[2] == products2[j] || needs[3] == products2[j] || needs[4] == products2[j] || needs[5] == products2[j]))
                {
                    produced2[j]--;
                    _producingLabNetworkIdAndStates[objectIndex].State = (int)LabState.Active;
                    return products2[j];
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            ref PowerGeneratorComponent powerGenerator = ref planet.powerSystem.genPool[offset];
            int inc2;
            if (offset > 0 && planet.powerSystem.genPool[offset].id == offset)
            {
                if (planet.powerSystem.genPool[offset].fuelCount <= 8)
                {
                    int result = planet.powerSystem.genPool[objectIndex].PickFuelFrom(filter, out inc2);
                    inc = (byte)inc2;
                    return result;
                }
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
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ConnectionBelts connectionBelts = _connectionBelts[inserterIndex];
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
            int[] requires = insertIntoConsumingPlant.Requires;
            int[] served = insertIntoConsumingPlant.Served;
            int[] incServed = insertIntoConsumingPlant.IncServed;
            int num = requires.Length;
            if (0 < num && requires[0] == itemId)
            {
                served[0] += itemCount;
                incServed[0] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1] == itemId)
            {
                served[1] += itemCount;
                incServed[1] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2] == itemId)
            {
                served[2] += itemCount;
                incServed[2] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3] == itemId)
            {
                served[3] += itemCount;
                incServed[3] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4] == itemId)
            {
                served[4] += itemCount;
                incServed[4] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5] == itemId)
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
                return 0;
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
                return 0;
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
            int[] requires2 = insertIntoConsumingPlant.Requires;
            int[] served = insertIntoConsumingPlant.Served;
            int[] incServed = insertIntoConsumingPlant.IncServed;
            if (requires2 == null)
            {
                return 0;
            }
            int num3 = requires2.Length;
            for (int i = 0; i < num3; i++)
            {
                if (requires2[i] == itemId)
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
                    int num4 = 0;
                    num4 = ((planet.entityPool[storageComponent.entityId].battleBaseId != 0) ? storageComponent.AddItemFilteredBanOnly(itemId, itemCount, itemInc, out var remainInc2) : storageComponent.AddItem(itemId, itemCount, itemInc, out remainInc2, useBan: true));
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
                return 0;
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
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            PowerGeneratorComponent[] genPool = planet.powerSystem.genPool;
            ref PowerGeneratorComponent powerGenerator = ref genPool[objectIndex];
            if (itemId == powerGenerator.fuelId)
            {
                if (powerGenerator.fuelCount < 10)
                {
                    ref short fuelCount = ref powerGenerator.fuelCount;
                    fuelCount += itemCount;
                    ref short fuelInc = ref powerGenerator.fuelInc;
                    fuelInc += itemInc;
                    remainInc = 0;
                    return itemCount;
                }
                return 0;
            }
            if (powerGenerator.fuelId == 0)
            {
                int[] array = ItemProto.fuelNeeds[powerGenerator.fuelMask];
                if (array == null || array.Length == 0)
                {
                    return 0;
                }
                for (int k = 0; k < array.Length; k++)
                {
                    if (array[k] == itemId)
                    {
                        powerGenerator.SetNewFuel(itemId, itemCount, itemInc);
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
            switch (offset)
            {
                case 0:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltA, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 1:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltB, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 2:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltC, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 3:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltD, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
            }
            return 0;
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
                           OptimizedPowerSystemInserterBuilder optimizedPowerSystemInserterBuilder)
    {
        List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates = [];
        List<InserterConnections> inserterConnections = [];
        List<int[]> inserterConnectionNeeds = [];
        List<InserterGrade> inserterGrades = [];
        Dictionary<InserterGrade, int> inserterGradeToIndex = [];
        List<T> optimizedInserters = [];
        List<OptimizedInserterStage> optimizedInserterStages = [];
        List<PickFromProducingPlant> pickFromProducingPlants = [];
        List<ConnectionBelts> connectionBelts = [];
        List<InsertIntoConsumingPlant> insertIntoConsumingPlants = [];
        Dictionary<int, int> inserterIdToOptimizedIndex = [];

        foreach (int inserterIndex in subFactoryGraph.GetAllNodes()
                                                     .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                                     .Select(x => x.EntityTypeIndex.Index)
                                                     .OrderBy(x => x))
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[inserterIndex];
            if (inserter.id != inserterIndex || !inserterSelector(inserter))
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
            int[] insertIntoNeeds = null;
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
            inserterConnections.Add(new InserterConnections(pickFrom, insertInto));
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

            CargoPath pickFromBelt = null;
            int pickFromOffset = inserter.pickOffset;
            if (pickFrom.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[pickFrom.Index];
                pickFromBelt = planet.cargoTraffic.GetCargoPath(belt.segPathId);
                pickFromOffset += belt.pivotOnPath;
            }

            CargoPath insertIntoBelt = null;
            int insertIntoOffset = inserter.insertOffset;
            if (insertInto.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[insertInto.Index];
                insertIntoBelt = planet.cargoTraffic.pathPool[belt.segPathId];
                insertIntoOffset += belt.pivotOnPath;
            }

            optimizedInserters.Add(default(T).Create(in inserter, pickFromOffset, insertIntoOffset, inserterGradeIndex));
            optimizedInserterStages.Add(ToOptimizedInserterStage(inserter.stage));
            connectionBelts.Add(new ConnectionBelts(pickFromBelt, insertIntoBelt));

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
}
