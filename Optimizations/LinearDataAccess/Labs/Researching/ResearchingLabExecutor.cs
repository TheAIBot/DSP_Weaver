using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Researching;

internal sealed class ResearchingLabExecutor
{
    private readonly StarClusterResearchManager _starClusterResearchManager;
    private int[] _matrixPoints;
    public NetworkIdAndState<LabState>[] _networkIdAndStates;
    public OptimizedResearchingLab[] _optimizedLabs;
    public LabPowerFields[] _labsPowerFields;
    public int[] _entityIds;
    public Dictionary<int, int> _labIdToOptimizedLabIndex;
    public HashSet<int> _unOptimizedLabIds;

    public int ResearchingLabCount => _optimizedLabs.Length;

    public ResearchingLabExecutor(StarClusterResearchManager starClusterResearchManager)
    {
        _starClusterResearchManager = starClusterResearchManager;
    }

    public void GameTickLabResearchMode(PlanetFactory planet)
    {
        lock (_starClusterResearchManager)
        {
            FactorySystem factorySystem = planet.factorySystem;
            GameHistoryData history = GameMain.history;
            GameStatData statistics = GameMain.statistics;
            FactoryProductionStat factoryProductionStat = statistics.production.factoryStatPool[planet.index];
            int[] consumeRegister = factoryProductionStat.consumeRegister;
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

            if (factorySystem.researchTechId != num)
            {
                factorySystem.researchTechId = num;

                int[] entityIds = _entityIds;
                for (int i = 0; i < optimizedLabs.Length; i++)
                {
                    optimizedLabs[i].SetFunction(entityIds[i],
                                                 factorySystem.researchTechId,
                                                 entitySignPool,
                                                 ref labsPowerFields[i]);
                }
            }

            for (int k = 0; k < optimizedLabs.Length; k++)
            {
                ref NetworkIdAndState<LabState> networkIdAndState = ref networkIdAndStates[k];
                if ((LabState)networkIdAndState.State != LabState.Active)
                {
                    continue;
                }

                ref OptimizedResearchingLab reference = ref optimizedLabs[k];
                reference.UpdateNeedsResearch();
                if (flag2)
                {
                    int curLevel = ts.curLevel;
                    float power = networkServes[networkIdAndState.Index];
                    networkIdAndState.State = (int)reference.InternalUpdateResearch(power,
                                                                                    research_speed,
                                                                                    factorySystem.researchTechId,
                                                                                    _matrixPoints,
                                                                                    consumeRegister,
                                                                                    ref ts,
                                                                                    ref techHashedThisFrame,
                                                                                    ref uMatrixPoint,
                                                                                    ref hashRegister,
                                                                                    ref labsPowerFields[k]);
                    if (ts.unlocked)
                    {
                        history.techStates[factorySystem.researchTechId] = ts;
                        _starClusterResearchManager.AddResearchedTech(factorySystem.researchTechId, curLevel, ts, techProto);
                        history.DequeueTech();
                        flag2 = false;
                    }
                    if (ts.curLevel > curLevel)
                    {
                        history.techStates[factorySystem.researchTechId] = ts;
                        _starClusterResearchManager.AddResearchedTech(factorySystem.researchTechId, curLevel, ts, techProto);
                        history.DequeueTech();
                        flag2 = false;
                    }
                }
            }

            history.techStates[factorySystem.researchTechId] = ts;
            statistics.techHashedThisFrame = techHashedThisFrame;
            history.universeMatrixPointUploaded = uMatrixPoint;
            factoryProductionStat.hashRegister = hashRegister;
        }
    }

    public void GameTickLabOutputToNext()
    {
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        for (int i = (int)(GameMain.gameTick % 5); i < optimizedLabs.Length; i += 5)
        {
            optimizedLabs[i].UpdateOutputToNext(i, optimizedLabs, networkIdAndStates);
        }
    }

    public void UpdatePower(int[] researchingLabPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        NetworkIdAndState<LabState>[] networkIdAndStates = _networkIdAndStates;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        for (int j = 0; j < networkIdAndStates.Length; j++)
        {
            int networkIndex = networkIdAndStates[j].Index;
            int powerConsumerTypeIndex = researchingLabPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, labsPowerFields[j]);
        }
    }

    public void Save(PlanetFactory planet)
    {
        LabComponent[] labComponents = planet.factorySystem.labPool;
        OptimizedResearchingLab[] optimizedLabs = _optimizedLabs;
        LabPowerFields[] labsPowerFields = _labsPowerFields;
        int researchTechId = planet.factorySystem.researchTechId;
        for (int i = 1; i < planet.factorySystem.labCursor; i++)
        {
            if (!_labIdToOptimizedLabIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedLabs[optimizedIndex].Save(ref labComponents[i], labsPowerFields[optimizedIndex], _matrixPoints, researchTechId);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder)
    {
        int[] matrixPoints = new int[LabComponent.matrixIds.Length];
        bool copiedMatrixPoints = false;
        List<NetworkIdAndState<LabState>> networkIdAndStates = [];
        List<OptimizedResearchingLab> optimizedLabs = [];
        List<LabPowerFields> labsPowerFields = [];
        List<int> entityIds = [];
        Dictionary<int, int> labIdToOptimizedLabIndex = [];
        HashSet<int> unOptimizedLabIds = [];

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
            optimizedPowerSystemBuilder.AddResearchingLab(in lab, networkIndex);

            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[lab.entityId] = lab.needs;
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

        _matrixPoints = matrixPoints.ToArray();
        _networkIdAndStates = networkIdAndStates.ToArray();
        _optimizedLabs = optimizedLabs.ToArray();
        _labsPowerFields = labsPowerFields.ToArray();
        _entityIds = entityIds.ToArray();
        _labIdToOptimizedLabIndex = labIdToOptimizedLabIndex;
        _unOptimizedLabIds = unOptimizedLabIds;
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType, LabPowerFields producingLabPowerFields)
    {
        return powerConsumerType.GetRequiredEnergy(producingLabPowerFields.replicating, 1000 + producingLabPowerFields.extraPowerRatio);
    }
}