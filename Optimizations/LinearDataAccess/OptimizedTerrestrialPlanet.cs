using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.Miners;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;
using Weaver.Optimizations.LinearDataAccess.Statistics;
using Weaver.Optimizations.LinearDataAccess.Turrets;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess;

internal sealed class OptimizedTerrestrialPlanet : IOptimizedPlanet
{
    public static bool ViewBeltsOnLocalOptimizedPlanet = false;
    private readonly PlanetFactory _planet;
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private readonly DysonSphereManager _dysonSphereManager;
    private OptimizedSubFactory[] _subFactories = null!;
    private OptimizedPowerSystem _optimizedPowerSystem = null!;
    private TurretExecutor _turretExecutor = null!;
    private OptimizedPlanetWideProductionStatistics _optimizedPlanetWideProductionStatistics = null!;
    public OptimizedPlanetStatus Status { get; private set; } = OptimizedPlanetStatus.Stopped;
    public int OptimizeDelayInTicks { get; set; } = 0;

    private WorkStep[]? _workSteps;
    private int _workStepsParallelism;

    public OptimizedTerrestrialPlanet(PlanetFactory planet,
                                      StarClusterResearchManager starClusterResearchManager,
                                      DysonSphereManager dysonSphereManager)
    {
        _planet = planet;
        _starClusterResearchManager = starClusterResearchManager;
        _dysonSphereManager = dysonSphereManager;
    }

    public void Save()
    {
        CargoContainer cargoContainer = _planet.cargoTraffic.container;
        cargoContainer.recycleBegin = 0;
        cargoContainer.recycleEnd = 0;
        cargoContainer.cursor = 0;

        foreach (var subFactory in _subFactories)
        {
            subFactory.Save(cargoContainer);
        }

        _optimizedPowerSystem.Save(_planet);

        if (ViewBeltsOnLocalOptimizedPlanet)
        {
            _planet.cargoTraffic.PresentCargoPathsSync();
        }
        else
        {
            Status = OptimizedPlanetStatus.Stopped;
            _workSteps = null;
            _workStepsParallelism = -1;
        }
    }

    public void Initialize()
    {
        List<Graph> subFactoryGraphs = Graphifier.ToGraphs(_planet);
        Graphifier.CombineSmallGraphs(subFactoryGraphs);

        //WeaverFixes.Logger.LogMessage($"Sub Factory count: {subFactoryGraphs.Count}");

        var planetWideProductionRegisterBuilder = new PlanetWideProductionRegisterBuilder(_planet);
        var optimizedPowerSystemBuilder = OptimizedPowerSystemBuilder.Create(_planet, planetWideProductionRegisterBuilder.GetSubFactoryBuilder(), out OptimizedItemId[]?[]? fuelNeeds);
        var planetWideBeltExecutor = new PlanetWideBeltExecutor();
        var turretExecutorBuilder = new TurretExecutorBuilder();

        _subFactories = new OptimizedSubFactory[subFactoryGraphs.Count];
        for (int i = 0; i < _subFactories.Length; i++)
        {
            _subFactories[i] = new OptimizedSubFactory(_planet, this, _starClusterResearchManager);
            _subFactories[i].Initialize(subFactoryGraphs[i],
                                        optimizedPowerSystemBuilder,
                                        planetWideBeltExecutor,
                                        turretExecutorBuilder,
                                        planetWideProductionRegisterBuilder,
                                        planetWideProductionRegisterBuilder.GetSubFactoryBuilder(),
                                        fuelNeeds);
        }

        _optimizedPowerSystem = optimizedPowerSystemBuilder.Build(_dysonSphereManager, planetWideBeltExecutor);
        _turretExecutor = turretExecutorBuilder.Build();
        _optimizedPlanetWideProductionStatistics = planetWideProductionRegisterBuilder.Build();

        Status = OptimizedPlanetStatus.Running;

        _workSteps = null;
        _workStepsParallelism = -1;
    }

