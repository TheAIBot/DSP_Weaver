using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Researching;

internal sealed class ResearchingLabExecutor
{
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private OptimizedItemId[]? _matrixIds = null!;
    private ReadonlyArray<int> _labNetworkIds = default;
    public LabState[] _labStates = null!;
    public OptimizedResearchingLab[] _optimizedLabs = null!;
    public LabPowerFields[] _labsPowerFields = null!;
    public ReadonlyArray<int> _entityIds = default;
    public Dictionary<int, int> _labIdToOptimizedLabIndex = null!;
    public HashSet<int> _unOptimizedLabIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int[] _matrixServed = null!;
    public int[] _matrixIncServed = null!;

    public int Count => _optimizedLabs.Length;

    public ResearchingLabExecutor(StarClusterResearchManager starClusterResearchManager)
    {
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void GameTickLabResearchMode(PlanetFactory planet,
                                        ReadonlyArray<short> researchingLabPowerConsumerIndexes,
                                        ReadonlyArray<PowerConsumerType> powerConsumerTypes,
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
            ReadonlyArray<int> labNetworkIds = _labNetworkIds;
            LabState[] labStates = _labStates;
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
            if (flag2 && optimizedLabs.Length > 0 ||
                factorySystem.researchTechId != num /*Also update if tech changed*/)
            {
                Array.Clear(LabComponent.matrixPoints, 0, LabComponent.matrixPoints.Length);
                if (techProto != null && techProto.IsLabTech)
                {
                    for (int i = 0; i < techProto.Items.Length; i++)
                    {
                        int num46779 = techProto.Items[i] - LabComponent.matrixIds[0];
                        if (num46779 >= 0 && num46779 < LabComponent.matrixPoints.Length)
                        {
                            LabComponent.matrixPoints[num46779] = techProto.ItemPoints[i];
                        }
                    }
                }
            }

            GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ResearchingLab);
            ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;

            if (factorySystem.researchTechId != num)
            {
                factorySystem.researchTechId = num;

                ReadonlyArray<int> entityIds = _entityIds;
                for (int i = 0; i < optimizedLabs.Length; i++)
                {
                    optimizedLabs[i].SetFunction(entityIds[i],
                                                 factorySystem.researchTechId,
                                                 entitySignPool,
                                                 ref labsPowerFields[i],
                                                 groupNeeds,
                                                 componentsNeeds,
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
                int networkIndex = labNetworkIds[labIndex];
                ref LabState labState = ref labStates[labIndex];
                ref LabPowerFields labPowerFields = ref labsPowerFields[labIndex];
                if (labState != LabState.Active)
                {
                    UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
                    continue;
                }

                ref OptimizedResearchingLab reference = ref optimizedLabs[labIndex];
                OptimizedResearchingLab.UpdateNeedsResearch(groupNeeds, componentsNeeds, matrixServed, labIndex);
                if (flag2)
                {
                    int curLevel = ts.curLevel;
                    float power = networkServes[networkIndex];
                    labState = reference.InternalUpdateResearch(power,
                                                                research_speed,
                                                                factorySystem.researchTechId,
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

                UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
            }

            history.techStates[factorySystem.researchTechId] = ts;
            statistics.techHashedThisFrame = techHashedThisFrame;
            history.universeMatrixPointUploaded = uMatrixPoint;
            factoryProductionStat.hashRegister = hashRegister;
        }
    }

    public void GameTickLabOutputToNext(long time, SubFactoryNeeds subFactoryNeeds)
    {
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.ResearchingLab);
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        int[] matrixServed = _matrixServed;
        int[] matrixIncServed = _matrixIncServed;
        LabState[] labStates = _labStates;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        int num = (int)(time & 3);
        for (int i = 0; i < optimizedLabs.Length; i++)
        {
            if ((i & 3) == num)
            {
                optimizedLabs[i].UpdateOutputToNext(i,
                                                    optimizedLabs,
                                                    labStates,
                                                    groupNeeds,
                                                    componentsNeeds,
                                                    matrixServed,
                                                    matrixIncServed);
            }
        }
    }

    public void UpdatePower(ReadonlyArray<short> researchingLabPowerConsumerIndexes,
                            ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        ReadonlyArray<int> labNetworkIds = _labNetworkIds;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int labIndex = 0; labIndex < labNetworkIds.Length; labIndex++)
        {
            int networkIndex = labNetworkIds[labIndex];
            LabPowerFields labPowerFields = labsPowerFields[labIndex];
            UpdatePower(researchingLabPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, labIndex, networkIndex, labPowerFields);
        }
    }

    private static void UpdatePower(ReadonlyArray<short> researchingLabPowerConsumerIndexes,
                                    ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int labIndex,
                                    int networkIndex,
                                    LabPowerFields labPowerFields)
    {
        int powerConsumerTypeIndex = researchingLabPowerConsumerIndexes[labIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, labPowerFields);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> researchingLabPowerConsumerIndexes,
                                                                         ReadonlyArray<PowerConsumerType> powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        LabPowerFields[] labsPowerFields = _labsPowerFields;
        ReadonlyArray<int> prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
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

    private static void UpdatePowerConsumptionPerPrototype(ReadonlyArray<short> researchingLabPowerConsumerIndexes,
                                                           ReadonlyArray<PowerConsumerType> powerConsumerTypes,
                                                           ReadonlyArray<int> prototypeIdIndexes,
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
        ComponentNeeds[] componentsNeeds = subFactoryNeeds.ComponentsNeeds;
        short[] needsPatterns = subFactoryNeeds.NeedsPatterns;
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
                                               researchTechId,
                                               groupNeeds,
                                               componentsNeeds,
                                               needsPatterns,
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
                           SubFactoryNeedsBuilder subFactoryNeedsBuilder,
                           UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        List<int> labNetworkIds = [];
        List<LabState> labStates = [];
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
            labNetworkIds.Add(networkIndex);
            labStates.Add(LabState.Active);
            entityIds.Add(lab.entityId);
            matrixServed.Add(lab.matrixServed);
            matrixIncServed.Add(lab.matrixIncServed);
            subFactoryPowerSystemBuilder.AddResearchingLab(in lab, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[lab.entityId]);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
            needsBuilder.AddNeeds(lab.needs, LabComponent.matrixIds);
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

        _labNetworkIds = universeStaticDataBuilder.DeduplicateArrayUnmanaged(labNetworkIds);
        _labStates = labStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _entityIds = universeStaticDataBuilder.DeduplicateArrayUnmanaged(entityIds);
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build(universeStaticDataBuilder);
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