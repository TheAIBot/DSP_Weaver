using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Ejectors;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;
using Weaver.Optimizations.LinearDataAccess.Silos;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal interface IWholeNeedsBuilder
{
    List<short> GetNeedsFlat();
    void CompletedGroup(EntityType entityType, GroupNeeds groupNeeds);
}

internal sealed class SubFactoryNeedsBuilder : IWholeNeedsBuilder
{
    private readonly List<short> _needsFlat = [];
    private readonly GroupNeeds[] _groupNeeds = new GroupNeeds[ArrayExtensions.GetEnumValuesEnumerable<EntityType>().Max(x => (int)x) + 1];

    public GroupNeedsBuilder CreateGroupNeedsBuilder(EntityType entityType)
    {
        return new GroupNeedsBuilder(this, entityType);
    }

    List<short> IWholeNeedsBuilder.GetNeedsFlat() => _needsFlat;

    void IWholeNeedsBuilder.CompletedGroup(EntityType entityType, GroupNeeds groupNeeds)
    {
        _groupNeeds[(int)entityType] = groupNeeds;
    }

    public SubFactoryNeeds Build()
    {
        return new SubFactoryNeeds(_groupNeeds, _needsFlat.ToArray());
    }
}

internal sealed class GroupNeedsBuilder
{
    private readonly IWholeNeedsBuilder _wholeNeedsBuilder;
    private readonly EntityType _groupEntityType;
    private readonly List<int[]> _allNeeds = [];
    private int _maxNeedsSize = int.MinValue;

    public GroupNeedsBuilder(IWholeNeedsBuilder wholeNeedsBuilder, EntityType groupEntityType)
    {
        _wholeNeedsBuilder = wholeNeedsBuilder;
        _groupEntityType = groupEntityType;
    }

    public void AddNeeds(int[] needs, int needsSize)
    {
        _allNeeds.Add(needs);
        _maxNeedsSize = Math.Max(_maxNeedsSize, needsSize);
    }

    public void Complete()
    {
        List<int[]> allNeeds = _allNeeds;
        if (allNeeds.Count == 0)
        {
            return;
        }

        List<short> needsFlat = _wholeNeedsBuilder.GetNeedsFlat();
        int groupStartIndex = needsFlat.Count;
        int maxNeedsSize = _maxNeedsSize;

        for (int i = 0; i < allNeeds.Count; i++)
        {
            for (int needsIndex = 0; needsIndex < maxNeedsSize; needsIndex++)
            {

                needsFlat.Add(GetOrDefault(allNeeds[i], needsIndex));
            }
        }

        _wholeNeedsBuilder.CompletedGroup(_groupEntityType, new GroupNeeds(groupStartIndex, maxNeedsSize));
    }

    private static short GetOrDefault(int[] values, int index)
    {
        if (values.Length <= index)
        {
            return 0;
        }

        return (short)values[index];
    }
}

internal readonly struct SubFactoryNeeds
{
    private readonly GroupNeeds[] _groupNeeds;
    public readonly short[] Needs;

    public SubFactoryNeeds(GroupNeeds[] groupNeeds, short[] needs)
    {
        _groupNeeds = groupNeeds;
        Needs = needs;
    }

    public readonly GroupNeeds GetGroupNeeds(EntityType entityType)
    {
        return _groupNeeds[(int)entityType];
    }

    public readonly int GetTypedObjectNeedsIndex(TypedObjectIndex typedObjectIndex)
    {
        return _groupNeeds[(int)typedObjectIndex.EntityType].GetObjectNeedsIndex(typedObjectIndex.Index);
    }
}

internal record struct GroupNeeds(int GroupStartIndex, int GroupNeedsSize)
{
    public int GetObjectNeedsIndex(int objectIndex)
    {
        return GroupStartIndex + objectIndex * GroupNeedsSize;
    }

    public static void SetIfInRange(int[] copyTo, short[] copyFrom, int copyToIndex, int copyFromIndex)
    {
        if (copyTo.Length <= copyToIndex)
        {
            return;
        }

        copyTo[copyToIndex] = copyFrom[copyFromIndex];
    }