    public void GameTickDefense(long time)
    {
        bool isActive = GameMain.localPlanet == _planet.planet;
        if (Status == OptimizedPlanetStatus.Running)
        {
            DefenseGameTick(_planet.defenseSystem, time);
        }
        else
        {
            _planet.defenseSystem.GameTick(time, isActive);
        }
        _planet.planetATField.GameTick(time, isActive);
    }

    public WorkStep[] GetMultithreadedWork(int maxParallelism)
    {
        if (_workSteps == null || _workStepsParallelism != maxParallelism)
        {
            _workSteps = CreateMultithreadedWork(maxParallelism);
            _workStepsParallelism = maxParallelism;
        }

        return _workSteps;
    }

    private WorkStep[] CreateMultithreadedWork(int maxParallelism)
    {
        if (Status == OptimizedPlanetStatus.Stopped)
        {
            return CreateParallelWorkForNonRunningOptimizedPlanet(maxParallelism);
        }

        if (_subFactories.Length == 0)
        {
            return [];
        }

        List<WorkStep> workSteps = [];

        workSteps.Add(new WorkStep([new PlanetWideBeforePower(this)]));
        workSteps.Add(new WorkStep([new PlanetWidePower(this)]));

        List<IWorkChunk> gameTickChunks = [];
        foreach (OptimizedSubFactory subFactory in _subFactories)
        {
            gameTickChunks.Add(new SubFactoryGameTick(subFactory, _optimizedPowerSystem.GetSubFactoryPowerConsumption(subFactory)));
        }
        workSteps.Add(new WorkStep(gameTickChunks.ToArray()));

        //workSteps.Add(new WorkStep([new PlanetWideTransport(this)]));
        //workSteps.Add(new WorkStep([new PlanetWideDigitalSystem(this)]));
        workSteps.Add(new WorkStep([new PostSubFactoryStep(this)]));

        return workSteps.ToArray();
    }

    public void BeforePowerStep(long time)
    {
        _planet.transport.GameTickBeforePower(time, false);
        _planet.defenseSystem.GameTickBeforePower(time, false);
        _planet.digitalSystem.GameTickBeforePower(time, false);
    }

    public void PowerStep(long time)
    {
        _optimizedPowerSystem.GameTick(_planet, time);
    }

    public void TransportGameTick(long time, UnityEngine.Vector3 playerPos)
    {
        PlanetTransport transport = _planet.transport;
        DispenserGameTick_SandboxMode(transport);
        GameHistoryData history = GameMain.history;
        float[] networkServes = transport.powerSystem.networkServes;
        PowerConsumerComponent[] consumerPool = transport.powerSystem.consumerPool;

        double num5 = Math.Cos((double)history.dispenserDeliveryMaxAngle * Math.PI / 180.0);
        if (num5 < -0.999)
        {
            num5 = -1.0;
        }
        playerPos += playerPos.normalized * 2.66666f;
        bool num6 = transport.playerDeliveryEnabled;
        transport.DeterminePlayerDeliveryEnabled(transport.factory);
        if (num6 != transport.playerDeliveryEnabled)
        {
            transport.RefreshDispenserTraffic(-10000);
        }
        for (int k = 1; k < transport.dispenserCursor; k++)
        {
            if (transport.dispenserPool[k] != null && transport.dispenserPool[k].id == k)
            {
                float power2 = networkServes[consumerPool[transport.dispenserPool[k].pcId].networkId];
                transport.dispenserPool[k].InternalTick(transport.factory, transport.factory.entityPool, transport.dispenserPool, playerPos, time, power2, history.logisticCourierSpeedModified, history.logisticCourierCarries, num5);
            }
        }
    }

