using System;
using UnityEngine;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class UnOptimizedPlanetWorkChunk : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly WorkType _workType;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;

    private UnOptimizedPlanetWorkChunk(PlanetFactory planet, WorkType workType, int workIndex, int maxWorkCount)
    {
        _planet = planet;
        _workType = workType;
        _workIndex = workIndex;
        _maxWorkCount = maxWorkCount;
    }

    public static SingleWorkLeaf[] CreateDuplicateChunksInWorkLeafs(PlanetFactory planet, WorkType workType, int count)
    {
        SingleWorkLeaf[] workLeafs = new SingleWorkLeaf[count];
        for (int i = 0; i < workLeafs.Length; i++)
        {
            workLeafs[i] = new SingleWorkLeaf(new UnOptimizedPlanetWorkChunk(planet, workType, i, count));
        }

        return workLeafs;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        bool isActive = localPlanet == _planet.planet;
        if (_workType == WorkType.BeforePower)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
            {
                DeepProfiler.BeginSample(DPEntry.PowerConsumer, workerIndex);
                _planet.defenseSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                _planet.digitalSystem?.ParallelGameTickBeforePower(time, _maxWorkCount, _workIndex, 2);
                DeepProfiler.EndSample(DPEntry.PowerConsumer, workerIndex);
            }
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
        }
        else if (_workType == WorkType.Power && _planet.powerSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.PowerSystem, workerIndex);
            _planet.powerSystem.multithreadPlayerPos = playerPosition;
            _planet.powerSystem.GameTick(time, isActive, multithreaded: true, workerIndex);
            DeepProfiler.EndSample(DPEntry.PowerSystem, workerIndex);
        }
        else if (_workType == WorkType.Construction && _planet.constructionSystem != null)
        {
            throw new InvalidOperationException($"The work type {nameof(WorkType.Construction)} is not thread safe.");
        }
        else if (_workType == WorkType.CheckBefore && _planet.factorySystem != null)
        {
            throw new InvalidOperationException($"The work type {nameof(WorkType.CheckBefore)} is not thread safe.");
        }
        else if (_workType == WorkType.Miner)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Miner} work on a null planet.");
            }

            ParallelMinerGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.Fractionator)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Fractionator} work on a null planet.");
            }

            ParallelFractionatorGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.Ejector)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Ejector} work on a null planet.");
            }

            ParallelEjectorGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.Silo)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Silo} work on a null planet.");
            }

            ParallelSiloGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.Assembler)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.Assembler} work on a null planet.");
            }

            ParallelAssemblerGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.LabProduce)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.LabProduce} work on a null planet.");
            }

            ParallelLabProduceGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.LabResearchMode)
        {
            DeepProfiler.BeginMajorSample(DPEntry.Lab, workerIndex);
            OptimizedStarCluster.ThreadSafeGameTickLabResearchMode(_planet, time, isActive);
            DeepProfiler.EndMajorSample(DPEntry.Lab, workerIndex);
        }
        else if (_workType == WorkType.LabOutput2NextData)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.LabOutput2NextData} work on a null planet.");
            }

            DeepProfiler.BeginSample(DPEntry.Lab, workerIndex);
            _planet.factorySystem.GameTickLabOutputToNext(time, isActive);
            DeepProfiler.EndSample(DPEntry.Lab, workerIndex);
        }
        else if (_workType == WorkType.TransportData && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Transport, workerIndex, 99L);
            _planet.transport.GameTick(time, isActive, multithreaded: false, -1);
            DeepProfiler.EndSample(DPEntry.Transport, workerIndex); ;
        }
        else if (_workType == WorkType.InputFromBelt && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            _planet.transport.GameTick_InputFromBelt(time);
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.InserterData)
        {
            if (_planet.factorySystem == null)
            {
                throw new InvalidOperationException($"Attempted to execute {WorkType.InserterData} work on a null planet.");
            }

            ParallelInserterGameTick(workerIndex, time, isActive);
        }
        else if (_workType == WorkType.Storage && _planet.factoryStorage != null)
        {
            DeepProfiler.BeginSample(DPEntry.Storage, workerIndex);
            _planet.factoryStorage.GameTickStorage(time, isActive);
            DeepProfiler.EndSample(DPEntry.Storage, workerIndex);

            DeepProfiler.BeginSample(DPEntry.FluidTank, workerIndex);
            _planet.factoryStorage.GameTickTank();
            DeepProfiler.EndSample(DPEntry.FluidTank, workerIndex);
        }
        else if (_workType == WorkType.CargoPathsData)
        {
            ParallelCargoPathsGameTick(workerIndex);
        }
        else if (_workType == WorkType.Splitter && _planet.cargoTraffic != null)
        {
            DeepProfiler.BeginSample(DPEntry.Splitter, workerIndex);
            _planet.cargoTraffic.SplitterGameTick(time);
            DeepProfiler.EndSample(DPEntry.Splitter, workerIndex);
        }
        else if (_workType == WorkType.CargoTrafficMisc && _planet.cargoTraffic != null)
        {
            ParallelCargoTrafficMiscGameTick(workerIndex);
        }
        else if (_workType == WorkType.OutputToBelt && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            int stationPilerLevel = GameMain.history.stationPilerLevel;
            _planet.transport.GameTick_OutputToBelt(stationPilerLevel, time);
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.SandboxMode && _planet.transport != null)
        {
            DeepProfiler.BeginSample(DPEntry.Station, workerIndex);
            lock (singleThreadedCodeLock)
            {
                _planet.transport.GameTick_SandboxMode(workerIndex);
            }
            DeepProfiler.EndSample(DPEntry.Station, workerIndex);
        }
        else if (_workType == WorkType.PresentCargoPathsData && _planet.cargoTraffic != null)
        {
            ParallelPresentCargoPathsGameTick(workerIndex);
        }
        else if (_workType == WorkType.Digital && _planet.digitalSystem != null)
        {
            DeepProfiler.BeginSample(DPEntry.DigitalSystem, workerIndex);
            _planet.digitalSystem.GameTick(isActive);
            DeepProfiler.EndSample(DPEntry.DigitalSystem, workerIndex);
        }
    }

    private void ParallelMinerGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Miner, workerIndex);
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        PowerSystem powerSystem = _planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        EntityData[] entityPool = _planet.entityPool;
        VeinData[] veinPool = _planet.veinPool;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        StationComponent[] stationPool = _planet.transport.stationPool;
        MinerComponent[] minerPool = _planet.factorySystem.minerPool;
        float num = 1f / 60f;
        float num3;
        float num2 = (num3 = _planet.gameData.gameDesc.resourceMultiplier);
        if (num3 < 5f / 12f)
        {
            num3 = 5f / 12f;
        }
        float num4 = history.miningCostRate;
        float miningSpeedScale = history.miningSpeedScale;
        float num5 = history.miningCostRate * 0.40111667f / num3;
        if (num2 > 99.5f)
        {
            num4 = 0f;
            num5 = 0f;
        }
        bool flag2 = isActive && num4 > 0f;
        int num6 = MinerComponent.InsufficientWarningThresAmount(num2, num4);
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.minerCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            if (minerPool[i].id != i)
            {
                continue;
            }
            int entityId = minerPool[i].entityId;
            int stationId = entityPool[entityId].stationId;
            float num7 = networkServes[consumerPool[minerPool[i].pcId].networkId];
            uint num8 = minerPool[i].InternalUpdate(_planet, veinPool, num7, (minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
            int num9 = (int)Mathf.Floor(entityAnimPool[entityId].time / 10f);
            entityAnimPool[entityId].time = entityAnimPool[entityId].time % 10f;
            entityAnimPool[entityId].Step(num8, num * num7);
            entityAnimPool[entityId].power = num7;
            if (stationId > 0)
            {
                if (minerPool[i].veinCount > 0)
                {
                    EVeinType veinTypeByItemId = LDB.veins.GetVeinTypeByItemId(veinPool[minerPool[i].veins[0]].productId);
                    entityAnimPool[entityId].state += (uint)((int)veinTypeByItemId * 100);
                }
                entityAnimPool[entityId].power += 10f;
                entityAnimPool[entityId].power += minerPool[i].speed;
                if (num8 == 1)
                {
                    num9 = 3000;
                }
                else
                {
                    num9 -= (int)(num * 1000f);
                    if (num9 < 0)
                    {
                        num9 = 0;
                    }
                }
                entityAnimPool[entityId].time += num9 * 10;
            }
            if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
            {
                entitySignPool[entityId].signType = ((minerPool[i].minimumVeinAmount < num6) ? 7u : 0u);
            }
            if (flag2 && minerPool[i].type == EMinerType.Vein)
            {
                if ((long)i % 30L == time % 30)
                {
                    minerPool[i].GetTotalVeinAmount(veinPool);
                }
                entitySignPool[entityId].count0 = minerPool[i].totalVeinAmount;
            }
            else
            {
                entitySignPool[entityId].count0 = 0f;
            }
            if (stationId > 0)
            {
                StationStore[] array2 = stationPool[stationId].storage;
                int num10 = array2[0].count;
                if (array2[0].localOrder < -4000)
                {
                    num10 += array2[0].localOrder + 4000;
                }
                int max = array2[0].max;
                max = ((max < 3000) ? 3000 : max);
                float num11 = (float)num10 / (float)max;
                num11 = ((num11 > 1f) ? 1f : num11);
                float num12 = -2.45f * num11 + 2.47f;
                num12 = ((num12 > 1f) ? 1f : num12);
                minerPool[i].speedDamper = num12;
            }
            else
            {
                float num13 = (float)minerPool[i].productCount / 50f;
                num13 = ((num13 > 1f) ? 1f : num13);
                float num14 = -2.45f * num13 + 2.47f;
                num14 = ((num14 > 1f) ? 1f : num14);
                minerPool[i].speedDamper = num14;
            }
            minerPool[i].SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Miner, workerIndex);
    }

    private void ParallelFractionatorGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Assembler, workerIndex);
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        FractionatorComponent[] fractionatorPool = _planet.factorySystem.fractionatorPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        float[] networkServes = _planet.powerSystem.networkServes;
        FactoryProductionStat obj = _planet.gameData.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.fractionatorCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref FractionatorComponent reference2 = ref fractionatorPool[i];
            if (reference2.id == i)
            {
                int entityId = reference2.entityId;
                float power = networkServes[consumerPool[reference2.pcId].networkId];
                uint state = reference2.InternalUpdate(_planet, power, entitySignPool, productRegister, consumeRegister);
                if (isActive)
                {
                    entityAnimPool[entityId].time = Mathf.Sqrt((float)reference2.fluidInputCount * 0.025f);
                    entityAnimPool[entityId].state = state;
                    entityAnimPool[entityId].power = power;
                }
                reference2.SetPCState(consumerPool);
            }
        }
        DeepProfiler.EndSample(DPEntry.Assembler, workerIndex);
    }

    private void ParallelEjectorGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Ejector, workerIndex);
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        int[][] entityNeeds = _planet.entityNeeds;
        EjectorComponent[] ejectorPool = _planet.factorySystem.ejectorPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        float[] networkServes = _planet.powerSystem.networkServes;
        int[] consumeRegister = _planet.gameData.statistics.production.factoryStatPool[_planet.index].consumeRegister;
        AstroData[]? astroPoses = null;
        lock (ejectorPool)
        {
            if (_planet.factorySystem.ejectorCount > 0)
            {
                astroPoses = _planet.planet.galaxy.astrosData;
            }
        }
        DysonSwarm? swarm = null;
        if (_planet.dysonSphere != null)
        {
            swarm = _planet.dysonSphere.swarm;
        }
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.ejectorCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref EjectorComponent reference2 = ref ejectorPool[i];
            if (reference2.id != i)
            {
                continue;
            }
            int entityId = reference2.entityId;
            float power = networkServes[consumerPool[reference2.pcId].networkId];
            uint num5 = reference2.InternalUpdate(power, time, swarm, astroPoses, entityAnimPool, consumeRegister);
            entityNeeds[entityId] = reference2.needs;
            if (isActive)
            {
                entityAnimPool[entityId].state = num5;
                if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                {
                    entitySignPool[entityId].signType = ((reference2.orbitId <= 0 && !reference2.autoOrbit) ? 5u : 0u);
                }
            }
            reference2.SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Ejector, workerIndex);
    }

    private void ParallelSiloGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Silo, workerIndex);
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        int[][] entityNeeds = _planet.entityNeeds;
        SiloComponent[] siloPool = _planet.factorySystem.siloPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        float[] networkServes = _planet.powerSystem.networkServes;
        int[] consumeRegister = _planet.gameData.statistics.production.factoryStatPool[_planet.index].consumeRegister;
        DysonSphere dysonSphere = _planet.dysonSphere;
        bool flag4 = dysonSphere != null && dysonSphere.autoNodeCount > 0;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.siloCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref SiloComponent reference2 = ref siloPool[i];
            if (reference2.id != i)
            {
                continue;
            }
            int entityId = reference2.entityId;
            float power = networkServes[consumerPool[reference2.pcId].networkId];
            uint num5 = reference2.InternalUpdate(power, dysonSphere, entityAnimPool, consumeRegister);
            entityNeeds[entityId] = reference2.needs;
            if (isActive)
            {
                entityAnimPool[entityId].state = num5;
                if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                {
                    entitySignPool[entityId].signType = ((!flag4) ? 9u : 0u);
                }
            }
            reference2.SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Silo, workerIndex);
    }

    private void ParallelAssemblerGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Assembler, workerIndex);
        GameLogic gameLogic = GameMain.logic;
        bool flag3 = isActive || (time + _planet.index) % 15 == 0;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        int[][] entityNeeds = _planet.entityNeeds;
        AssemblerComponent[] assemblerPool = _planet.factorySystem.assemblerPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        float[] networkServes = _planet.powerSystem.networkServes;
        FactoryProductionStat obj = _planet.gameData.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.assemblerCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref AssemblerComponent reference2 = ref assemblerPool[i];
            if (reference2.id != i)
            {
                continue;
            }
            int entityId = reference2.entityId;
            if (flag3)
            {
                uint num5 = 0u;
                float num6 = networkServes[consumerPool[reference2.pcId].networkId];
                if (reference2.recipeId != 0)
                {
                    reference2.UpdateNeeds();
                    num5 = reference2.InternalUpdate(num6, productRegister, consumeRegister);
                }
                if (reference2.recipeType == ERecipeType.Chemical)
                {
                    entityAnimPool[entityId].working_length = 2f;
                    entityAnimPool[entityId].Step(num5, gameLogic.deltaTime * num6);
                    entityAnimPool[entityId].power = num6;
                    entityAnimPool[entityId].working_length = reference2.recipeId;
                }
                else
                {
                    entityAnimPool[entityId].Step(num5, gameLogic.deltaTime * num6);
                    entityAnimPool[entityId].power = num6;
                }
                entityNeeds[entityId] = reference2.needs;
                if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                {
                    entitySignPool[entityId].signType = ((reference2.recipeId == 0) ? 4u : ((num5 == 0) ? 6u : 0u));
                }
            }
            else
            {
                float power = networkServes[consumerPool[reference2.pcId].networkId];
                if (reference2.recipeId != 0)
                {
                    reference2.UpdateNeeds();
                    reference2.InternalUpdate(power, productRegister, consumeRegister);
                }
                entityNeeds[entityId] = reference2.needs;
            }
            reference2.SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Assembler, workerIndex);
    }

    private void ParallelLabProduceGameTick(int workerIndex, long time, bool isActive)
    {
        DeepProfiler.BeginSample(DPEntry.Lab, workerIndex);
        GameLogic gameLogic = GameMain.logic;
        bool flag4 = isActive || (time + _planet.index) % 15 == 0;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        int[][] entityNeeds = _planet.entityNeeds;
        LabComponent[] labPool = _planet.factorySystem.labPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        float[] networkServes = _planet.powerSystem.networkServes;
        FactoryProductionStat obj = _planet.gameData.statistics.production.factoryStatPool[_planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.labCursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref LabComponent reference2 = ref labPool[i];
            if (reference2.id != i || reference2.researchMode)
            {
                continue;
            }
            int entityId = reference2.entityId;
            float power = networkServes[consumerPool[reference2.pcId].networkId];
            if (reference2.recipeId > 0)
            {
                reference2.UpdateNeedsAssemble();
                uint num5 = reference2.InternalUpdateAssemble(power, productRegister, consumeRegister);
                if (isActive)
                {
                    entityAnimPool[entityId].working_length = LabComponent.matrixShaderStates[num5];
                    entityAnimPool[entityId].prepare_length = 0f;
                    entityAnimPool[entityId].power = power;
                    entityAnimPool[entityId].Step01(num5, gameLogic.deltaTime);
                    if (flag4 && (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3))
                    {
                        entitySignPool[entityId].signType = ((!reference2.researchMode) ? ((reference2.recipeId == 0) ? 4u : ((num5 == 0) ? 6u : 0u)) : ((num5 == 0) ? 6u : 0u));
                    }
                }
            }
            entityNeeds[entityId] = reference2.needs;
        }
        DeepProfiler.EndSample(DPEntry.Lab, workerIndex);
    }

    private void ParallelInserterGameTick(int workerIndex, long time, bool isActive)
    {
        InserterComponent[] inserterPool = _planet.factorySystem.inserterPool;
        PowerSystem powerSystem = _planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        CargoTraffic cargoTraffic = _planet.cargoTraffic;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        int[][] entityNeeds = _planet.entityNeeds;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = _planet.entityPool;
        BeltComponent[] beltPool = _planet.cargoTraffic.beltPool;
        bool isTimeForOffsetCorrection = time % 60 == 0;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.factorySystem.inserterCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.Inserter, workerIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref InserterComponent component = ref inserterPool[i];
            if (component.id != i)
            {
                continue;
            }

            float power = networkServes[consumerPool[component.pcId].networkId];
            if (isTimeForOffsetCorrection)
            {
                component.InternalOffsetCorrection(entityPool, cargoTraffic, beltPool);
            }
            if (component.bidirectional)
            {
                component.InternalUpdate_Bidirectional(_planet, entityNeeds, entityAnimPool, power, isActive);
            }
            else if (isActive)
            {
                component.InternalUpdate(_planet, entityNeeds, entityAnimPool, power);
            }
            else
            {
                component.InternalUpdateNoAnim(_planet, entityNeeds, power);
            }
            component.SetPCState(consumerPool);
        }
        DeepProfiler.EndSample(DPEntry.Inserter, workerIndex);
    }

    private void ParallelCargoPathsGameTick(int workerIndex)
    {
        CargoPath[] pathPool = _planet.cargoTraffic.pathPool;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.cargoTraffic.pathCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.Belt, workerIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            CargoPath cargoPath = pathPool[i];
            if (cargoPath == null ||
                cargoPath.id != i)
            {
                continue;
            }

            cargoPath.Update();
        }
        DeepProfiler.EndSample(DPEntry.Belt, workerIndex);
    }

    private void ParallelCargoTrafficMiscGameTick(int workerIndex)
    {
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        DigitalSystem digitalSystem = _planet.digitalSystem;
        EntityData[] entityPool = _planet.entityPool;
        PowerConsumerComponent[] consumerPool = _planet.powerSystem.consumerPool;
        CargoTraffic cargoTraffic = _planet.cargoTraffic;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;

        if (cargoTraffic.monitorCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Monitor, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.monitorCursor, _maxWorkCount, _workIndex);
            MonitorComponent[] monitorPool = cargoTraffic.monitorPool;
            for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
            {
                ref MonitorComponent component = ref monitorPool[i];
                if (component.id != i)
                {
                    continue;
                }

                component.InternalUpdate(cargoTraffic, sandboxToolsEnabled, entityPool, digitalSystem, entityAnimPool);
                component.SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Monitor, workerIndex);
        }

        if (cargoTraffic.spraycoaterCursor > 0)
        {
            int[] consumeRegister = GameMain.statistics.production.factoryStatPool[_planet.index].consumeRegister;
            DeepProfiler.BeginSample(DPEntry.Spraycoater, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.spraycoaterCursor, _maxWorkCount, _workIndex);
            SpraycoaterComponent[] spraycoaterPool = cargoTraffic.spraycoaterPool;
            for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
            {
                ref SpraycoaterComponent component = ref spraycoaterPool[i];
                if (component.id != i)
                {
                    continue;
                }

                component.InternalUpdate(cargoTraffic, entityAnimPool, consumeRegister);
                component.SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Spraycoater, workerIndex);
        }

        if (cargoTraffic.pilerCursor > 0)
        {
            DeepProfiler.BeginSample(DPEntry.Piler, workerIndex);
            (int startIndex, int workLength) = GetWorkChunkIndices(cargoTraffic.pilerCursor, _maxWorkCount, _workIndex);
            PilerComponent[] pilerPool = cargoTraffic.pilerPool;
            for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
            {
                ref PilerComponent component = ref pilerPool[i];
                if (component.id != i)
                {
                    continue;
                }

                pilerPool[i].InternalUpdate(cargoTraffic, entityAnimPool);
                pilerPool[i].SetPCState(consumerPool);
            }
            DeepProfiler.EndSample(DPEntry.Piler, workerIndex);
        }
    }

    private void ParallelPresentCargoPathsGameTick(int workerIndex)
    {
        CargoPath[] pathPool = _planet.cargoTraffic.pathPool;
        (int startIndex, int workLength) = GetWorkChunkIndices(_planet.cargoTraffic.pathCursor, _maxWorkCount, _workIndex);
        if (workLength == 0)
        {
            return;
        }

        DeepProfiler.BeginSample(DPEntry.CargoPresent, workerIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            CargoPath cargoPath = pathPool[i];
            if (cargoPath == null ||
                cargoPath.id != i)
            {
                continue;
            }

            cargoPath.PresentCargos();
        }
        DeepProfiler.EndSample(DPEntry.CargoPresent, workerIndex);
    }

    internal static (int startIndex, int workLength) GetWorkChunkIndices(int totalLength, int maxWorkCount, int workIndex)
    {
        int workChunkLength = ((totalLength + maxWorkCount - 1) / maxWorkCount);
        int startIndex = workChunkLength * workIndex;
        int workLength = Math.Min(workChunkLength, totalLength - startIndex);
        if (workLength <= 0)
        {
            return (0, 0);
        }
        return (startIndex, workLength);
    }
}
