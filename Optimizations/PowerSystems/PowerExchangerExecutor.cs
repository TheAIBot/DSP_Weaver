using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.PowerSystems.Generators;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.PowerSystems;

internal sealed class PowerExchangerExecutor
{
    private OptimizedPowerExchanger[] _optimizedPowerExchangers = null!;
    private Dictionary<int, int> _powerExchangerIdToOptimizedIndex = null!;
    private int _subId;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => _optimizedPowerExchangers.Length > 0;
    public int GeneratorCount { get; private set; }
    public int ConsumerCount { get; private set; }
    public int? PrototypeId { get; private set; }
    public long TotalGenerationCapacityCurrentTick { get; private set; }
    public long TotalConsumptionCapacityCurrentTick { get; private set; }

    public (long inputEnergySum, long outputEnergySum) InputOutputUpdate(long[] currentGeneratorCapacities, int workerIndex)
    {
        if (_optimizedPowerExchangers.Length == 0)
        {
            return (0, 0);
        }

        DeepProfiler.BeginSample(DPEntry.PowerExchanger, workerIndex, _optimizedPowerExchangers.Length);
        long inputEnergySum = 0;
        long outputEnergySum = 0;
        OptimizedPowerExchanger[] optimizedPowerExchangers = _optimizedPowerExchangers;
        for (int i = 0; i < optimizedPowerExchangers.Length; i++)
        {
            ref OptimizedPowerExchanger powerExchanger = ref optimizedPowerExchangers[i];
            powerExchanger.StateUpdate();
            powerExchanger.BeltUpdate();
            bool flag3 = powerExchanger.state >= 1f;
            bool flag4 = powerExchanger.state <= -1f;
            if (!flag3 && !flag4)
            {
                powerExchanger.capsCurrentTick = 0L;
                powerExchanger.currEnergyPerTick = 0L;
            }
            if (flag4)
            {
                outputEnergySum += powerExchanger.OutputCaps();

            }
            else if (flag3)
            {
                inputEnergySum += powerExchanger.InputCaps();
            }
        }

        currentGeneratorCapacities[_subId] += outputEnergySum;

        DeepProfiler.EndSample(DPEntry.PowerExchanger, workerIndex);
        return (inputEnergySum, outputEnergySum);
    }

    public void UpdateInput(int[] productRegister, int[] consumeRegister, double num40, ref long num32, ref long num23, ref long num4, int workerIndex)
    {
        ConsumerCount = 0;

        long energySum = 0;
        OptimizedPowerExchanger[] optimizedPowerExchangers = _optimizedPowerExchangers;
        if (optimizedPowerExchangers.Length > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerExchanger, workerIndex, optimizedPowerExchangers.Length);
            for (int i = 0; i < optimizedPowerExchangers.Length; i++)
            {
                ref OptimizedPowerExchanger powerExchanger = ref optimizedPowerExchangers[i];
                if (powerExchanger.state >= 1f && num40 >= 0.0)
                {
                    long num42 = (long)(num40 * powerExchanger.capsCurrentTick + 0.99999);
                    long remaining = num32 < num42 ? num32 : num42;
                    energySum += powerExchanger.InputUpdate(remaining, productRegister, consumeRegister);
                    ConsumerCount++;
                }
                else
                {
                    powerExchanger.currEnergyPerTick = 0L;
                }
            }
            DeepProfiler.EndSample(DPEntry.PowerExchanger, workerIndex);

            num32 -= energySum;
            num23 += energySum;
            num4 += energySum;
        }

