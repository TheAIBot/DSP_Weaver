using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Labs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Researching;

internal sealed class ResearchingLabExecutor
{
    private readonly StarClusterResearchManager _starClusterResearchManager = null!;
    private int[] _matrixPoints = null!;
    private OptimizedItemId[]? _matrixIds = null!;
    public NetworkIdAndState<LabState>[] _networkIdAndStates = null!;
    public OptimizedResearchingLab[] _optimizedLabs = null!;
    public LabPowerFields[] _labsPowerFields = null!;
    public int[] _entityIds = null!;
    public Dictionary<int, int> _labIdToOptimizedLabIndex = null!;
    public HashSet<int> _unOptimizedLabIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int[] _matrixServed = null!;
    public int[] _matrixIncServed = null!;

    public int ResearchingLabCount => _optimizedLabs.Length;

    public ResearchingLabExecutor(StarClusterResearchManager starClusterResearchManager)
    {
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void GameTickLabResearchMode(PlanetFactory planet,
                                        int[] researchingLabPowerConsumerIndexes,
                                        PowerConsumerType[] powerConsumerTypes,
                                        long[] thisSubFactoryNetworkPowerConsumption,
                                        int[] consumeRegister,
                                        SubFactoryNeeds subFactoryNeeds)
    {
        lock (_starClusterResearchManager)
        {
            FactorySystem factorySystem = planet.factorySystem;
            GameHistoryData history = GameMain.history;
            GameStatData statistics = GameMain.statistics;
            FactoryProductionStat factoryProductionStat = statistics.production.factoryStatPool[planet.index];
            SignData[] entitySignPool = planet.entitySignPool;
            PowerSystem powerSystem = planet.powerSystem;
            float[] networkServes = powerSystem.networkServes;
            NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
            OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
            LabPowerFields[] labsPowerFields = _labsPowerFields;
            int num = history.currentTech;
            TechProto techProto = LDB.techs.Select(num);
            TechState ts = default;
            bool flag2 = false;
            float research_speed = history.techSpeed;
            int techHashedThisFrame = statistics.techHashedThisFrame;
            long uMatrixPoint = history.universeMatrixPointUploaded;
            long hashRegister = factoryProductionStat.hashRegister;
            if (num > 0 && techProto != null && techProto.IsLabTech && GameMain.history.techStates.ContainsKey(num))
            {
                ts = history.techStates[num];
                flag2 = true;
            }
            if (!flag2)
            {
                num = 0;
            }

            // Handle rare case where optimizing/deoptimizing causes
            // matrix points to be set incorrectly while researching
            // technology. This could cause labs to not consume 
            // anything while still adding hashes to the research.
            if (flag2 && optimizedLabs.Length > 0)
            {
                Array.Clear(_matrixPoints, 0, _matrixPoints.Length);
                if (techProto != null && techProto.IsLabTech)
                {
                    for (int i = 0; i < techProto.Items.Length; i++)
                    {
                        int num46779 = techProto.Items[i] - LabComponent.matrixIds[0];
                        if (num46779 >= 0 && num46779 < _matrixPoints.Length)
                        {
                            _matrixPoints[num46779] = techProto.ItemPoints[i];
                        }
                    }
                }
            }

            GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ResearchingLab);
            short[] needs = subFactoryNeeds.Needs;

            if (factorySystem.researchTechId != num)
            {
                factorySystem.researchTechId = num;

                int[] entityIds = _entityIds;
                for (int i = 0; i < optimizedLabs.Length; i++)
                {
                    optimizedLabs[i].SetFunction(entityIds[i],
                                                 factorySystem.researchTechId,
                                                 entitySignPool,
                                                 ref labsPowerFields[i],
                                                 groupNeeds,
                                                 needs,
                                                 i);
                }
            }

            // There is no reasearching labs if this is null
            if (_matrixIds == null)
            {
                return;
            }

            int[] matrixServed = _matrixServed;
            int[] matrixIncServed = _matrixIncServed;

            for (int labIndex = 0; labIndex < optimizedLabs.Length; labIndex++)
            {
                ref NetworkIdAndState<LabState> networkIdAndState = ref networkIdAndStates[labIndex];
                ref LabPowerFields labPowerFields = ref labsPowerFields[labIndex];
                if ((LabState)networkIdAndState.State != LabState.Active)
                {
                    UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIdAndState.Index, labPowerFields);
                    continue;
                }

                ref OptimizedResearchingLab reference = ref optimizedLabs[labIndex];
                reference.UpdateNeedsResearch(groupNeeds, needs, matrixServed, labIndex);
                if (flag2)
                {
                    int curLevel = ts.curLevel;
                    float power = networkServes[networkIdAndState.Index];
                    networkIdAndState.State = (int)reference.InternalUpdateResearch(power,
                                                                                    research_speed,
                                                                                    factorySystem.researchTechId,
                                                                                    _matrixPoints,
                                                                                    _matrixIds,
                                                                                    consumeRegister,
                                                                                    ref ts,
                                                                                    ref techHashedThisFrame,
                                                                                    ref uMatrixPoint,
                                                                                    ref hashRegister,
                                                                                    ref labPowerFields,
                                                                                    groupNeeds,
                                                                                    matrixServed,
                                                                                    matrixIncServed,
                                                                                    labIndex);
                    if (ts.unlocked)
                    {
                        history.techStates[factorySystem.researchTechId] = ts;
                        _starClusterResearchManager.AddResearchedTech(factorySystem.researchTechId, curLevel, ts, techProto!);
                        history.DequeueTech();
                        flag2 = false;
                    }
                    if (ts.curLevel > curLevel)
                    {
                        history.techStates[factorySystem.researchTechId] = ts;
                        _starClusterResearchManager.AddResearchedTech(factorySystem.researchTechId, curLevel, ts, techProto!);
                        history.DequeueTech();
                        flag2 = false;
                    }
                }

                UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIdAndState.Index, labPowerFields);
            }

