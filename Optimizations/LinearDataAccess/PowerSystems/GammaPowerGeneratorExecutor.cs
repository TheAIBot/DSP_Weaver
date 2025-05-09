using System;
using System.Collections.Generic;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

internal sealed class GammaPowerGeneratorExecutor
{
    private OptimizedGammaPowerGenerator[] _optimizedGammaPowerGenerators;
    private Dictionary<int, int> _gammaIdToOptimizedIndex;
    private int _subId;

    public Dictionary<int, int>.KeyCollection OptimizedPowerGeneratorIds => _gammaIdToOptimizedIndex.Keys;

    public (long, bool) EnergyCap_Gamma_Req(float eta, float increase, UnityEngine.Vector3 normalized)
    {
        long energySum = 0L;
        OptimizedGammaPowerGenerator[] optimizedGammaPowerGenerators = _optimizedGammaPowerGenerators;
        for (int i = 0; i < optimizedGammaPowerGenerators.Length; i++)
        {
            energySum += optimizedGammaPowerGenerators[i].EnergyCap_Gamma_Req(normalized, increase, eta);
        }

        return (energySum, optimizedGammaPowerGenerators.Length > 0);
    }

    public long EnergyCap(PlanetFactory planet, long[] currentGeneratorCapacities)
    {
        if (_optimizedGammaPowerGenerators.Length == 0)
        {
            return 0;
        }

        float response = planet.powerSystem.dysonSphere != null ? planet.powerSystem.dysonSphere.energyRespCoef : 0f;
        long energySum = 0;
        OptimizedGammaPowerGenerator[] optimizedGammaPowerGenerators = _optimizedGammaPowerGenerators;
        for (int i = 0; i < optimizedGammaPowerGenerators.Length; i++)
        {
            energySum += optimizedGammaPowerGenerators[i].EnergyCap_Gamma(response);
        }

        currentGeneratorCapacities[_subId] += energySum;
        return energySum;
    }

    public void GameTick(PlanetFactory planet,
                         long time,
                         int[] productRegister,
                         int[] consumeRegister)
    {
        bool useIonLayer = GameMain.history.useIonLayer;
        bool useCata = time % 10 == 0;
        int num10 = (int)(time % 90);
        OptimizedGammaPowerGenerator[] optimizedGammaPowerGenerators = _optimizedGammaPowerGenerators;
        for (int i = 0; i < optimizedGammaPowerGenerators.Length; i++)
        {
            bool keyFrame = (i + num10) % 90 == 0;
            optimizedGammaPowerGenerators[i].GameTick_Gamma(useIonLayer, useCata, keyFrame, productRegister, consumeRegister);
        }
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        OptimizedGammaPowerGenerator[] optimizedGammaPowerGenerators = _optimizedGammaPowerGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_gammaIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedGammaPowerGenerator optimizedGamma = ref optimizedGammaPowerGenerators[optimizedIndex];
            optimizedGamma.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId,
                           PlanetWideBeltExecutor beltExecutor)
    {
        List<OptimizedGammaPowerGenerator> optimizedGammaPowerGenerators = [];
        Dictionary<int, int> gammaIdToOptimizedIndex = [];
        int? subId = null;

        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            ref readonly PowerGeneratorComponent powerGenerator = ref planet.powerSystem.genPool[i];
            if (powerGenerator.id != i)
            {
                continue;
            }

            if (powerGenerator.networkId != networkId)
            {
                continue;
            }

            if (!powerGenerator.gamma)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all gamma machines is incorrect.");
            }
            subId = powerGenerator.subId;

            planet.ReadObjectConn(powerGenerator.entityId, 0, out var isOutput, out var otherObjId, out var _);
            OptimizedCargoPath? slot0Belt = null;
            int slot0BeltOffset = 0;
            if (otherObjId > 0 && planet.entityPool[otherObjId].beltId > 0)
            {
                int beltId = planet.entityPool[otherObjId].beltId;
                slot0BeltOffset = planet.cargoTraffic.beltPool[beltId].pivotOnPath;
                CargoPath cargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[beltId].segPathId];
                slot0Belt = beltExecutor.GetOptimizedCargoPath(cargoPath);
            }

            planet.ReadObjectConn(powerGenerator.entityId, 1, out var isOutput2, out var otherObjId2, out var _);
            OptimizedCargoPath? slot1Belt = null;
            int slot1BeltOffset = 0;
            if (otherObjId2 > 0 && planet.entityPool[otherObjId2].beltId > 0)
            {
                int beltId = planet.entityPool[otherObjId2].beltId;
                slot1BeltOffset = planet.cargoTraffic.beltPool[beltId].pivotOnPath;
                CargoPath cargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[beltId].segPathId];
                slot1Belt = beltExecutor.GetOptimizedCargoPath(cargoPath);
            }

            gammaIdToOptimizedIndex.Add(powerGenerator.id, optimizedGammaPowerGenerators.Count);
            optimizedGammaPowerGenerators.Add(new OptimizedGammaPowerGenerator(slot0Belt,
                                                                               slot0BeltOffset,
                                                                               isOutput,
                                                                               slot1Belt,
                                                                               slot1BeltOffset,
                                                                               isOutput2,
                                                                               in powerGenerator));
        }

        _optimizedGammaPowerGenerators = optimizedGammaPowerGenerators.ToArray();
        _gammaIdToOptimizedIndex = gammaIdToOptimizedIndex;
        _subId = subId ?? -1;
    }
}
