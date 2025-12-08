using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.PowerSystems.Generators;

internal sealed class GammaPowerGeneratorExecutor
{
    private OptimizedGammaPowerGenerator[] _optimizedGammaPowerGenerators = null!;
    private Dictionary<int, int> _gammaIdToOptimizedIndex = null!;
    private int _subId;
    private int _gammaMachinesGeneratingEnergyCount;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount => _gammaMachinesGeneratingEnergyCount;
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

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

        float response = planet.dysonSphere?.energyRespCoef ?? 0f;
        long energySum = 0;
        OptimizedGammaPowerGenerator[] optimizedGammaPowerGenerators = _optimizedGammaPowerGenerators;
        for (int i = 0; i < optimizedGammaPowerGenerators.Length; i++)
        {
            energySum += optimizedGammaPowerGenerators[i].EnergyCap_Gamma(response);
        }

        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void GameTick(long time,
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

            optimizedGammaPowerGenerators[optimizedIndex].Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId,
                           SubFactoryProductionRegisterBuilder subProductionRegisterBuilder,
                           PlanetWideBeltExecutor beltExecutor)
    {
        List<OptimizedGammaPowerGenerator> optimizedGammaPowerGenerators = [];
        Dictionary<int, int> gammaIdToOptimizedIndex = [];
        int? subId = null;
        int? prototypeId = null;
        int gammaMachinesGeneratingEnergyCount = 0;

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

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all gamma machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            planet.ReadObjectConn(powerGenerator.entityId, 0, out var isOutput, out var otherObjId, out var _);
            OptimizedIndexedCargoPath slot0Belt = OptimizedIndexedCargoPath.NoBelt;
            int slot0BeltOffset = 0;
            if (otherObjId > 0 && planet.entityPool[otherObjId].beltId > 0)
            {
                int beltId = planet.entityPool[otherObjId].beltId;
                slot0BeltOffset = planet.cargoTraffic.beltPool[beltId].pivotOnPath;
                CargoPath cargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[beltId].segPathId];
                slot0Belt = beltExecutor.GetOptimizedCargoPath(cargoPath);
            }

            planet.ReadObjectConn(powerGenerator.entityId, 1, out var isOutput2, out var otherObjId2, out var _);
            OptimizedIndexedCargoPath slot1Belt = OptimizedIndexedCargoPath.NoBelt;
            int slot1BeltOffset = 0;
            if (otherObjId2 > 0 && planet.entityPool[otherObjId2].beltId > 0)
            {
                int beltId = planet.entityPool[otherObjId2].beltId;
                slot1BeltOffset = planet.cargoTraffic.beltPool[beltId].pivotOnPath;
                CargoPath cargoPath = planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[beltId].segPathId];
                slot1Belt = beltExecutor.GetOptimizedCargoPath(cargoPath);
            }

            OptimizedItemId catalystId = default;
            if (powerGenerator.catalystId != 0)
            {
                catalystId = subProductionRegisterBuilder.AddConsume(powerGenerator.catalystId);
            }
            OptimizedItemId productId = default;
            if (powerGenerator.productId != 0)
            {
                productId = subProductionRegisterBuilder.AddProduct(powerGenerator.productId);
            }

            gammaIdToOptimizedIndex.Add(powerGenerator.id, optimizedGammaPowerGenerators.Count);
            optimizedGammaPowerGenerators.Add(new OptimizedGammaPowerGenerator(slot0Belt,
                                                                               slot0BeltOffset,
                                                                               isOutput,
                                                                               slot1Belt,
                                                                               slot1BeltOffset,
                                                                               isOutput2,
                                                                               catalystId,
                                                                               productId,
                                                                               in powerGenerator));

            if (powerGenerator.productId <= 0)
            {
                gammaMachinesGeneratingEnergyCount++;
            }
        }

        _optimizedGammaPowerGenerators = optimizedGammaPowerGenerators.ToArray();
        _gammaIdToOptimizedIndex = gammaIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
        _gammaMachinesGeneratingEnergyCount = gammaMachinesGeneratingEnergyCount;
    }
}