        TotalConsumptionCapacityCurrentTick = energySum;
    }

    public void UpdateOutput(int[] productRegister, int[] consumeRegister, double num45, ref long num44, ref long num24, ref long num3, int workerIndex)
    {
        GeneratorCount = 0;
        long energySum = 0;
        OptimizedPowerExchanger[] optimizedPowerExchangers = _optimizedPowerExchangers;
        if (optimizedPowerExchangers.Length > 0)
        {
            DeepProfiler.BeginSample(DPEntry.PowerExchanger, workerIndex, optimizedPowerExchangers.Length);
            for (int num46 = 0; num46 < optimizedPowerExchangers.Length; num46++)
            {
                ref OptimizedPowerExchanger powerExchanger = ref optimizedPowerExchangers[num46];
                if (powerExchanger.state <= -1f)
                {
                    long num47 = (long)(num45 * powerExchanger.capsCurrentTick + 0.99999);
                    long energyPay = num44 < num47 ? num44 : num47;
                    energySum += powerExchanger.OutputUpdate(energyPay, productRegister, consumeRegister);
                    GeneratorCount++;
                }
            }
            DeepProfiler.EndSample(DPEntry.PowerExchanger, workerIndex);

            num24 += energySum;
            num3 += energySum;
            num44 -= energySum;
        }

        TotalGenerationCapacityCurrentTick = energySum;
    }

    public void Save(PlanetFactory planet)
    {
        PowerExchangerComponent[] powerExchangers = planet.powerSystem.excPool;
        OptimizedPowerExchanger[] optimizedPowerExchangers = _optimizedPowerExchangers;
        for (int i = 1; i < planet.powerSystem.excCursor; i++)
        {
            if (!_powerExchangerIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedPowerExchangers[optimizedIndex].Save(ref powerExchangers[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId,
                           SubFactoryProductionRegisterBuilder subProductionRegisterBuilder,
                           PlanetWideBeltExecutor beltExecutor)
    {
        List<OptimizedPowerExchanger> optimizedPowerExchangers = [];
        Dictionary<int, int> powerExchangerIdToOptimizedIndex = [];
        int? subId = null;
        int? prototypeId = null;

        for (int i = 1; i < planet.powerSystem.excCursor; i++)
        {
            ref readonly PowerExchangerComponent powerExchanger = ref planet.powerSystem.excPool[i];
            if (powerExchanger.id != i)
            {
                continue;
            }

            if (powerExchanger.networkId != networkId)
            {
                continue;
            }

            if (subId.HasValue && subId != powerExchanger.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerExchangerComponent.subId)} is the same for all power exchangers is incorrect.");
            }
            subId = powerExchanger.subId;

            int componentPrototypeId = planet.entityPool[powerExchanger.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all power exchanger machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            OptimizedCargoPath? belt0 = null;
            OptimizedCargoPath? belt1 = null;
            OptimizedCargoPath? belt2 = null;
            OptimizedCargoPath? belt3 = null;

            OptimizedItemId emptyId = default;
            OptimizedItemId fullId = default;
            if (powerExchanger.targetState == 1f)
            {
                emptyId = subProductionRegisterBuilder.AddConsume(powerExchanger.emptyId);
                fullId = subProductionRegisterBuilder.AddProduct(powerExchanger.fullId);
            }
            else if (powerExchanger.targetState == -1f)
            {
                emptyId = subProductionRegisterBuilder.AddProduct(powerExchanger.emptyId);
                fullId = subProductionRegisterBuilder.AddConsume(powerExchanger.fullId);
            }

            if (powerExchanger.belt0 > 0)
            {
                belt0 = beltExecutor.GetOptimizedCargoPath(planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[powerExchanger.belt0].segPathId]);
            }
            if (powerExchanger.belt1 > 0)
            {
                belt1 = beltExecutor.GetOptimizedCargoPath(planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[powerExchanger.belt1].segPathId]);
            }
            if (powerExchanger.belt2 > 0)
            {
                belt2 = beltExecutor.GetOptimizedCargoPath(planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[powerExchanger.belt2].segPathId]);
            }
            if (powerExchanger.belt3 > 0)
            {
                belt3 = beltExecutor.GetOptimizedCargoPath(planet.cargoTraffic.pathPool[planet.cargoTraffic.beltPool[powerExchanger.belt3].segPathId]);
            }

            powerExchangerIdToOptimizedIndex.Add(powerExchanger.id, optimizedPowerExchangers.Count);
            optimizedPowerExchangers.Add(new OptimizedPowerExchanger(emptyId,
                                                                     fullId,
                                                                     belt0,
                                                                     belt1,
                                                                     belt2,
                                                                     belt3,
                                                                     in powerExchanger));
        }

        _optimizedPowerExchangers = optimizedPowerExchangers.ToArray();
        _powerExchangerIdToOptimizedIndex = powerExchangerIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
    }
}