    public static void SetIfInRange(int[] copyTo, int[] copyFrom, int copyToIndex, int copyFromIndex)
    {
        if (copyTo.Length <= copyToIndex)
        {
            return;
        }

        copyTo[copyToIndex] = copyFrom[copyFromIndex];
    }
}

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

internal sealed class InserterExecutor<T>
    where T : struct, IInserter<T>
{
    private T[] _optimizedInserters = null!;
    private OptimizedInserterStage[] _optimizedInserterStages = null!;
    private InserterGrade[] _inserterGrades = null!;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates = null!;
    public InserterConnections[] _inserterConnections = null!;
    public ConnectionBelts[] _connectionBelts = null!;
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
    private readonly AssemblerRecipe[] _assemblerRecipes;

    private readonly int _producingLabProducedSize;
    private readonly short[] _producingLabServed;
    private readonly short[] _producingLabIncServed;
    private readonly short[] _producingLabProduced;
    private readonly short[] _producingLabRecipeIndexes;
    private readonly ProducingLabRecipe[] _producingLabRecipes;

    private readonly int[] _researchingLabMatrixServed = null!;
    private readonly int[] _researchingLabMatrixIncServed = null!;

    private readonly int[] _siloIndexes;
    private readonly int[] _ejectorIndexes;

    public int InserterCount => _optimizedInserters.Length;

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
                            AssemblerRecipe[] assemblerRecipes,
                            int producingLabProducedSize,
                            short[] producingLabServed,
                            short[] producingLabIncServed,
                            short[] producingLabProduced,
                            short[] producingLabRecipeIndexes,
                            ProducingLabRecipe[] producingLabRecipes,
                            int[] researchingLabMatrixServed,
                            int[] researchingLabMatrixIncServed,
                            int[] siloIndexes,
                            int[] ejectorIndexes)
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
        _assemblerRecipes = assemblerRecipes;
        _producingLabProducedSize = producingLabProducedSize;
        _producingLabServed = producingLabServed;
        _producingLabIncServed = producingLabIncServed;
        _producingLabProduced = producingLabProduced;
        _producingLabRecipeIndexes = producingLabRecipeIndexes;
        _producingLabRecipes = producingLabRecipes;
        _researchingLabMatrixServed = researchingLabMatrixServed;
        _researchingLabMatrixIncServed = researchingLabMatrixIncServed;
        _siloIndexes = siloIndexes;
        _ejectorIndexes = ejectorIndexes;
    }

    public void GameTickInserters(PlanetFactory planet,
                                  int[] inserterPowerConsumerIndexes,
                                  PowerConsumerType[] powerConsumerTypes,
                                  long[] thisSubFactoryNetworkPowerConsumption)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedInserterStage[] optimizedInserterStages = _optimizedInserterStages;
        NetworkIdAndState<InserterState>[] inserterNetworkIdAndStates = _inserterNetworkIdAndStates;
        T[] optimizedInserters = _optimizedInserters;
        InserterGrade[] inserterGrades = _inserterGrades;

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
            ref T optimizedInserter = ref optimizedInserters[inserterIndex];
            InserterGrade inserterGrade = inserterGrades[optimizedInserter.grade];
            optimizedInserter.Update(planet,
                                     this,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     inserterGrade,
                                     ref optimizedInserterStage,
                                     _inserterConnections,
                                     in _subFactoryNeeds);

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

    public int PickFrom(PlanetFactory planet,
                        ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                        int inserterIndex,
                        int offset,
                        int filter,
                        InserterConnections inserterConnections,
                        GroupNeeds groupNeeds,
                        out byte stack,
                        out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = inserterConnections.PickFrom;
        int objectIndex = typedObjectIndex.Index;

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            ConnectionBelts connectionBelts = _connectionBelts[inserterIndex];
            if (connectionBelts.PickFrom == null)
            {
                throw new InvalidOperationException($"{nameof(connectionBelts.PickFrom)} was null.");
            }

            if (groupNeeds.GroupNeedsSize == 0)
            {
                if (filter != 0)
                {
                    return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, filter, out stack, out inc);
                }
                return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, out stack, out inc);
            }

            short[] needs = _subFactoryNeeds.Needs;
            int needsSize = groupNeeds.GroupNeedsSize;
            int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
            return connectionBelts.PickFrom.TryPickItem(offset - 2, 5, filter, needs, needsOffset, needsSize, out stack, out inc);
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

            OptimizedItemId[] products = _assemblerRecipes[_assemblerRecipeIndexes[objectIndex]].Products;
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
            if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && needs[needsOffset + EjectorExecutor.SoleEjectorNeedsIndex] == bulletId)
            {
                ejector.TakeOneBulletUnsafe(out inc);
                return bulletId;
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
            if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && needs[needsOffset + SiloExecutor.SoleSiloNeedsIndex] == bulletId2)
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
                int[] needs = planet.entityNeeds[storageComponent.entityId];
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

            OptimizedItemId[] products = _producingLabRecipes[_producingLabRecipeIndexes[objectIndex]].Products;
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
                          InserterConnections inserterConnections,
                          GroupNeeds groupNeeds,
                          int offset,
                          int itemId,
                          byte itemCount,
                          byte itemInc,
                          out byte remainInc)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = inserterConnections.InsertInto;
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

            if (groupNeeds.GroupNeedsSize == 0)
            {
                throw new InvalidOperationException($"Needs should only be null if assembler is inactive which the above if statement should have caught.");
            }

            OptimizedItemId[] requires = _assemblerRecipes[_assemblerRecipeIndexes[objectIndex]].Requires;
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

            OptimizedItemId[] requires = _producingLabRecipes[_producingLabRecipeIndexes[objectIndex]].Requires;
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
        List<InserterGrade> inserterGrades = [];
        Dictionary<InserterGrade, int> inserterGradeToIndex = [];
        List<T> optimizedInserters = [];
        List<OptimizedInserterStage> optimizedInserterStages = [];
        List<ConnectionBelts> connectionBelts = [];
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

            inserterIdToOptimizedIndex.Add(inserterIndex, optimizedInserters.Count);
            int networkIndex = planet.powerSystem.consumerPool[inserter.pcId].networkId;
            inserterNetworkIdAndStates.Add(new NetworkIdAndState<InserterState>((int)(inserterState ?? InserterState.Active), networkIndex));
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
        }

        int[] optimalInserterNeedsOrder = inserterConnections.Select((x, i) => (x, i))
                                                             .OrderBy(x => _subFactoryNeeds.GetTypedObjectNeedsIndex(x.x.InsertInto))
                                                             .Select(x => x.i)
                                                             .ToArray();

        _inserterNetworkIdAndStates = optimalInserterNeedsOrder.Select(x => inserterNetworkIdAndStates[x]).ToArray();
        _inserterConnections = optimalInserterNeedsOrder.Select(x => inserterConnections[x]).ToArray();
        _inserterGrades = inserterGrades.ToArray();
        _optimizedInserters = optimalInserterNeedsOrder.Select(x => optimizedInserters[x]).ToArray();
        _optimizedInserterStages = optimalInserterNeedsOrder.Select(x => optimizedInserterStages[x]).ToArray();
        _connectionBelts = optimalInserterNeedsOrder.Select(x => connectionBelts[x]).ToArray();
        _inserterIdToOptimizedIndex = inserterIdToOptimizedIndex.ToDictionary(x => x.Key, x => optimalInserterNeedsOrder[x.Value]);
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(optimalInserterNeedsOrder);
    }

    private void Print(int inserterIndex)
    {
        WeaverFixes.Logger.LogMessage(_optimizedInserters[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterNetworkIdAndStates[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_inserterConnections[inserterIndex].ToString());
        WeaverFixes.Logger.LogMessage(_connectionBelts[inserterIndex].ToString());
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