    private static void DispenserGameTick_SandboxMode(PlanetTransport transport)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }
        for (int j = 1; j < transport.dispenserCursor; j++)
        {
            if (transport.dispenserPool[j] != null && transport.dispenserPool[j].id == j)
            {
                transport.dispenserPool[j].UpdateKeepMode();
            }
        }
    }

    public void DigitalSystemStep()
    {
        _planet.digitalSystem.GameTick(false);
    }

    public void AggregateSubFactoryDataStep(long time)
    {
        _optimizedPlanetWideProductionStatistics.UpdateStatistics(time);
    }

    private void DefenseGameTick(DefenseSystem defenseSystem, long tick)
    {
        GameHistoryData history = GameMain.history;
        PowerSystem powerSystem = defenseSystem.factory.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        PowerNodeComponent[] nodePool = powerSystem.nodePool;
        EntityData[] entityPool = defenseSystem.factory.entityPool;
        AnimData[] entityAnimPool = defenseSystem.factory.entityAnimPool;
        ref CombatSettings combatSettings = ref defenseSystem.factory.gameData.history.combatSettings;
        CombatUpgradeData combatUpgradeData = default(CombatUpgradeData);
        history.GetCombatUpgradeData(ref combatUpgradeData);
        defenseSystem.UpdateMatchSpaceEnemies();
        EAggressiveLevel aggressiveLevel = combatSettings.aggressiveLevel;
        int cursor = defenseSystem.beacons.cursor;
        BeaconComponent[] buffer = defenseSystem.beacons.buffer;
        for (int i = 1; i < cursor; i++)
        {
            ref BeaconComponent reference = ref buffer[i];
            if (reference.id == i)
            {
                float power = networkServes[nodePool[reference.pnId].networkId];
                PrefabDesc pdesc = PlanetFactory.PrefabDescByModelIndex[entityPool[reference.entityId].modelIndex];
                reference.GameTick(defenseSystem.factory, pdesc, aggressiveLevel, power, tick);
                if (reference.DeterminActiveEnemyUnits(isSpace: false, tick))
                {
                    reference.ActiveEnemyUnits_Ground(defenseSystem.factory, pdesc);
                }
                if (reference.DeterminActiveEnemyUnits(isSpace: true, tick))
                {
                    reference.ActiveEnemyUnits_Space(defenseSystem.factory, pdesc);
                }
            }
        }
        bool flag2 = false;
        for (int num3 = defenseSystem.localGlobalTargetCursor - 1; num3 >= 0; num3--)
        {
            defenseSystem.globalTargets[num3].lifeTick--;
            if (defenseSystem.globalTargets[num3].lifeTick <= 0)
            {
                defenseSystem.RemoveGlobalTargets(num3);
                flag2 = true;
            }
        }
        if (flag2)
        {
            defenseSystem.ArrangeGlobalTargets();
        }
        defenseSystem.UpdateOtherGlobalTargets();
        defenseSystem.engagingGaussCount = 0;
        defenseSystem.engagingLaserCount = 0;
        defenseSystem.engagingCannonCount = 0;
        defenseSystem.engagingMissileCount = 0;
        defenseSystem.engagingPlasmaCount = 0;
        defenseSystem.engagingLocalPlasmaCount = 0;
        defenseSystem.engagingTurretTotalCount = 0;
        defenseSystem.turretEnableDefenseSpace = false;
        int num2 = _turretExecutor.GameTick(defenseSystem, tick, ref combatUpgradeData);
        defenseSystem.engagingTurretTotalCount = defenseSystem.engagingGaussCount + defenseSystem.engagingLaserCount + defenseSystem.engagingCannonCount + defenseSystem.engagingPlasmaCount + defenseSystem.engagingMissileCount + defenseSystem.engagingLocalPlasmaCount;
        if (num2 < 300)
        {
            defenseSystem.incomingSupernovaTime = num2;
        }
        else
        {
            defenseSystem.incomingSupernovaTime = 0;
        }
        int cursor3 = defenseSystem.fieldGenerators.cursor;
        FieldGeneratorComponent[] buffer3 = defenseSystem.fieldGenerators.buffer;
        for (int k = 1; k < cursor3; k++)
        {
            ref FieldGeneratorComponent reference3 = ref buffer3[k];
            if (reference3.id != k)
            {
                continue;
            }
            ref PowerConsumerComponent reference4 = ref consumerPool[reference3.pcId];
            float num11 = networkServes[reference4.networkId];
            reference3.InternalUpdate(defenseSystem.factory, num11, ref reference4, ref entityAnimPool[reference3.entityId]);
        }
    }

    public void DefenseGameTickUIThread(long tick)
    {
        DefenseSystem defenseSystem = _planet.defenseSystem;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[defenseSystem.factory.index];
        int[] productRegister = obj.productRegister;
        PowerSystem powerSystem = defenseSystem.factory.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        AnimData[] entityAnimPool = defenseSystem.factory.entityAnimPool;
        VectorLF3 relativePos = defenseSystem.factory.gameData.relativePos;
        UnityEngine.Quaternion relativeRot = defenseSystem.factory.gameData.relativeRot;
        TrashSystem trashSystem = defenseSystem.factory.gameData.trashSystem;
        bool flag = trashSystem.trashCount > 0;
        float num = 1f / 60f;
        int num13 = (int)(tick % 4);
        BattleBaseComponent[] buffer4 = defenseSystem.battleBases.buffer;
        for (int l = 1; l < defenseSystem.battleBases.cursor; l++)
        {
            BattleBaseComponent battleBaseComponent = buffer4[l];
            if (battleBaseComponent != null && battleBaseComponent.id == l)
            {
                float power2 = networkServes[consumerPool[battleBaseComponent.pcId].networkId];
                battleBaseComponent.InternalUpdate(num, defenseSystem.factory, power2, ref entityAnimPool[battleBaseComponent.entityId]);
                if (flag && battleBaseComponent.autoPickEnabled && battleBaseComponent.energy > 0 && (long)l % 4L == num13)
                {
                    battleBaseComponent.AutoPickTrash(defenseSystem.factory, trashSystem, tick, ref relativePos, ref relativeRot, productRegister);
                }
            }
        }
    }

    public void AddMiningFlags(MiningFlags miningFlags)
    {
        if (miningFlags.MiningFlag == 0 &&
            miningFlags.VeinMiningFlag == 0)
        {
            return;
        }

        lock (this)
        {
            _planet._miningFlag |= miningFlags.MiningFlag;
            _planet._veinMiningFlag |= miningFlags.VeinMiningFlag;
        }
    }

    public void RefreshPowerGenerationCapacites(ProductionStatistics statistics, PlanetFactory planet)
    {
        _optimizedPowerSystem.RefreshPowerGenerationCapacites(statistics, planet);
    }

    public void RefreshPowerConsumptionDemands(ProductionStatistics statistics, PlanetFactory planet)
    {
        foreach (OptimizedSubFactory subFactory in _subFactories)
        {
            subFactory.RefreshPowerConsumptionDemands(statistics, _optimizedPowerSystem.GetSubFactoryPowerConsumption(subFactory));
        }

        _optimizedPowerSystem.RefreshPowerConsumptionDemands(statistics, planet);
    }

    private WorkStep[] CreateParallelWorkForNonRunningOptimizedPlanet(int maxParallelism)
    {
        List<WorkStep> work = [];

        int minerCount = _planet.factorySystem.minerCursor;
        int assemblerCount = _planet.factorySystem.assemblerCursor;
        int fractionatorCount = _planet.factorySystem.fractionatorCursor;
        int ejectorCount = _planet.factorySystem.ejectorCursor;
        int siloCount = _planet.factorySystem.siloCursor;

        int monitorCount = _planet.cargoTraffic.monitorCursor;
        int spraycoaterCount = _planet.cargoTraffic.spraycoaterCursor;
        int pilerCount = _planet.cargoTraffic.pilerCursor;
        int splitterCount = _planet.cargoTraffic.splitterCursor;
        int cargoPathCount = _planet.cargoTraffic.pathCursor;

        int stationCount = _planet.transport.stationCursor;
        int dispenserCount = _planet.transport.dispenserCursor;
        int transportEntities = stationCount + dispenserCount;

        int turretCount = _planet.defenseSystem.turrets.cursor;
        int fieldGeneratorCount = _planet.defenseSystem.fieldGenerators.cursor;
        int battleBaseCount = _planet.defenseSystem.battleBases.cursor;

        int markerCount = _planet.digitalSystem.markers.cursor;

        int inserterCount = _planet.factorySystem.inserterCursor;

        int producingLabCount = _planet.factorySystem.labCursor;
        int researchingLabCount = _planet.factorySystem.labCursor;
        int labCount = _planet.factorySystem.labCursor;

        int storageCount = _planet.factoryStorage.storageCursor;
        int tankCount = _planet.factoryStorage.tankCursor;

        int totalEntities = minerCount +
                            assemblerCount +
                            fractionatorCount +
                            ejectorCount +
                            siloCount +
                            monitorCount +
                            spraycoaterCount +
                            pilerCount +
                            stationCount +
                            dispenserCount +
                            turretCount +
                            fieldGeneratorCount +
                            battleBaseCount +
                            markerCount;

        const int minimumWorkPerCore = 5_000;
        int beforePowerWorkCount = ((totalEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        beforePowerWorkCount = Math.Min(beforePowerWorkCount, maxParallelism);
        if (beforePowerWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.BeforePower, beforePowerWorkCount)));
        }

        int powerNetworkCount = _planet.powerSystem.netCursor;
        if (powerNetworkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Power, 1)));
        }

        int assemblerStepEntityCount = minerCount +
                                       assemblerCount +
                                       fractionatorCount +
                                       ejectorCount +
                                       siloCount +
                                       producingLabCount;
        int assemblerWorkCount = ((assemblerStepEntityCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        assemblerWorkCount = Math.Min(assemblerWorkCount, maxParallelism);
        if (assemblerWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Assembler, assemblerWorkCount)));
        }

        if (researchingLabCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.LabResearchMode, 1)));
        }

        int labOutput2NextWorkCount = ((labCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        labOutput2NextWorkCount = Math.Min(labOutput2NextWorkCount, maxParallelism);
        if (labOutput2NextWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.LabOutput2NextData, labOutput2NextWorkCount)));
        }

        if (transportEntities > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.TransportData, 1)));
        }

        if (stationCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.InputFromBelt, 1)));
        }

        int inserterWorkCount = ((inserterCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        inserterWorkCount = Math.Min(inserterWorkCount, maxParallelism);
        if (inserterWorkCount > 0)
        {
            // Work count set to 1 because of lock in cargo container which causes inserters that interact with
            // belts to wait for each other
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.InserterData, 1)));
        }

        if ((storageCount + tankCount) > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Storage, 1)));
        }

        int cargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        cargoPathsWorkCount = Math.Min(cargoPathsWorkCount, maxParallelism);
        if (cargoPathsWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.CargoPathsData, cargoPathsWorkCount)));
        }

        if (splitterCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Splitter, 1)));
        }


        if (monitorCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Monitor, 1)));
        }

        if (spraycoaterCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Spraycoater, 1)));
        }

        if (pilerCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Piler, 1)));
        }

        if (stationCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.OutputToBelt, 1)));
        }

        int sandboxModeWorkCount = ((transportEntities + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        sandboxModeWorkCount = Math.Min(sandboxModeWorkCount, maxParallelism);
        if (GameMain.sandboxToolsEnabled && sandboxModeWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.SandboxMode, sandboxModeWorkCount)));
        }

        int presentCargoPathsWorkCount = ((cargoPathCount + (minimumWorkPerCore - 1)) / minimumWorkPerCore);
        presentCargoPathsWorkCount = Math.Min(presentCargoPathsWorkCount, maxParallelism);
        if (presentCargoPathsWorkCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.PresentCargoPathsData, presentCargoPathsWorkCount)));
        }

        if (markerCount > 0)
        {
            work.Add(new WorkStep(UnOptimizedPlanetWorkChunk.CreateDuplicateChunks(_planet, WorkType.Digital, 1)));
        }

        return work.ToArray();
    }
}