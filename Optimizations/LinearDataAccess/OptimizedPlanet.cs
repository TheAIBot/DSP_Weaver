﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Assemblers;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.Labs.Producing;
using Weaver.Optimizations.LinearDataAccess.Labs.Researching;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Spraycoaters;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedPlanet
{
    private readonly PlanetFactory _planet;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    public InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    public InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    public AssemblerExecutor _assemblerExecutor;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    //private NetworkIdAndState<LabState>[] _labProduceNetworkIdAndStates;
    public ProducingLabExecutor _producingLabExecutor;
    public NetworkIdAndState<LabState>[] _producingLabNetworkIdAndStates;
    public OptimizedProducingLab[] _optimizedProducingLabs;
    public ProducingLabRecipe[] _producingLabRecipes;
    public Dictionary<int, int> _producingLabIdToOptimizedIndex;

    public ResearchingLabExecutor _researchingLabExecutor;
    public NetworkIdAndState<LabState>[] _researchingLabNetworkIdAndStates;
    public OptimizedResearchingLab[] _optimizedResearchingLabs;
    public Dictionary<int, int> _researchingLabIdToOptimizedIndex;

    public SpraycoaterExecutor _spraycoaterExecutor;

    public OptimizedPowerSystem _optimizedPowerSystem;

    public OptimizedPlanet(PlanetFactory planet)
    {
        _planet = planet;
    }

    public void Save()
    {
        _optimizedBiInserterExecutor.Save(_planet);
        _optimizedInserterExecutor.Save(_planet);
        _assemblerExecutor.Save(_planet);
        _producingLabExecutor.Save(_planet);
        _researchingLabExecutor.Save(_planet);
        _spraycoaterExecutor.Save(_planet);

        Status = OptimizedPlanetStatus.Stopped;
    }

    public void Initialize()
    {
        var optimizedPowerSystemBuilder = new OptimizedPowerSystemBuilder(_planet.powerSystem);

        InitializeAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeMiners(_planet);
        InitializeEjectors(_planet);
        InitializeLabAssemblers(_planet, optimizedPowerSystemBuilder);
        InitializeResearchingLabs(_planet, optimizedPowerSystemBuilder);
        InitializeInserters(_planet, optimizedPowerSystemBuilder);
        InitializeSpraycoaters(_planet, optimizedPowerSystemBuilder);

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build();

        Status = OptimizedPlanetStatus.Running;
    }

    private void InitializeInserters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedBiInserterExecutor.Initialize(planet, this, x => x.bidirectional, optimizedPowerSystemBuilder.CreateBiInserterBuilder());

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>(_assemblerExecutor._assemblerNetworkIdAndStates, _producingLabNetworkIdAndStates, _researchingLabNetworkIdAndStates);
        _optimizedInserterExecutor.Initialize(planet, this, x => !x.bidirectional, optimizedPowerSystemBuilder.CreateInserterBuilder());
    }

    private void InitializeAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _assemblerExecutor = new AssemblerExecutor();
        _assemblerExecutor.InitializeAssemblers(planet, optimizedPowerSystemBuilder);
    }

    private void InitializeMiners(PlanetFactory planet)
    {
        int[] minerNetworkIds = new int[planet.factorySystem.minerCursor];

        for (int i = 0; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[i];
            if (miner.id != i)
            {
                continue;
            }

            minerNetworkIds[i] = planet.powerSystem.consumerPool[miner.pcId].networkId;

        }

        _minerNetworkIds = minerNetworkIds;
    }

    private void InitializeEjectors(PlanetFactory planet)
    {
        int[] ejectorNetworkIds = new int[planet.factorySystem.ejectorCursor];

        for (int i = 0; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[i];
            if (ejector.id != i)
            {
                continue;
            }

            ejectorNetworkIds[i] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }

    private void InitializeLabAssemblers(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _producingLabExecutor = new ProducingLabExecutor();
        _producingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _producingLabNetworkIdAndStates = _producingLabExecutor._networkIdAndStates;
        _optimizedProducingLabs = _producingLabExecutor._optimizedLabs;
        _producingLabRecipes = _producingLabExecutor._producingLabRecipes;
        _producingLabIdToOptimizedIndex = _producingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeResearchingLabs(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _researchingLabExecutor = new ResearchingLabExecutor();
        _researchingLabExecutor.Initialize(planet, optimizedPowerSystemBuilder);
        _researchingLabNetworkIdAndStates = _researchingLabExecutor._networkIdAndStates;
        _optimizedResearchingLabs = _researchingLabExecutor._optimizedLabs;
        _researchingLabIdToOptimizedIndex = _researchingLabExecutor._labIdToOptimizedLabIndex;
    }

    private void InitializeSpraycoaters(PlanetFactory planet, OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        _spraycoaterExecutor = new SpraycoaterExecutor();
        _spraycoaterExecutor.Initialize(planet, optimizedPowerSystemBuilder);
    }

    public void GameTick(PlanetFactory planet, long time, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        EntityData[] entityPool = planet.entityPool;
        VeinData[] veinPool = planet.veinPool;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SignData[] entitySignPool = planet.entitySignPool;
        int[][] entityNeeds = planet.entityNeeds;
        FactorySystem factorySystem = planet.factorySystem;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        AstroData[] astroPoses = null;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            float num2;
            float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
            if (num2 < 5f / 12f)
            {
                num2 = 5f / 12f;
            }
            float num4 = history.miningCostRate;
            float miningSpeedScale = history.miningSpeedScale;
            float num5 = history.miningCostRate * 0.40111667f / num2;
            if (num3 > 99.5f)
            {
                num4 = 0f;
                num5 = 0f;
            }

            int[] minerNetworkIds = _minerNetworkIds;
            int num6 = MinerComponent.InsufficientWarningThresAmount(num3, num4);
            for (int i = _start; i < _end; i++)
            {
                if (factorySystem.minerPool[i].id != i)
                {
                    continue;
                }

                float num7 = networkServes[minerNetworkIds[i]];
                factorySystem.minerPool[i].InternalUpdate(planet, veinPool, num7, (factorySystem.minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
            }
        }

        _assemblerExecutor.GameTick(planet, time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.fractionatorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int l = _start; l < _end; l++)
            {
                if (factorySystem.fractionatorPool[l].id == l)
                {
                    int entityId4 = factorySystem.fractionatorPool[l].entityId;
                    float power2 = networkServes[consumerPool[factorySystem.fractionatorPool[l].pcId].networkId];
                    uint state = factorySystem.fractionatorPool[l].InternalUpdate(factorySystem.factory, power2, entitySignPool, productRegister, consumeRegister);
                    entityAnimPool[entityId4].time = Mathf.Sqrt((float)factorySystem.fractionatorPool[l].fluidInputCount * 0.025f);
                    entityAnimPool[entityId4].state = state;
                    entityAnimPool[entityId4].power = power2;
                }
            }
        }
        lock (factorySystem.ejectorPool)
        {
            if (factorySystem.ejectorCursor - factorySystem.ejectorRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSwarm swarm = null;
        if (factorySystem.factory.dysonSphere != null)
        {
            swarm = factorySystem.factory.dysonSphere.swarm;
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.ejectorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            int[] ejectorNetworkIds = _ejectorNetworkIds;
            for (int m = _start; m < _end; m++)
            {
                if (factorySystem.ejectorPool[m].id == m)
                {
                    float power3 = networkServes[ejectorNetworkIds[m]];
                    factorySystem.ejectorPool[m].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);
                }
            }
        }
        lock (factorySystem.siloPool)
        {
            if (factorySystem.siloCursor - factorySystem.siloRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        bool flag3 = dysonSphere != null && dysonSphere.autoNodeCount > 0;
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.siloCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            return;
        }
        for (int n = _start; n < _end; n++)
        {
            if (factorySystem.siloPool[n].id == n)
            {
                int entityId6 = factorySystem.siloPool[n].entityId;
                uint num12 = 0u;
                float power4 = networkServes[consumerPool[factorySystem.siloPool[n].pcId].networkId];
                num12 = factorySystem.siloPool[n].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
                entityAnimPool[entityId6].state = num12;
                entityNeeds[entityId6] = factorySystem.siloPool[n].needs;
                if (entitySignPool[entityId6].signType == 0 || entitySignPool[entityId6].signType > 3)
                {
                    entitySignPool[entityId6].signType = ((!flag3) ? 9u : 0u);
                }
            }
        }
    }

    public static void ParallelSpraycoaterLogic()
    {
        PerformanceMonitor.BeginSample(ECpuWorkEntry.Belt);

        PlanetFactory? localFactory = GameMain.localPlanet?.factory;
        Parallel.For(0, GameMain.data.factoryCount, i =>
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet.cargoTraffic == null)
            {
                return;
            }

            OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(planet);
            if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
            {
                planet.cargoTraffic.SpraycoaterGameTick();
                return;
            }

            optimizedPlanet._spraycoaterExecutor.SpraycoaterGameTick(planet);
        });

        PerformanceMonitor.EndSample(ECpuWorkEntry.Belt);
    }

    public TypedObjectIndex GetAsGranularTypedObjectIndex(int index, PlanetFactory planet)
    {
        ref readonly EntityData entity = ref planet.entityPool[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            if (!_assemblerExecutor._assemblerIdToOptimizedIndex.TryGetValue(entity.assemblerId, out int optimizedAssemblerIndex))
            {
                throw new InvalidOperationException("Failed to convert assembler id into optimized assembler id.");
            }

            return new TypedObjectIndex(EntityType.Assembler, optimizedAssemblerIndex);
        }
        else if (entity.ejectorId != 0)
        {
            return new TypedObjectIndex(EntityType.Ejector, entity.ejectorId);
        }
        else if (entity.siloId != 0)
        {
            return new TypedObjectIndex(EntityType.Silo, entity.siloId);
        }
        else if (entity.labId != 0)
        {
            if (planet.factorySystem.labPool[entity.labId].researchMode)
            {
                if (!_researchingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    throw new InvalidOperationException("Failed to convert researching lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ResearchingLab, optimizedLabIndex);
            }
            else
            {
                if (!_producingLabIdToOptimizedIndex.TryGetValue(entity.labId, out int optimizedLabIndex))
                {
                    throw new InvalidOperationException("Failed to convert producing lab id into optimized lab id.");
                }

                return new TypedObjectIndex(EntityType.ProducingLab, optimizedLabIndex);
            }
        }
        else if (entity.storageId != 0)
        {
            return new TypedObjectIndex(EntityType.Storage, entity.storageId);
        }
        else if (entity.stationId != 0)
        {
            return new TypedObjectIndex(EntityType.Station, entity.stationId);
        }
        else if (entity.powerGenId != 0)
        {
            return new TypedObjectIndex(EntityType.PowerGenerator, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new TypedObjectIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new TypedObjectIndex(EntityType.Inserter, entity.inserterId);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }

    public static int[]? GetEntityNeeds(PlanetFactory planet, int entityIndex)
    {
        ref readonly EntityData entity = ref planet.entityPool[entityIndex];
        if (entity.beltId != 0)
        {
            return null;
        }
        else if (entity.assemblerId != 0)
        {
            return planet.factorySystem.assemblerPool[entity.assemblerId].needs;
        }
        else if (entity.ejectorId != 0)
        {
            return planet.factorySystem.ejectorPool[entity.ejectorId].needs;
        }
        else if (entity.siloId != 0)
        {
            return planet.factorySystem.siloPool[entity.siloId].needs;
        }
        else if (entity.labId != 0)
        {
            return planet.factorySystem.labPool[entity.labId].needs;
        }
        else if (entity.storageId != 0)
        {
            return null;
        }
        else if (entity.stationId != 0)
        {
            return planet.transport.stationPool[entity.stationId].needs;
        }
        else if (entity.powerGenId != 0)
        {
            return null;
        }
        else if (entity.splitterId != 0)
        {
            return null;
        }
        else if (entity.inserterId != 0)
        {
            return null;
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}