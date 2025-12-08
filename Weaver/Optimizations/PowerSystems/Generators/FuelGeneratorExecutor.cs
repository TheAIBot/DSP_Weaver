using System.Collections.Generic;
using System.Linq;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.PowerSystems.Generators;

internal sealed class FuelGeneratorExecutor
{
    private GeneratorIDWithGenerators<OptimizedFuelGenerator>[] _generatorIDWithOptimizedFuelGenerators = null!;
    private Dictionary<int, GeneratorIdIndexWithOptimizedGeneratorIndex> _fuelIdToOptimizedIndex = null!;
    private long[] _totalGeneratorCapacitiesCurrentTick = null!;
    private Dictionary<int, OptimizedFuelGeneratorLocation> _fuelGeneratorIdToOptimizedFuelGeneratorLocation = null!;
    public GeneratorIDWithGenerators<OptimizedFuelGenerator>[] Generators => _generatorIDWithOptimizedFuelGenerators;
    public long[] TotalGeneratorCapacitiesCurrentTick => _totalGeneratorCapacitiesCurrentTick;
    public Dictionary<int, OptimizedFuelGeneratorLocation> FuelGeneratorIdToOptimizedFuelGeneratorLocation => _fuelGeneratorIdToOptimizedFuelGeneratorLocation;
    public IEnumerable<OptimizedFuelGenerator[]> GeneratorSegments => _generatorIDWithOptimizedFuelGenerators.Select(x => x.OptimizedFuelGenerators);

    public int GeneratorCount => _generatorIDWithOptimizedFuelGenerators.Sum(x => x.OptimizedFuelGenerators.Length);

    public long EnergyCap(long[] currentGeneratorCapacities)
    {
        if (_generatorIDWithOptimizedFuelGenerators.Length == 0)
        {
            return 0;
        }

        long energySum = 0;
        long[] totalGeneratorCapacitiesCurrentTick = _totalGeneratorCapacitiesCurrentTick;
        GeneratorIDWithGenerators<OptimizedFuelGenerator>[] generatorIDWithOptimizedFuelGenerators = _generatorIDWithOptimizedFuelGenerators;
        for (int generatorIdIndex = 0; generatorIdIndex < generatorIDWithOptimizedFuelGenerators.Length; generatorIdIndex++)
        {
            GeneratorIDWithGenerators<OptimizedFuelGenerator> generatorIDWithGenerators = generatorIDWithOptimizedFuelGenerators[generatorIdIndex];
            OptimizedFuelGenerator[] optimizedFuelGenerators = generatorIDWithGenerators.OptimizedFuelGenerators;
            long subIdEnergySum = 0;
            for (int i = 0; i < optimizedFuelGenerators.Length; i++)
            {
                subIdEnergySum += optimizedFuelGenerators[i].EnergyCap_Fuel();
            }

            currentGeneratorCapacities[generatorIDWithGenerators.GeneratorID.SubId] += subIdEnergySum;
            totalGeneratorCapacitiesCurrentTick[generatorIdIndex] = subIdEnergySum;
            energySum += subIdEnergySum;
        }

        return energySum;
    }

    public void GameTick(ref long num44, double num51, int[] consumeRegister)
    {
        GeneratorIDWithGenerators<OptimizedFuelGenerator>[] generatorIDWithOptimizedFuelGenerators = _generatorIDWithOptimizedFuelGenerators;
        for (int generatorIdIndex = 0; generatorIdIndex < generatorIDWithOptimizedFuelGenerators.Length; generatorIdIndex++)
        {
            OptimizedFuelGenerator[] optimizedFuelGenerators = generatorIDWithOptimizedFuelGenerators[generatorIdIndex].OptimizedFuelGenerators;
            for (int i = 0; i < optimizedFuelGenerators.Length; i++)
            {
                optimizedFuelGenerators[i].GeneratePower(ref num44, num51, consumeRegister);
            }
        }
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        GeneratorIDWithGenerators<OptimizedFuelGenerator>[] generatorIDWithOptimizedFuelGenerators = _generatorIDWithOptimizedFuelGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_fuelIdToOptimizedIndex.TryGetValue(i, out GeneratorIdIndexWithOptimizedGeneratorIndex optimizedIndex))
            {
                continue;
            }

