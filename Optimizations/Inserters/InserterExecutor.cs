using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Ejectors;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.PowerSystems.Generators;
using Weaver.Optimizations.Silos;
using Weaver.Optimizations.Statistics;
using Weaver.Optimizations.Storages;

namespace Weaver.Optimizations.Inserters;

internal static class CargoPathMethods
{
    // Takes CargoPath instead of belt id
    public static bool TryPickItemAtRear(ref OptimizedCargoPath cargoPath, int filter, int[]? needs, out OptimizedCargo cargo)
    {
        if (!cargoPath.TryGetCargoIdAtRear(out cargo))
        {
            cargo = default;
            return false;
        }
        int item = cargo.Item;
        if (filter != 0)
        {
            if (item == filter)
            {
                cargoPath.TryRemoveItemAtRear();
                return true;
            }
        }
        else
        {
            if (needs == null)
            {
                cargoPath.TryRemoveItemAtRear();
                return true;
            }
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i] == item)
                {
                    cargoPath.TryRemoveItemAtRear();
                    return true;
                }
            }
        }

        cargo = default;
        return false;
    }
}

internal sealed class InserterExecutor<TInserter, TInserterGrade>
    where TInserter : struct, IInserter<TInserter, TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>
{
    private TInserter[] _optimizedInserters = null!;
    private OptimizedInserterStage[] _optimizedInserterStages = null!;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates = null!;
    public InserterConnections[] _inserterConnections = null!;
    public Dictionary<int, int> _inserterIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    private readonly NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    private readonly NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    private readonly OptimizedFuelGenerator[][] _generatorSegmentToOptimizedFuelGenerators;
    private readonly OptimizedItemId[]?[]? _fuelNeeds;
    private readonly SubFactoryNeeds _subFactoryNeeds;

    private readonly int _assemblerProducedSize;
    private readonly short[] _assemblerServed;
    private readonly short[] _assemblerIncServed;
    private readonly short[] _assemblerProduced;
    private readonly short[] _assemblerRecipeIndexes;
    private readonly bool[] _assemblerNeedToUpdateNeeds;

    private readonly int _producingLabProducedSize;
    private readonly short[] _producingLabServed;
    private readonly short[] _producingLabIncServed;
    private readonly short[] _producingLabProduced;
    private readonly short[] _producingLabRecipeIndexes;

    private readonly int[] _researchingLabMatrixServed = null!;
    private readonly int[] _researchingLabMatrixIncServed = null!;

    private readonly int[] _siloIndexes;
    private readonly int[] _ejectorIndexes;

    private readonly UniverseStaticData _universeStaticData;

    public int Count => _optimizedInserters.Length;

    public InserterExecutor(NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates,
                            NetworkIdAndState<LabState>[] producingLabNetworkIdAndStates,
                            NetworkIdAndState<LabState>[] researchingLabNetworkIdAndStates,
                            OptimizedFuelGenerator[][] generatorSegmentToOptimizedFuelGenerators,
                            OptimizedItemId[]?[]? fuelNeeds,
                            SubFactoryNeeds subFactoryNeeds,
                            int assemblerProducedSize,
                            short[] assemblerServed,
                            short[] assemblerIncServed,
                            short[] assemblerProduced,
                            short[] assemblerRecipeIndexes,
                            bool[] assemblerNeedToUpdateNeeds,
                            int producingLabProducedSize,
                            short[] producingLabServed,
                            short[] producingLabIncServed,
                            short[] producingLabProduced,
                            short[] producingLabRecipeIndexes,
                            int[] researchingLabMatrixServed,
                            int[] researchingLabMatrixIncServed,
                            int[] siloIndexes,
                            int[] ejectorIndexes,
                            UniverseStaticData universeStaticData)
    {
        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates;
        _producingLabNetworkIdAndStates = producingLabNetworkIdAndStates;
        _researchingLabNetworkIdAndStates = researchingLabNetworkIdAndStates;
        _generatorSegmentToOptimizedFuelGenerators = generatorSegmentToOptimizedFuelGenerators;
        _fuelNeeds = fuelNeeds;
        _subFactoryNeeds = subFactoryNeeds;
        _assemblerProducedSize = assemblerProducedSize;
        _assemblerServed = assemblerServed;
        _assemblerIncServed = assemblerIncServed;
        _assemblerProduced = assemblerProduced;
        _assemblerRecipeIndexes = assemblerRecipeIndexes;
        _assemblerNeedToUpdateNeeds = assemblerNeedToUpdateNeeds;
        _producingLabProducedSize = producingLabProducedSize;
        _producingLabServed = producingLabServed;
        _producingLabIncServed = producingLabIncServed;
        _producingLabProduced = producingLabProduced;
        _producingLabRecipeIndexes = producingLabRecipeIndexes;
        _researchingLabMatrixServed = researchingLabMatrixServed;
        _researchingLabMatrixIncServed = researchingLabMatrixIncServed;
        _siloIndexes = siloIndexes;
        _ejectorIndexes = ejectorIndexes;
        _universeStaticData = universeStaticData;
    }

    public void GameTickInserters(PlanetFactory planet,
                                  int[] inserterPowerConsumerIndexes,
                                  PowerConsumerType[] powerConsumerTypes,
                                  long[] thisSubFactoryNetworkPowerConsumption,
                                  OptimizedCargoPath[] optimizedCargoPaths,
                                  UniverseStaticData universeStaticData)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedInserterStage[] optimizedInserterStages = _optimizedInserterStages;
        NetworkIdAndState<InserterState>[] inserterNetworkIdAndStates = _inserterNetworkIdAndStates;
        TInserter[] optimizedInserters = _optimizedInserters;
        TInserterGrade[] inserterGrades = default(TInserter).GetInserterGrades(universeStaticData);

        for (int inserterIndex = 0; inserterIndex < inserterNetworkIdAndStates.Length; inserterIndex++)
        {
            ref OptimizedInserterStage optimizedInserterStage = ref optimizedInserterStages[inserterIndex];
            ref NetworkIdAndState<InserterState> networkIdAndState = ref inserterNetworkIdAndStates[inserterIndex];
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
            ref TInserter optimizedInserter = ref optimizedInserters[inserterIndex];
            ref readonly TInserterGrade inserterGrade = ref inserterGrades[optimizedInserter.grade];
            optimizedInserter.Update(planet,
                                     this,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     in inserterGrade,
                                     ref optimizedInserterStage,
                                     _inserterConnections,
                                     in _subFactoryNeeds,
                                     optimizedCargoPaths);

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

    private static bool IsInNeed(short productItemIndex,
                                 short[] needs,
                                 int needsOffset,
                                 int needsSize)
    {
        // What is inserted into might not have any needs. For example a belt.
        if (needsSize == 0)
        {
            return true;
        }

        for (int i = 0; i < needsSize; i++)
        {
            if (needs[needsOffset + i] == productItemIndex)
            {
                return true;
            }
        }

        return false;
    }

    public short PickFrom(PlanetFactory planet,
                          ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                          int inserterIndex,
                          int offset,
                          int filter,
                          InserterConnections inserterConnections,
                          GroupNeeds groupNeeds,
                          out byte stack,
                          out byte inc,
                          OptimizedCargoPath[] optimizedCargoPaths)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = inserterConnections.PickFrom;
        int objectIndex = typedObjectIndex.Index;

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ref OptimizedCargoPath pickFromBelt = ref optimizedCargoPaths[objectIndex];
            if (groupNeeds.GroupNeedsSize == 0)
            {
                if (filter != 0)
                {
                    pickFromBelt.TryPickItem(offset - 2, 5, filter, out OptimizedCargo optimizedCargo);
                    stack = optimizedCargo.Stack;
                    inc = optimizedCargo.Inc;
                    return optimizedCargo.Item;
                }
                {
                    OptimizedCargo optimizedCargo = pickFromBelt.TryPickItem(offset - 2, 5);
                    stack = optimizedCargo.Stack;
                    inc = optimizedCargo.Inc;
                    return optimizedCargo.Item;
                }
            }

            {
                short[] needs = _subFactoryNeeds.Needs;
                int needsSize = groupNeeds.GroupNeedsSize;
                int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
                OptimizedCargo optimizedCargo = pickFromBelt.TryPickItem(offset - 2, 5, filter, needs, needsOffset, needsSize);
                stack = optimizedCargo.Stack;
                inc = optimizedCargo.Inc;
                return optimizedCargo.Item;
            }
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

            short[] needs = _subFactoryNeeds.Needs;
            int needsSize = groupNeeds.GroupNeedsSize;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);

            OptimizedItemId[] products = _universeStaticData.AssemblerRecipes[_assemblerRecipeIndexes[objectIndex]].Products;
            short[] produced = _assemblerProduced;
            int producedOffset = _assemblerProducedSize * objectIndex;

            for (int i = 0; i < products.Length; i++)
            {
                short productItemIndex = products[i].ItemIndex;
                if ((filter == productItemIndex || filter == 0) && produced[producedOffset + i] > 0 && productItemIndex > 0 && IsInNeed(productItemIndex, needs, needsOffset, needsSize))
                {
                    produced[producedOffset + i]--;
                    _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                    return products[i].ItemIndex;
                }
            }

            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            int ejectorId = _ejectorIndexes[objectIndex];
            short[] needs = _subFactoryNeeds.Needs;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorId];
            int bulletId = ejector.bulletId;
            int bulletCount = ejector.bulletCount;
            if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && IsInNeed((short)bulletId, needs, needsOffset + EjectorExecutor.SoleEjectorNeedsIndex, groupNeeds.GroupNeedsSize))
            {
                ejector.TakeOneBulletUnsafe(out inc);
                return (short)bulletId;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            int siloId = _siloIndexes[objectIndex];
            short[] needs = _subFactoryNeeds.Needs;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloId];
            int bulletId2 = silo.bulletId;
            int bulletCount2 = silo.bulletCount;
            if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && IsInNeed((short)bulletId2, needs, needsOffset + SiloExecutor.SoleSiloNeedsIndex, groupNeeds.GroupNeedsSize))
            {
                silo.TakeOneBulletUnsafe(out inc);
                return (short)bulletId2;
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
                short[] needs = _subFactoryNeeds.Needs;
                storageComponent = storageComponent.topStorage;
                while (storageComponent != null)
                {
                    if (storageComponent.lastEmptyItem != 0 && storageComponent.lastEmptyItem != filter)
                    {
                        int itemId = filter;
                        int count = 1;
                        bool flag;
                        if (groupNeeds.GroupNeedsSize == 0)
                        {
                            storageComponent.TakeTailItems(ref itemId, ref count, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                            inc = (byte)inc2;
                            flag = count == 1;
                        }
                        else
                        {
                            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
                            bool flag2 = OptimizedStorage.TakeTailItems(storageComponent, ref itemId, ref count, needs, needsOffset, groupNeeds.GroupNeedsSize, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                            inc = (byte)inc2;
                            flag = count == 1 || flag2;
                        }
                        if (count == 1)
                        {
                            storageComponent.lastEmptyItem = -1;
                            return (short)itemId;
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
            throw new InvalidOperationException("Assumption that sorters can not interact with stations is incorrect.");
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

            OptimizedItemId[] products = _universeStaticData.ProducingLabRecipes[_producingLabRecipeIndexes[objectIndex]].Products;
            short[] produced = _producingLabProduced;
            int producedOffset = _producingLabProducedSize * objectIndex;

            short[] needs = _subFactoryNeeds.Needs;
            int needsSize = groupNeeds.GroupNeedsSize;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);

            for (int j = 0; j < products.Length; j++)
            {
                short productItemIndex = products[j].ItemIndex;
                if (produced[producedOffset + j] > 0 && productItemIndex > 0 && (filter == 0 || filter == productItemIndex) && IsInNeed(productItemIndex, needs, needsOffset, needsSize))
                {
                    produced[producedOffset + j]--;
                    _producingLabNetworkIdAndStates[objectIndex].State = (int)LabState.Active;
                    return products[j].ItemIndex;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.FuelPowerGenerator)
        {
            ref readonly TInserter inserter = ref _optimizedInserters[inserterIndex];
            ref OptimizedFuelGenerator insertIntoFuelGenerator = ref _generatorSegmentToOptimizedFuelGenerators[inserter.insertOffset][inserterConnections.InsertInto.Index];
            if (insertIntoFuelGenerator.fuelCount <= 8)
            {
                ref OptimizedFuelGenerator pickFromFuelGenerator = ref _generatorSegmentToOptimizedFuelGenerators[offset][objectIndex];
                int result = pickFromFuelGenerator.PickFuelFrom(filter, out int inc2).ItemIndex;
                inc = (byte)inc2;
                return (short)result;
            }
            return 0;
        }

        return 0;
    }

    public short InsertInto(PlanetFactory planet,
                            ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                            int inserterIndex,
                            InserterConnections inserterConnections,
                            GroupNeeds groupNeeds,
                            int offset,
                            int itemId,
                            byte itemCount,
                            byte itemInc,
                            out byte remainInc,
                            OptimizedCargoPath[] optimizedCargoPaths)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = inserterConnections.InsertInto;
        int objectIndex = typedObjectIndex.Index;

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ref OptimizedCargoPath insertIntoBelt = ref optimizedCargoPaths[objectIndex];
            if (insertIntoBelt.TryInsertItem(offset, itemId, itemCount, itemInc))
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

            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException($"Needs should only be null if assembler is inactive which the above if statement should have caught.");
            }

            OptimizedItemId[] requires = _universeStaticData.AssemblerRecipes[_assemblerRecipeIndexes[objectIndex]].Requires;
            short[] assemblerServed = _assemblerServed;
            short[] assemblerIncServed = _assemblerIncServed;
            int assemblerServedOffset = groupNeeds.GroupNeedsSize * objectIndex;

            for (int i = 0; i < requires.Length; i++)
            {
                if (requires[i].ItemIndex != itemId)
                {
                    continue;
                }

                assemblerServed[assemblerServedOffset + i] += itemCount;
                assemblerIncServed[assemblerServedOffset + i] += itemInc;
                remainInc = 0;
                _assemblerNetworkIdAndStates[objectIndex].State = (int)AssemblerState.Active;
                _assemblerNeedToUpdateNeeds[objectIndex] = true;
                return itemCount;
            }

            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException("Need was null for active ejector.");
            }

            int ejectorId = _ejectorIndexes[objectIndex];
            short[] needs = _subFactoryNeeds.Needs;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(objectIndex);
            if (needs[needsOffset + EjectorExecutor.SoleEjectorNeedsIndex] == itemId && planet.factorySystem.ejectorPool[ejectorId].bulletId == itemId)
            {
                planet.factorySystem.ejectorPool[ejectorId].bulletCount += itemCount;
                planet.factorySystem.ejectorPool[ejectorId].bulletInc += itemInc;
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException("Need was null for active silo.");
            }

            int siloId = _siloIndexes[objectIndex];
            short[] needs = _subFactoryNeeds.Needs;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(objectIndex);
            if (needs[needsOffset + SiloExecutor.SoleSiloNeedsIndex] == itemId && planet.factorySystem.siloPool[siloId].bulletId == itemId)
            {
                planet.factorySystem.siloPool[siloId].bulletCount += itemCount;
                planet.factorySystem.siloPool[siloId].bulletInc += itemInc;
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

            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException($"Needs should only be null if producing lab is inactive which the above if statement should have caught.");
            }

            OptimizedItemId[] requires = _universeStaticData.ProducingLabRecipes[_producingLabRecipeIndexes[objectIndex]].Requires;
            short[] served = _producingLabServed;
            short[] incServed = _producingLabIncServed;
            int servedOffset = groupNeeds.GroupNeedsSize * objectIndex;

            for (int i = 0; i < requires.Length; i++)
            {
                if (requires[i].ItemIndex == itemId)
                {
                    served[servedOffset + i] += itemCount;
                    incServed[servedOffset + i] += itemInc;
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

            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException($"Needs should only be null if researching lab is inactive which the above if statement should have caught.");
            }

            int matrixServedOffset = groupNeeds.GroupNeedsSize * objectIndex;
            int[] matrixServed = _researchingLabMatrixServed;
            int[] matrixIncServed = _researchingLabMatrixIncServed;

            int num2 = itemId - 6001;
            if (num2 >= 0 && num2 < 6)
            {
                matrixServed[matrixServedOffset + num2] += 3600 * itemCount;
                matrixIncServed[matrixServedOffset + num2] += 3600 * itemInc;
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
                    int num4 = planet.entityPool[storageComponent.entityId].battleBaseId != 0 ? storageComponent.AddItemFilteredBanOnly(itemId, itemCount, itemInc, out var remainInc2) : storageComponent.AddItem(itemId, itemCount, itemInc, out remainInc2, useBan: true);
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
                        return (short)num4;
                    }
                }
                storageComponent = storageComponent.nextStorage;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            throw new InvalidOperationException("Assumption that sorters can not interact with stations is incorrect.");
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
        TInserter[] optimizedInserters = _optimizedInserters;
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
                           BeltExecutor beltExecutor,
                           UniverseInserterStaticDataBuilder<TInserterGrade> universeInserterStaticDataBuilder)
    {
        List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates = [];
        List<InserterConnections> inserterConnections = [];
        List<TInserter> optimizedInserters = [];
        List<OptimizedInserterStage> optimizedInserterStages = [];
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
            if (inserter.insertTarget != 0)
            {
                insertInto = subFactory.GetAsGranularTypedObjectIndex(inserter.insertTarget, planet);
                if (insertInto.EntityType == EntityType.None)
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

            if (pickFrom.EntityType == EntityType.None || insertInto.EntityType == EntityType.None)
            {
                continue;
            }

            // Inserters can only move items from a fuel generator if the destination is another fuel generator
            if (pickFrom.EntityType == EntityType.FuelPowerGenerator && insertInto.EntityType != EntityType.FuelPowerGenerator)
            {
                continue;
            }
            else if (pickFrom.EntityType == EntityType.FuelPowerGenerator &&
                     insertInto.EntityType == EntityType.FuelPowerGenerator &&
                     (inserter.pickOffset <= 0 || planet.powerSystem.genPool[inserter.pickOffset].id != inserter.pickOffset))
            {
                continue;
            }
            else if (pickFrom.EntityType == EntityType.FuelPowerGenerator &&
                     insertInto.EntityType == EntityType.FuelPowerGenerator &&
                     planet.powerSystem.genPool[insertInto.Index].id != insertInto.Index)
            {
                continue;
            }

            inserterIdToOptimizedIndex.Add(inserterIndex, optimizedInserters.Count);
            int networkIndex = planet.powerSystem.consumerPool[inserter.pcId].networkId;
            inserterNetworkIdAndStates.Add(new NetworkIdAndState<InserterState>((int)(inserterState ?? InserterState.Active), networkIndex));
            optimizedPowerSystemInserterBuilder.AddInserter(ref inserter, networkIndex);

            TInserterGrade inserterGrade = default(TInserterGrade).Create(ref inserter);
            int inserterGradeIndex = universeInserterStaticDataBuilder.AddInserterGrade(in inserterGrade);

            int pickFromOffset = inserter.pickOffset;
            if (pickFrom.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[pickFrom.Index];
                if (beltExecutor.TryGetOptimizedCargoPathIndex(planet, pickFrom.Index, out BeltIndex pickFromBeltIndex))
                {
                    pickFromOffset = GetCorrectedPickOffset(inserter.pickOffset, ref belt, in pickFromBeltIndex.GetBelt(beltExecutor.OptimizedCargoPaths));
                    pickFrom = new TypedObjectIndex(pickFrom.EntityType, pickFromBeltIndex.GetIndex());
                }
                pickFromOffset += belt.pivotOnPath;
            }
            else if (pickFrom.EntityType == EntityType.FuelPowerGenerator)
            {
                OptimizedFuelGeneratorLocation optimizedFuelGeneratorLocation = optimizedPowerSystemInserterBuilder.GetOptimizedFuelGeneratorLocation(pickFrom.Index);
                pickFromOffset = optimizedFuelGeneratorLocation.SegmentIndex;
                pickFrom = new TypedObjectIndex(pickFrom.EntityType, optimizedFuelGeneratorLocation.Index);
            }

            int insertIntoOffset = inserter.insertOffset;
            if (insertInto.EntityType == EntityType.Belt)
            {
                BeltComponent belt = planet.cargoTraffic.beltPool[insertInto.Index];
                if (beltExecutor.TryGetOptimizedCargoPathIndex(planet, insertInto.Index, out BeltIndex insertIntoBeltIndex))
                {
                    insertIntoOffset = GetCorrectedInsertOffset(inserter.insertOffset, ref belt, in insertIntoBeltIndex.GetBelt(beltExecutor.OptimizedCargoPaths));
                    insertInto = new TypedObjectIndex(insertInto.EntityType, insertIntoBeltIndex.GetIndex());
                }
                insertIntoOffset += belt.pivotOnPath;
            }
            else if (insertInto.EntityType == EntityType.FuelPowerGenerator)
            {
                OptimizedFuelGeneratorLocation optimizedFuelGeneratorLocation = optimizedPowerSystemInserterBuilder.GetOptimizedFuelGeneratorLocation(insertInto.Index);
                insertIntoOffset = optimizedFuelGeneratorLocation.SegmentIndex;
                insertInto = new TypedObjectIndex(insertInto.EntityType, optimizedFuelGeneratorLocation.Index);
            }

            inserterConnections.Add(new InserterConnections(pickFrom, insertInto));
            optimizedInserters.Add(default(TInserter).Create(in inserter, pickFromOffset, insertIntoOffset, inserterGradeIndex));
            optimizedInserterStages.Add(ToOptimizedInserterStage(inserter.stage));
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[inserter.entityId]);
        }

        // Maps new index -> old index. This is used to reorder arrays.
        int[] optimalInserterNeedsOrder = inserterConnections.Select((x, i) => (x, i))
                                                             .OrderBy(x => _subFactoryNeeds.GetTypedObjectNeedsIndex(x.x.InsertInto))
                                                             .Select(x => x.i)
                                                             .ToArray();


        // Maps old index -> new index. This is used to reorder dictionaries.
        int[] oldIndexToNewIndex = new int[optimalInserterNeedsOrder.Length];
        for (int i = 0; i < optimalInserterNeedsOrder.Length; i++)
        {
            oldIndexToNewIndex[optimalInserterNeedsOrder[i]] = i;
        }

        //if (inserterGrades.Count > 0)
        //{
        //    WeaverFixes.Logger.LogMessage($"Inserter grade count: {inserterGrades.Count}");
        //}

        _inserterNetworkIdAndStates = optimalInserterNeedsOrder.Select(x => inserterNetworkIdAndStates[x]).ToArray();
        _inserterConnections = optimalInserterNeedsOrder.Select(x => inserterConnections[x]).ToArray();
        _optimizedInserters = optimalInserterNeedsOrder.Select(x => optimizedInserters[x]).ToArray();
        _optimizedInserterStages = optimalInserterNeedsOrder.Select(x => optimizedInserterStages[x]).ToArray();
        _inserterIdToOptimizedIndex = inserterIdToOptimizedIndex.ToDictionary(x => x.Key, x => oldIndexToNewIndex[x.Value]);
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(optimalInserterNeedsOrder);
    }

    private void Print(int inserterIndex)
    {
        WeaverFixes.Logger.LogMessage(_optimizedInserters[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterNetworkIdAndStates[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterConnections[inserterIndex].ToString());
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

    private static int GetCorrectedPickOffset(int pickOffset, ref readonly BeltComponent belt, ref readonly OptimizedCargoPath cargoPath)
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

    private static int GetCorrectedInsertOffset(int insertOffset, ref readonly BeltComponent belt, ref readonly OptimizedCargoPath cargoPath)
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
