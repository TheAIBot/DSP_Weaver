﻿using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal record struct PickFromProducingPlant(int[] Products, int[] Produced);

internal record struct ConnectionBelts(CargoPath PickFrom, CargoPath InsertInto);

internal sealed class InserterExecutor<T> : IInserterExecutor<T>
    where T : struct, IInserter<T>
{
    private T[] _optimizedInserters;
    private OptimizedInserterStage[] _optimizedInserterStages;
    private InserterGrade[] _inserterGrades;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates;
    public InserterConnections[] _inserterConnections;
    public int[][] _inserterConnectionNeeds;
    public int[] _optimizedInserterToInserterIndex;
    public PickFromProducingPlant[] _pickFromProducingPlants;
    public ConnectionBelts[] _connectionBelts;

    public int inserterCount => _optimizedInserters.Length;

    public void Initialize(PlanetFactory planet, OptimizedPlanet optimizedPlanet, Func<InserterComponent, bool> inserterSelector, OptimizedPowerSystemInserterBuilder optimizedPowerSystemInserterBuilder)
    {
        (List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates,
         List<InserterConnections> inserterConnections,
         List<int[]> inserterConnectionNeeds,
         List<InserterGrade> inserterGrades,
         Dictionary<InserterGrade, int> inserterGradeToIndex,
         List<T> optimizedInserters,
         List<OptimizedInserterStage> optimizedInserterStages,
         List<int> optimizedInserterToInserterIndex,
         List<PickFromProducingPlant> pickFromProducingPlants,
         List<ConnectionBelts> connectionBelts)
            = InitializeInserters<T>(planet, optimizedPlanet, inserterSelector, optimizedPowerSystemInserterBuilder);

        _inserterNetworkIdAndStates = inserterNetworkIdAndStates.ToArray();
        _inserterConnections = inserterConnections.ToArray();
        _inserterConnectionNeeds = inserterConnectionNeeds.ToArray();
        _inserterGrades = inserterGrades.ToArray();
        _optimizedInserters = optimizedInserters.ToArray();
        _optimizedInserterStages = optimizedInserterStages.ToArray();
        _optimizedInserterToInserterIndex = optimizedInserterToInserterIndex.ToArray();
        _pickFromProducingPlants = pickFromProducingPlants.ToArray();
        _connectionBelts = connectionBelts.ToArray();
    }

    public T Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return default(T).Create(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public int GetUnoptimizedInserterIndex(int optimizedInserterIndex)
    {
        return _optimizedInserterToInserterIndex[optimizedInserterIndex];
    }

    public void GameTickInserters(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, int _start, int _end)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        InserterComponent[] unoptimizedInserters = planet.factorySystem.inserterPool;
        _end = _end > _optimizedInserters.Length ? _optimizedInserters.Length : _end;
        for (int inserterIndex = _start; inserterIndex < _end; inserterIndex++)
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
                    if (!IsObjectPickFromActive(optimizedPlanet, inserterIndex))
                    {
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
                else if (inserterState == InserterState.InactiveInsertInto)
                {
                    if (!IsObjectInsertIntoActive(optimizedPlanet, inserterIndex))
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
            ref readonly ConnectionBelts connectionBelts = ref _connectionBelts[inserterIndex];
            optimizedInserter.Update(planet,
                                     optimizedPlanet,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     in _inserterConnections[inserterIndex],
                                     in _inserterConnectionNeeds[inserterIndex],
                                     _pickFromProducingPlants,
                                     inserterGrade,
                                     ref optimizedInserterStage,
                                     in connectionBelts);
        }
    }

    public void UpdatePower(OptimizedPlanet optimizedPlanet,
                            int[] inserterPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisThreadNetworkPowerConsumption,
                            int _usedThreadCnt,
                            int _curThreadIdx,
                            int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(0, _optimizedInserters.Length - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out int _start, out int _end))
        {
            return;
        }

        NetworkIdAndState<InserterState>[] inserterNetworkIdAndStates = _inserterNetworkIdAndStates;
        for (int j = _start; j < _end; j++)
        {
            int networkIndex = inserterNetworkIdAndStates[j].Index;
            int powerConsumerTypeIndex = inserterPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            OptimizedInserterStage optimizedInserterStage = _optimizedInserterStages[j];
            thisThreadNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, optimizedInserterStage);
        }
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType, OptimizedInserterStage stage)
    {
        return powerConsumerType.GetRequiredEnergy(stage == OptimizedInserterStage.Sending || stage == OptimizedInserterStage.Returning);
    }

    private bool IsObjectPickFromActive(OptimizedPlanet optimizedPlanet, int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].PickFrom;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return (AssemblerState)optimizedPlanet._assemblerNetworkIdAndStates[objectIndex.Index].State == AssemblerState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ProducingLab)
        {
            return (LabState)optimizedPlanet._producingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if pick from is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private bool IsObjectInsertIntoActive(OptimizedPlanet optimizedPlanet, int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].InsertInto;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return (AssemblerState)optimizedPlanet._assemblerNetworkIdAndStates[objectIndex.Index].State == AssemblerState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ProducingLab)
        {
            return (LabState)optimizedPlanet._producingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else if (objectIndex.EntityType == EntityType.ResearchingLab)
        {
            return (LabState)optimizedPlanet._researchingLabNetworkIdAndStates[objectIndex.Index].State == LabState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if insert into is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private static (List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates,
                    List<InserterConnections> inserterConnections,
                    List<int[]> inserterConnectionNeeds,
                    List<InserterGrade> inserterGrades,
                    Dictionary<InserterGrade, int> inserterGradeToIndex,
                    List<TInserter> optimizedInserters,
                    List<OptimizedInserterStage> optimizedInserterStages,
                    List<int> optimizedInserterToInserterIndex,
                    List<PickFromProducingPlant> pickFromProducingPlants,
                    List<ConnectionBelts> connectionBelts)
        InitializeInserters<TInserter>(PlanetFactory planet,
                                       OptimizedPlanet optimizedPlanet,
                                       Func<InserterComponent, bool> inserterSelector,
                                       OptimizedPowerSystemInserterBuilder optimizedPowerSystemInserterBuilder)
        where TInserter : struct, IInserter<TInserter>
    {
        List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates = [];
        List<InserterConnections> inserterConnections = [];
        List<int[]> inserterConnectionNeeds = [];
        List<InserterGrade> inserterGrades = [];
        Dictionary<InserterGrade, int> inserterGradeToIndex = [];
        List<TInserter> optimizedInserters = [];
        List<OptimizedInserterStage> optimizedInserterStages = [];
        List<int> optimizedInserterToInserterIndex = [];
        List<PickFromProducingPlant> pickFromProducingPlants = [];
        List<ConnectionBelts> connectionBelts = [];

        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[i];
            if (inserter.id != i || !inserterSelector(inserter))
            {
                continue;
            }

            InserterState? inserterState = null;
            TypedObjectIndex pickFrom = new TypedObjectIndex(EntityType.None, 0);
            if (inserter.pickTarget != 0)
            {
                pickFrom = optimizedPlanet.GetAsGranularTypedObjectIndex(inserter.pickTarget, planet);
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
                insertInto = optimizedPlanet.GetAsGranularTypedObjectIndex(inserter.insertTarget, planet);
                insertIntoNeeds = OptimizedPlanet.GetEntityNeeds(planet, inserter.insertTarget);
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

            optimizedInserters.Add(default(TInserter).Create(in inserter, pickFromOffset, insertIntoOffset, inserterGradeIndex));
            optimizedInserterStages.Add(ToOptimizedInserterStage(inserter.stage));
            optimizedInserterToInserterIndex.Add(i);
            connectionBelts.Add(new ConnectionBelts(pickFromBelt, insertIntoBelt));

            if (pickFrom.EntityType == EntityType.Assembler)
            {
                ref readonly OptimizedAssembler assembler = ref optimizedPlanet._optimizedAssemblers[pickFrom.Index];
                ref readonly AssemblerRecipe assemblerRecipe = ref optimizedPlanet._assemblerRecipes[assembler.assemblerRecipeIndex];
                pickFromProducingPlants.Add(new PickFromProducingPlant(assemblerRecipe.Products, assembler.produced));
            }
            else if (pickFrom.EntityType == EntityType.ProducingLab)
            {
                ref readonly OptimizedProducingLab lab = ref optimizedPlanet._optimizedProducingLabs[pickFrom.Index];
                ref readonly ProducingLabRecipe producingLabRecipe = ref optimizedPlanet._producingLabRecipes[lab.producingLabRecipeIndex];
                pickFromProducingPlants.Add(new PickFromProducingPlant(producingLabRecipe.Products, lab.produced));
            }
            else
            {
                pickFromProducingPlants.Add(default);
            }
        }

        return (inserterNetworkIdAndStates,
                inserterConnections,
                inserterConnectionNeeds,
                inserterGrades,
                inserterGradeToIndex,
                optimizedInserters,
                optimizedInserterStages,
                optimizedInserterToInserterIndex,
                pickFromProducingPlants,
                connectionBelts);
    }

    private static OptimizedInserterStage ToOptimizedInserterStage(EInserterStage inserterStage) => inserterStage switch
    {
        EInserterStage.Picking => OptimizedInserterStage.Picking,
        EInserterStage.Sending => OptimizedInserterStage.Sending,
        EInserterStage.Inserting => OptimizedInserterStage.Inserting,
        EInserterStage.Returning => OptimizedInserterStage.Returning,
        _ => throw new ArgumentOutOfRangeException(nameof(inserterStage))
    };
}