            generatorIDWithOptimizedFuelGenerators[optimizedIndex.GeneratorIDIndex]
                .OptimizedFuelGenerators[optimizedIndex.OptimizedGeneratorIndex]
                .Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId,
                           SubFactoryProductionRegisterBuilder subProductionRegisterBuilder,
                           ref OptimizedItemId[]?[]? fuelNeeds)
    {
        Dictionary<GeneratorID, List<OptimizedFuelGenerator>> generatorIDToOptimizedFuelGenerators = [];
        Dictionary<int, GeneratorIdIndexWithOptimizedGeneratorIndex> fuelIdToOptimizedIndex = [];
        Dictionary<GeneratorID, int> generatorIDToOptimizedGeneratorIndex = [];
        if (fuelNeeds == null)
        {
            fuelNeeds = new OptimizedItemId[ItemProto.fuelNeeds.Length][];
        }

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

            bool isFuelGenerator = !powerGenerator.wind && !powerGenerator.photovoltaic && !powerGenerator.gamma && !powerGenerator.geothermal;
            if (!isFuelGenerator)
            {
                continue;
            }

            if (fuelNeeds[powerGenerator.fuelMask] == null &&
                ItemProto.fuelNeeds[powerGenerator.fuelMask] != null)
            {
                fuelNeeds[powerGenerator.fuelMask] = subProductionRegisterBuilder.AddConsume(ItemProto.fuelNeeds[powerGenerator.fuelMask]);
            }

            OptimizedItemId fuelId = default;
            if (powerGenerator.fuelId != 0)
            {
                fuelId = subProductionRegisterBuilder.AddConsume(powerGenerator.fuelId);
            }

            int subId = powerGenerator.subId;
            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            GeneratorID generatorId = new GeneratorID(subId, componentPrototypeId);
            if (!generatorIDToOptimizedFuelGenerators.TryGetValue(generatorId, out List<OptimizedFuelGenerator> optimizedFuelGenerators))
            {
                optimizedFuelGenerators = [];
                generatorIDToOptimizedFuelGenerators.Add(generatorId, optimizedFuelGenerators);
                generatorIDToOptimizedGeneratorIndex.Add(generatorId, generatorIDToOptimizedGeneratorIndex.Count);
            }

            fuelIdToOptimizedIndex.Add(powerGenerator.id, new GeneratorIdIndexWithOptimizedGeneratorIndex(generatorIDToOptimizedGeneratorIndex[generatorId], optimizedFuelGenerators.Count));
            optimizedFuelGenerators.Add(new OptimizedFuelGenerator(fuelId, in powerGenerator));
        }

        _generatorIDWithOptimizedFuelGenerators = generatorIDToOptimizedFuelGenerators.Select(x => new GeneratorIDWithGenerators<OptimizedFuelGenerator>(x.Key, x.Value.ToArray())).ToArray();
        _fuelIdToOptimizedIndex = fuelIdToOptimizedIndex;
        _totalGeneratorCapacitiesCurrentTick = new long[_generatorIDWithOptimizedFuelGenerators.Length];

        Dictionary<int, OptimizedFuelGeneratorLocation> fuelGeneratorIdToOptimizedFuelGeneratorLocation = [];
        for (int generatorSegmentIndex = 0; generatorSegmentIndex < _generatorIDWithOptimizedFuelGenerators.Length; generatorSegmentIndex++)
        {
            GeneratorIDWithGenerators<OptimizedFuelGenerator> generatorSegment = _generatorIDWithOptimizedFuelGenerators[generatorSegmentIndex];

            for (int generatorIndex = 0; generatorIndex < generatorSegment.OptimizedFuelGenerators.Length; generatorIndex++)
            {
                fuelGeneratorIdToOptimizedFuelGeneratorLocation.Add(generatorSegment.OptimizedFuelGenerators[generatorIndex].id,
                                                                    new OptimizedFuelGeneratorLocation(generatorSegmentIndex, generatorIndex));
            }
        }
        _fuelGeneratorIdToOptimizedFuelGeneratorLocation = fuelGeneratorIdToOptimizedFuelGeneratorLocation;
    }


    private record struct GeneratorIdIndexWithOptimizedGeneratorIndex(int GeneratorIDIndex, int OptimizedGeneratorIndex);
}