            history.techStates[factorySystem.researchTechId] = ts;
            statistics.techHashedThisFrame = techHashedThisFrame;
            history.universeMatrixPointUploaded = uMatrixPoint;
            factoryProductionStat.hashRegister = hashRegister;
        }
    }

    public void GameTickLabOutputToNext(SubFactoryNeeds subFactoryNeeds)
    {
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ResearchingLab);
        short[] needs = subFactoryNeeds.Needs;
        int[] matrixServed = _matrixServed;
        int[] matrixIncServed = _matrixIncServed;
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        for (int i = (int)(GameMain.gameTick % 5); i < optimizedLabs.Length; i += 5)
        {
            optimizedLabs[i].UpdateOutputToNext(i,
                                                optimizedLabs,
                                                networkIdAndStates,
                                                groupNeeds,
                                                needs,
                                                matrixServed,
                                                matrixIncServed);
        }
    }

    public void UpdatePower(int[] researchingLabPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int labIndex = 0; labIndex < networkIdAndStates.Length; labIndex++)
        {
            int networkIndex = networkIdAndStates[labIndex].Index;
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
        }
    }

    private static void UpdatePower(int[] researchingLabPowerConsumerIndexes,
                                PowerConsumerType[] powerConsumerTypes,
                                long[] thisSubFactoryNetworkPowerConsumption,
                                int labIndex,
                                int networkIndex,
                                LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = researchingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] researchingLabPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        LabPowerFields[] labsPowerFields = _labsPowerFields;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int labIndex = 0; labIndex < labsPowerFields.Length; labIndex++)
        {
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePowerConsumptionPerPrototype(researchingLabPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               labIndex,
                                               labPowerFields);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] researchingLabPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int labIndex,
                                                           LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = researchingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[labIndex]] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        SignData[] entitySignPool = planet.entitySignPool;
        LabComponent[] labComponents = planet.factorySystem.labPool;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        int researchTechId = planet.factorySystem.researchTechId;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ResearchingLab);
        short[] needs = subFactoryNeeds.Needs;
        int[] matrixServed = _matrixServed;
        int[] matrixIncServed = _matrixIncServed;
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            if (!_labIdToOptimizedLabIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref LabComponent unoptimizedLab = ref labComponents[i];
            optimizedLabs[optimizedIndex].Save(ref unoptimizedLab,
                                               labsPowerFields[optimizedIndex],
                                               _matrixPoints,
                                               researchTechId,
                                               groupNeeds,
                                               needs,
                                               matrixServed,
                                               matrixIncServed,
                                               optimizedIndex);

            entitySignPool[unoptimizedLab.entityId].iconId0 = (uint)researchTechId;
            entitySignPool[unoptimizedLab.entityId].iconType = researchTechId != 0 ? 3u : 0u;
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder)
    {
        int[] matrixPoints = new int[LabComponent.matrixIds.Length];
        bool copiedMatrixPoints = false;
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedResearchingLab> optimizedLabs = [];
        List<LabPowerFields> labsPowerFields = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];
        HashSet<int> unOptimizedLabIds = [];
        List<int[]> matrixServed = [];
        List<int[]> matrixIncServed = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.ResearchingLab);

        foreach (int labIndex in subFactoryGraph.GetAllNodes()
                                                .Where(x => x.EntityTypeIndex.EntityType == EntityType.ResearchingLab)
                                                .Select(x => x.EntityTypeIndex.Index)
                                                .OrderBy(x => x))
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[labIndex];

            if (!copiedMatrixPoints && lab.matrixPoints != null)
            {
                Array.Copy(lab.matrixPoints, matrixPoints, matrixPoints.Length);
                copiedMatrixPoints = true;
            }

            int? nextLabIndex = null;
            if (planet.factorySystem.labPool[lab.nextLabId].id != 0 &&
                planet.factorySystem.labPool[lab.nextLabId].id == lab.nextLabId)
            {
                nextLabIndex = lab.nextLabId;
            }

            labIdToOptimizedLabIndex.Add(labIndex, optimizedLabs.Count);
            optimizedLabs.Add(new OptimizedResearchingLab(nextLabIndex, ref lab));
            labsPowerFields.Add(new LabPowerFields(in lab));
            int networkIndex = planet.powerSystem.consumerPool[lab.pcId].networkId;
            networkIdAndStates.Add(new NetworkIdAndState<LabState>((int)LabState.Active, networkIndex));
            entityIds.Add(lab.entityId);
            matrixServed.Add(lab.matrixServed);
            matrixIncServed.Add(lab.matrixIncServed);
            subFactoryPowerSystemBuilder.AddResearchingLab(in lab, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[lab.entityId]);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
            needsBuilder.AddNeeds(lab.needs, LabComponent.matrixIds.Length);
        }

        for (int i = 0; i < optimizedLabs.Count; i++)
        {
            OptimizedResearchingLab lab = optimizedLabs[i];
            if (lab.nextLabIndex == OptimizedResearchingLab.NO_NEXT_LAB)
            {
                continue;
            }

            if (!labIdToOptimizedLabIndex.TryGetValue(lab.nextLabIndex, out int nextOptimizedLabIndex))
            {
                throw new InvalidOperationException("Next lab index was not part of the converted research labs.");
            }

            optimizedLabs[i] = new OptimizedResearchingLab(nextOptimizedLabIndex, ref lab);
        }

        _matrixIds = null;
        if (optimizedLabs.Count > 0)
        {
            _matrixIds = subFactoryProductionRegisterBuilder.AddConsume(LabComponent.matrixIds);
        }

        if (optimizedLabs.Count > 0)
        {
            int maxServedSize = LabComponent.matrixIds.Length;
            List<int> matrixServedFlat = [];
            List<int> matrixIncServedFlat = [];

            for (int labIndex = 0; labIndex < optimizedLabs.Count; labIndex++)
            {
                for (int servedIndex = 0; servedIndex < maxServedSize; servedIndex++)
                {
                    matrixServedFlat.Add(GetOrDefault(matrixServed[labIndex], servedIndex));
                    matrixIncServedFlat.Add(GetOrDefault(matrixIncServed[labIndex], servedIndex));
                }
            }

            _matrixServed = matrixServedFlat.ToArray();
            _matrixIncServed = matrixIncServedFlat.ToArray();
        }

        _matrixPoints = matrixPoints.ToArray();
        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, LabPowerFields producingLabPowerFields)
    {
        return powerConsumerType.GetRequiredEnergy(producingLabPowerFields.replicating, 1000 + producingLabPowerFields.extraPowerRatio);
    }

    private static int GetOrDefault(int[] values, int index)
    {
        if (values.Length <= index)
        {
            return 0;
        }

        return values[index];
    }
}