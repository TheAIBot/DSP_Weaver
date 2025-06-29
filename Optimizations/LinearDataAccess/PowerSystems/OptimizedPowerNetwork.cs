using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedWindGeneratorGroup
{
    private readonly long _genEnergyPerTick;
    public readonly int _componentCount;

    public OptimizedWindGeneratorGroup(long genEnergyPerTick, int componentCount)
    {
        _genEnergyPerTick = genEnergyPerTick;
        _componentCount = componentCount;
    }

    public long EnergyCap_Wind(float windStrength)
    {
        return (long)(windStrength * (float)_genEnergyPerTick) * _componentCount;
    }
}

internal sealed class WindGeneratorExecutor
{
    private OptimizedWindGeneratorGroup[] _optimizedWindGeneratorGroups = null!;
    private List<int> _optimizedWindIndexes = null!;
    private int _subId;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount { get; private set; }
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public IEnumerable<int> OptimizedPowerGeneratorIds => _optimizedWindIndexes;

    public long EnergyCap(float windStrength, long[] currentGeneratorCapacities)
    {
        if (_optimizedWindGeneratorGroups.Length == 0)
        {
            return 0;
        }

        long energySum = 0;
        OptimizedWindGeneratorGroup[] optimizedWindGeneratorGroups = _optimizedWindGeneratorGroups;
        for (int i = 0; i < optimizedWindGeneratorGroups.Length; i++)
        {
            energySum += optimizedWindGeneratorGroups[i].EnergyCap_Wind(windStrength);
        }

        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void Save(PlanetFactory planet)
    {
        // There is nothing to save for wind power
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        Dictionary<long, int> optimizedWindGeneratorGroupToCount = [];
        List<int> optimizedWindIndexes = [];
        int? subId = null;
        int? prototypeId = null;

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

            if (!powerGenerator.wind)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all wind machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all wind machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            optimizedWindIndexes.Add(powerGenerator.id);
            if (!optimizedWindGeneratorGroupToCount.TryGetValue(powerGenerator.genEnergyPerTick, out int groupCount))
            {
                optimizedWindGeneratorGroupToCount.Add(powerGenerator.genEnergyPerTick, 0);
            }
            optimizedWindGeneratorGroupToCount[powerGenerator.genEnergyPerTick]++;
        }

        _optimizedWindGeneratorGroups = optimizedWindGeneratorGroupToCount.Select(x => new OptimizedWindGeneratorGroup(x.Key, x.Value)).ToArray();
        _optimizedWindIndexes = optimizedWindIndexes;
        _subId = subId ?? -1;
        GeneratorCount = optimizedWindGeneratorGroupToCount.Values.Sum();
        PrototypeId = prototypeId;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedGeothermalGenerator
{
    private readonly long genEnergyPerTick;
    private readonly float warmupSpeed;
    private readonly float gthStrength;
    private readonly float gthAffectStrength;
    private float warmup;

    public OptimizedGeothermalGenerator(ref readonly PowerGeneratorComponent powerGenerator)
    {
        genEnergyPerTick = powerGenerator.genEnergyPerTick;
        warmupSpeed = powerGenerator.warmupSpeed;
        gthStrength = powerGenerator.gthStrength;
        gthAffectStrength = powerGenerator.gthAffectStrength;
        warmup = powerGenerator.warmup;
    }

    public long EnergyCap_GTH()
    {
        float currentStrength = gthStrength * gthAffectStrength * warmup;
        return (long)((float)genEnergyPerTick * currentStrength);
    }

    public void GeneratePower()
    {
        float num58 = warmup + warmupSpeed;
        warmup = num58 > 1f ? 1f : num58 < 0f ? 0f : num58;
    }

    public readonly void Save(ref PowerGeneratorComponent powerGenerator)
    {
        powerGenerator.warmup = warmup;
    }
}

internal sealed class GeothermalGeneratorExecutor
{
    private OptimizedGeothermalGenerator[] _optimizedGeothermalGenerators = null!;
    private Dictionary<int, int> _geothermalIdToOptimizedIndex = null!;
    private int _subId;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount => _optimizedGeothermalGenerators.Length;
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public Dictionary<int, int>.KeyCollection OptimizedPowerGeneratorIds => _geothermalIdToOptimizedIndex.Keys;

    public long EnergyCap(long[] currentGeneratorCapacities)
    {
        if (_optimizedGeothermalGenerators.Length == 0)
        {
            return 0;
        }

        long energySum = 0;
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 0; i < optimizedGeothermalGenerators.Length; i++)
        {
            energySum += optimizedGeothermalGenerators[i].EnergyCap_GTH();
        }

        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void GameTick()
    {
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 0; i < optimizedGeothermalGenerators.Length; i++)
        {
            optimizedGeothermalGenerators[i].GeneratePower();
        }
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        OptimizedGeothermalGenerator[] optimizedGeothermalGenerators = _optimizedGeothermalGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_geothermalIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedGeothermalGenerator optimizedGeothermal = ref optimizedGeothermalGenerators[optimizedIndex];
            optimizedGeothermal.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        List<OptimizedGeothermalGenerator> optimizedGeothermalGenerators = [];
        Dictionary<int, int> geothermalIdToOptimizedIndex = [];
        int? subId = null;
        int? prototypeId = null;

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

            if (!powerGenerator.geothermal)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all geothermal machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all geothermal machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            geothermalIdToOptimizedIndex.Add(powerGenerator.id, optimizedGeothermalGenerators.Count);
            optimizedGeothermalGenerators.Add(new OptimizedGeothermalGenerator(in powerGenerator));
        }

        _optimizedGeothermalGenerators = optimizedGeothermalGenerators.ToArray();
        _geothermalIdToOptimizedIndex = geothermalIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedSolarGenerator
{
    private readonly long genEnergyPerTick;
    private readonly UnityEngine.Vector3 position;
    private float currentStrength;
    private long capacityCurrentTick;

    public OptimizedSolarGenerator(ref readonly PowerGeneratorComponent powerGenerator)
    {
        genEnergyPerTick = powerGenerator.genEnergyPerTick;
        position = new UnityEngine.Vector3(powerGenerator.x, powerGenerator.y, powerGenerator.z);
        currentStrength = powerGenerator.currentStrength;
        capacityCurrentTick = powerGenerator.capacityCurrentTick;
    }

    public long EnergyCap_PV(UnityEngine.Vector3 normalized, float lumino)
    {
        float num = UnityEngine.Vector3.Dot(normalized, position) * 2.5f + 0.8572445f;
        num = ((num > 1f) ? 1f : ((num < 0f) ? 0f : num));
        currentStrength = num * lumino;
        capacityCurrentTick = (long)(currentStrength * (float)genEnergyPerTick);
        return capacityCurrentTick;
    }

    public readonly void Save(ref PowerGeneratorComponent powerGenerator)
    {
        powerGenerator.currentStrength = currentStrength;
        powerGenerator.capacityCurrentTick = capacityCurrentTick;
    }
}

internal sealed class SolarGeneratorExecutor
{
    private OptimizedSolarGenerator[] _optimizedSolarGenerators = null!;
    private Dictionary<int, int> _solarIdToOptimizedIndex = null!;
    private int _subId;
    private long? _cachedEnergySum;

    [MemberNotNullWhen(true, nameof(PrototypeId))]
    public bool IsUsed => GeneratorCount > 0;
    public int GeneratorCount => _optimizedSolarGenerators.Length;
    public int? PrototypeId { get; private set; }
    public long TotalCapacityCurrentTick { get; private set; }

    public Dictionary<int, int>.KeyCollection OptimizedPowerGeneratorIds => _solarIdToOptimizedIndex.Keys;

    public long EnergyCap(float luminosity,
                          UnityEngine.Vector3 normalized,
                          bool flag2,
                          long[] currentGeneratorCapacities)
    {
        if (_optimizedSolarGenerators.Length == 0)
        {
            return 0;
        }

        // Solar is apparently not updated on each tick so the previously calculated
        // result can be reused
        if (!flag2 && _cachedEnergySum.HasValue)
        {
            currentGeneratorCapacities[_subId] += _cachedEnergySum.Value;
            return _cachedEnergySum.Value;
        }

        long energySum = 0;
        OptimizedSolarGenerator[] optimizedSolarGenerators = _optimizedSolarGenerators;
        for (int i = 0; i < optimizedSolarGenerators.Length; i++)
        {
            energySum += optimizedSolarGenerators[i].EnergyCap_PV(normalized, luminosity);
        }

        _cachedEnergySum = energySum;
        currentGeneratorCapacities[_subId] += energySum;
        TotalCapacityCurrentTick = energySum;
        return energySum;
    }

    public void Save(PlanetFactory planet)
    {
        PowerGeneratorComponent[] powerGenerators = planet.powerSystem.genPool;
        OptimizedSolarGenerator[] optimizedSolarGenerators = _optimizedSolarGenerators;
        for (int i = 1; i < planet.powerSystem.genCursor; i++)
        {
            if (!_solarIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedSolarGenerator optimizedSolar = ref optimizedSolarGenerators[optimizedIndex];
            optimizedSolar.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        List<OptimizedSolarGenerator> optimizedSolarGenerators = [];
        Dictionary<int, int> solarIdToOptimizedIndex = [];
        int? subId = null;
        int? prototypeId = null;

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

            if (!powerGenerator.photovoltaic)
            {
                continue;
            }

            if (subId.HasValue && subId != powerGenerator.subId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(PowerGeneratorComponent.subId)} is the same for all solar machines is incorrect.");
            }
            subId = powerGenerator.subId;

            int componentPrototypeId = planet.entityPool[powerGenerator.entityId].protoId;
            if (prototypeId.HasValue && prototypeId != componentPrototypeId)
            {
                throw new InvalidOperationException($"Assumption that {nameof(EntityData.protoId)} is the same for all solar machines is incorrect.");
            }
            prototypeId = componentPrototypeId;

            solarIdToOptimizedIndex.Add(powerGenerator.id, optimizedSolarGenerators.Count);
            optimizedSolarGenerators.Add(new OptimizedSolarGenerator(in powerGenerator));
        }

        _optimizedSolarGenerators = optimizedSolarGenerators.ToArray();
        _solarIdToOptimizedIndex = solarIdToOptimizedIndex;
        _subId = subId ?? -1;
        PrototypeId = prototypeId;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedFuelGenerator
{
    public int id;
    private readonly long genEnergyPerTick;
    private readonly long useFuelPerTick;
    public readonly short fuelMask;
    private readonly int productId;
    private readonly bool boost;
    private long fuelEnergy;
    private short curFuelId;
    public short fuelId;
    public short fuelCount;
    public short fuelInc;
    private bool productive;
    private bool incUsed;
    private byte fuelIncLevel;
    private long fuelHeat;
    private float currentStrength;
    private long capacityCurrentTick;

    public OptimizedFuelGenerator(ref readonly PowerGeneratorComponent powerGenerator)
    {
        id = powerGenerator.id;
        genEnergyPerTick = powerGenerator.genEnergyPerTick;
        useFuelPerTick = powerGenerator.useFuelPerTick;
        fuelMask = powerGenerator.fuelMask;
        productId = powerGenerator.productId;
        boost = powerGenerator.boost;
        fuelEnergy = powerGenerator.fuelEnergy;
        curFuelId = powerGenerator.curFuelId;
        fuelId = powerGenerator.fuelId;
        fuelCount = powerGenerator.fuelCount;
        fuelInc = powerGenerator.fuelInc;
        productive = powerGenerator.productive;
        incUsed = powerGenerator.incUsed;
        fuelIncLevel = powerGenerator.fuelIncLevel;
        fuelHeat = powerGenerator.fuelHeat;
        currentStrength = powerGenerator.currentStrength;
        capacityCurrentTick = powerGenerator.capacityCurrentTick;
    }

    public long EnergyCap_Fuel()
    {
        long num = ((fuelCount > 0 || fuelEnergy >= useFuelPerTick) ? genEnergyPerTick : (fuelEnergy * genEnergyPerTick / useFuelPerTick));
        capacityCurrentTick = (productive ? ((long)((double)num * (1.0 + Cargo.incTableMilli[fuelIncLevel]) + 0.1)) : ((long)((double)num * (1.0 + Cargo.accTableMilli[fuelIncLevel]) + 0.1)));
        if (fuelMask == 4)
        {
            if (boost)
            {
                capacityCurrentTick *= 100L;
            }
            if (curFuelId == 1804)
            {
                capacityCurrentTick *= 2L;
            }
        }
        return capacityCurrentTick;
    }

    public void GeneratePower(ref long num44, double num51, int[] consumeRegister)
    {
        currentStrength = num44 > 0 && capacityCurrentTick > 0 ? 1 : 0;
        if (num44 > 0 && productId == 0)
        {
            long num57 = (long)(num51 * capacityCurrentTick + 0.99999);
            long num56 = num44 < num57 ? num44 : num57;
            if (num56 > 0)
            {
                num44 -= num56;
                GenEnergyByFuel(num56, consumeRegister);
            }
        }
    }

    public void GenEnergyByFuel(long energy, int[] consumeRegister)
    {
        long num = (productive ? (energy * useFuelPerTick * 40 / (genEnergyPerTick * Cargo.incFastDivisionNumerator[fuelIncLevel])) : (energy * useFuelPerTick / genEnergyPerTick));
        num = ((energy > 0 && num == 0L) ? 1 : num);
        if (fuelEnergy >= num)
        {
            fuelEnergy -= num;
            return;
        }
        curFuelId = 0;
        if (fuelCount > 0)
        {
            int num2 = fuelInc / fuelCount;
            num2 = ((num2 > 0) ? ((num2 > 10) ? 10 : num2) : 0);
            fuelInc -= (short)num2;
            productive = LDB.items.Select(fuelId).Productive;
            if (productive)
            {
                fuelIncLevel = (byte)num2;
                num = energy * useFuelPerTick * 40 / (genEnergyPerTick * Cargo.incFastDivisionNumerator[fuelIncLevel]);
            }
            else
            {
                fuelIncLevel = (byte)num2;
                num = energy * useFuelPerTick / genEnergyPerTick;
            }
            if (!incUsed)
            {
                incUsed = fuelIncLevel > 0;
            }
            long num3 = num - fuelEnergy;
            fuelEnergy = fuelHeat - num3;
            curFuelId = fuelId;
            fuelCount--;
            consumeRegister[fuelId]++;
            if (fuelCount == 0)
            {
                fuelId = 0;
                fuelInc = 0;
                fuelHeat = 0L;
            }
            if (fuelEnergy < 0)
            {
                fuelEnergy = 0L;
            }
        }
        else
        {
            fuelEnergy = 0L;
            productive = false;
        }
    }

    public void SetNewFuel(int _itemId, short _count, short _inc)
    {
        fuelId = (short)_itemId;
        fuelCount = _count;
        fuelInc = _inc;
        fuelHeat = LDB.items.Select(_itemId)?.HeatValue ?? 0;
        incUsed = false;
    }

    public void ClearFuel()
    {
        fuelId = 0;
        fuelCount = 0;
        fuelInc = 0;
        fuelHeat = 0L;
        incUsed = false;
    }

    public int PickFuelFrom(int filter, out int inc)
    {
        inc = 0;
        if (fuelId > 0 && fuelCount > 5 && (filter == 0 || filter == fuelId))
        {
            if (fuelInc > 0)
            {
                inc = fuelInc / fuelCount;
            }
            fuelInc -= (short)inc;
            fuelCount--;
            return fuelId;
        }
        return 0;
    }

    public readonly void Save(ref PowerGeneratorComponent powerGenerator)
    {
        powerGenerator.genEnergyPerTick = genEnergyPerTick;
        powerGenerator.useFuelPerTick = useFuelPerTick;
        powerGenerator.fuelMask = fuelMask;
        powerGenerator.productId = productId;
        powerGenerator.boost = boost;
        powerGenerator.fuelEnergy = fuelEnergy;
        powerGenerator.curFuelId = curFuelId;
        powerGenerator.fuelId = fuelId;
        powerGenerator.fuelCount = fuelCount;
        powerGenerator.fuelInc = fuelInc;
        powerGenerator.productive = productive;
        powerGenerator.incUsed = incUsed;
        powerGenerator.fuelIncLevel = fuelIncLevel;
        powerGenerator.fuelHeat = fuelHeat;
        powerGenerator.currentStrength = currentStrength;
        powerGenerator.capacityCurrentTick = capacityCurrentTick;
    }
}

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

            ref readonly OptimizedFuelGenerator optimizedGeothermal = ref generatorIDWithOptimizedFuelGenerators[optimizedIndex.GeneratorIDIndex].OptimizedFuelGenerators[optimizedIndex.OptimizedGeneratorIndex];
            optimizedGeothermal.Save(ref powerGenerators[i]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           int networkId)
    {
        Dictionary<GeneratorID, List<OptimizedFuelGenerator>> generatorIDToOptimizedFuelGenerators = [];
        Dictionary<int, GeneratorIdIndexWithOptimizedGeneratorIndex> fuelIdToOptimizedIndex = [];
        Dictionary<GeneratorID, int> generatorIDToOptimizedGeneratorIndex = [];

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
            optimizedFuelGenerators.Add(new OptimizedFuelGenerator(in powerGenerator));
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

internal record struct GeneratorID(int SubId, int PrototypeId);
internal record struct GeneratorIDWithGenerators<T>(GeneratorID GeneratorID, T[] OptimizedFuelGenerators);

internal sealed class OptimizedPowerNetwork
{
    private readonly PowerNetwork _powerNetwork;
    private readonly int _networkIndex;
    private readonly int[] _networkNonOptimizedPowerConsumerIndexes;
    private readonly WindGeneratorExecutor _windExecutor;
    private readonly SolarGeneratorExecutor _solarExecutor;
    private readonly GammaPowerGeneratorExecutor _gammaPowerGeneratorExecutor;
    private readonly GeothermalGeneratorExecutor _geothermalGeneratorExecutor;
    private readonly FuelGeneratorExecutor _fuelGeneratorExecutor;
    private readonly PowerExchangerExecutor _powerExchangerExecutor;
    private readonly long _totalPowerNodeEnergyConsumption;

    public OptimizedPowerNetwork(PowerNetwork powerNetwork,
                                 int networkIndex,
                                 int[] networkNonOptimizedPowerConsumerIndexes,
                                 WindGeneratorExecutor windExecutor,
                                 SolarGeneratorExecutor solarGeneratorExecutor,
                                 GammaPowerGeneratorExecutor gammaPowerGeneratorExecutor,
                                 GeothermalGeneratorExecutor geothermalGeneratorExecutor,
                                 FuelGeneratorExecutor fuelGeneratorExecutor,
                                 PowerExchangerExecutor powerExchangerExecutor,
                                 long totalPowerNodeEnergyConsumption)
    {
        _powerNetwork = powerNetwork;
        _networkIndex = networkIndex;
        _networkNonOptimizedPowerConsumerIndexes = networkNonOptimizedPowerConsumerIndexes;
        _windExecutor = windExecutor;
        _solarExecutor = solarGeneratorExecutor;
        _gammaPowerGeneratorExecutor = gammaPowerGeneratorExecutor;
        _geothermalGeneratorExecutor = geothermalGeneratorExecutor;
        _fuelGeneratorExecutor = fuelGeneratorExecutor;
        _powerExchangerExecutor = powerExchangerExecutor;
        _totalPowerNodeEnergyConsumption = totalPowerNodeEnergyConsumption;
    }

    public (long, bool) RequestDysonSpherePower(PowerSystem powerSystem, float eta, float increase, UnityEngine.Vector3 normalized)
    {
        return _gammaPowerGeneratorExecutor.EnergyCap_Gamma_Req(eta, increase, normalized);
    }

    public void GameTick(PlanetFactory planet,
                         long time,
                         int[] productRegister,
                         int[] consumeRegister,
                         ref long num,
                         ref long num2,
                         ref long num3,
                         ref long num4,
                         ref long num5,
                         float windStrength,
                         float luminosity,
                         UnityEngine.Vector3 normalized,
                         bool flag2,
                         Dictionary<OptimizedSubFactory, SubFactoryPowerConsumption>.ValueCollection subFactoryToPowerConsumption)
    {
        PowerSystem powerSystem = planet.powerSystem;
        PowerNetwork powerNetwork = _powerNetwork;
        int[] consumers = _networkNonOptimizedPowerConsumerIndexes;
        long totalEnergyDemand = 0L;
        for (int j = 0; j < consumers.Length; j++)
        {
            long requiredEnergy = powerSystem.consumerPool[consumers[j]].requiredEnergy;
            totalEnergyDemand += requiredEnergy;
            num2 += requiredEnergy;
        }
        totalEnergyDemand += _totalPowerNodeEnergyConsumption;
        num2 += _totalPowerNodeEnergyConsumption;
        foreach (SubFactoryPowerConsumption subFactoryNetworkPowerConsumptionPrepared in subFactoryToPowerConsumption)
        {
            totalEnergyDemand += subFactoryNetworkPowerConsumptionPrepared.NetworksPowerConsumption[_networkIndex];
            num2 += subFactoryNetworkPowerConsumptionPrepared.NetworksPowerConsumption[_networkIndex];
        }

        long num23 = 0L;
        long num24 = 0L;
        (long inputEnergySum, long outputEnergySum) = _powerExchangerExecutor.InputOutputUpdate(powerSystem.currentGeneratorCapacities);
        long num27 = inputEnergySum;
        long num26 = outputEnergySum;

        long totalEnergyProduction = outputEnergySum;
        long windEnergyCapacity = _windExecutor.EnergyCap(windStrength, powerSystem.currentGeneratorCapacities);
        totalEnergyProduction += windEnergyCapacity;
        long solarEnergyCapacity = _solarExecutor.EnergyCap(luminosity, normalized, flag2, powerSystem.currentGeneratorCapacities);
        totalEnergyProduction += solarEnergyCapacity;
        long gammaEnergyCapacity = _gammaPowerGeneratorExecutor.EnergyCap(planet, powerSystem.currentGeneratorCapacities);
        totalEnergyProduction += gammaEnergyCapacity;
        long geothermalEnergyCapacity = _geothermalGeneratorExecutor.EnergyCap(powerSystem.currentGeneratorCapacities);
        totalEnergyProduction += geothermalEnergyCapacity;
        long fuelEnergyCapacity = _fuelGeneratorExecutor.EnergyCap(powerSystem.currentGeneratorCapacities);
        totalEnergyProduction += fuelEnergyCapacity;

        num += totalEnergyProduction - num26;
        long totalEnergyOverProduction = totalEnergyProduction - totalEnergyDemand;
        long num33 = 0L;
        if (totalEnergyOverProduction > 0 && powerNetwork.exportDemandRatio > 0.0)
        {
            if (powerNetwork.exportDemandRatio > 1.0)
            {
                powerNetwork.exportDemandRatio = 1.0;
            }
            num33 = (long)(totalEnergyOverProduction * powerNetwork.exportDemandRatio + 0.5);
            totalEnergyOverProduction -= num33;
            totalEnergyDemand += num33;
        }
        powerNetwork.exportDemandRatio = 0.0;
        powerNetwork.energyStored = 0L;
        List<int> accumulators = powerNetwork.accumulators;
        int count4 = accumulators.Count;
        long num34 = 0L;
        long num35 = 0L;
        if (totalEnergyOverProduction >= 0)
        {
            for (int m = 0; m < count4; m++)
            {
                int num36 = accumulators[m];
                powerSystem.accPool[num36].curPower = 0L;
                long num37 = powerSystem.accPool[num36].InputCap();
                if (num37 > 0)
                {
                    num37 = num37 < totalEnergyOverProduction ? num37 : totalEnergyOverProduction;
                    powerSystem.accPool[num36].curEnergy += num37;
                    powerSystem.accPool[num36].curPower = num37;
                    totalEnergyOverProduction -= num37;
                    num34 += num37;
                    num4 += num37;
                }
                powerNetwork.energyStored += powerSystem.accPool[num36].curEnergy;
            }
        }
        else
        {
            long num38 = -totalEnergyOverProduction;
            for (int n = 0; n < count4; n++)
            {
                int num36 = accumulators[n];
                powerSystem.accPool[num36].curPower = 0L;
                long num39 = powerSystem.accPool[num36].OutputCap();
                if (num39 > 0)
                {
                    num39 = num39 < num38 ? num39 : num38;
                    powerSystem.accPool[num36].curEnergy -= num39;
                    powerSystem.accPool[num36].curPower = -num39;
                    num38 -= num39;
                    num35 += num39;
                    num3 += num39;
                }
                powerNetwork.energyStored += powerSystem.accPool[num36].curEnergy;
            }
        }
        double num40 = totalEnergyOverProduction < num27 ? totalEnergyOverProduction / (double)num27 : 1.0;
        _powerExchangerExecutor.UpdateInput(productRegister, consumeRegister, num40, ref totalEnergyOverProduction, ref num23, ref num4);

        long num44 = totalEnergyProduction < totalEnergyDemand + num23 ? totalEnergyProduction + num34 + num23 : totalEnergyDemand + num34 + num23;
        double num45 = num44 < num26 ? num44 / (double)num26 : 1.0;
        _powerExchangerExecutor.UpdateOutput(productRegister, consumeRegister, num45, ref num44, ref num24, ref num3);

        powerNetwork.energyCapacity = totalEnergyProduction - num26;
        powerNetwork.energyRequired = totalEnergyDemand - num33;
        powerNetwork.energyExport = num33;
        powerNetwork.energyServed = totalEnergyProduction + num35 < totalEnergyDemand ? totalEnergyProduction + num35 : totalEnergyDemand;
        powerNetwork.energyAccumulated = num34 - num35;
        powerNetwork.energyExchanged = num23 - num24;
        powerNetwork.energyExchangedInputTotal = num23;
        powerNetwork.energyExchangedOutputTotal = num24;
        if (num33 > 0)
        {
            PlanetATField planetATField = powerSystem.factory.planetATField;
            planetATField.energy += num33;
            planetATField.atFieldRechargeCurrent = num33 * 60;
        }
        totalEnergyProduction += num35;
        totalEnergyDemand += num34;
        num5 += totalEnergyProduction >= totalEnergyDemand ? num2 + num33 : totalEnergyProduction;
        long num49 = num24 - totalEnergyDemand > 0 ? num24 - totalEnergyDemand : 0;
        double num50 = totalEnergyProduction >= totalEnergyDemand ? 1.0 : totalEnergyProduction / (double)totalEnergyDemand;
        totalEnergyDemand += num23 - num49;
        totalEnergyProduction -= num24;
        double num51 = totalEnergyProduction > totalEnergyDemand ? totalEnergyDemand / (double)totalEnergyProduction : 1.0;
        powerNetwork.consumerRatio = num50;
        powerNetwork.generaterRatio = num51;
        powerNetwork.energyDischarge = num35 + num24;
        powerNetwork.energyCharge = num34 + num23;
        float num52 = totalEnergyProduction > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num50 : 0f;
        float num53 = totalEnergyProduction > 0 || powerNetwork.energyStored > 0 || num24 > 0 ? (float)num51 : 0f;
        powerSystem.networkServes[_networkIndex] = num52;
        powerSystem.networkGenerates[_networkIndex] = num53;

        _gammaPowerGeneratorExecutor.GameTick(planet, time, productRegister, consumeRegister);
        _geothermalGeneratorExecutor.GameTick();
        _fuelGeneratorExecutor.GameTick(ref num44, num51, consumeRegister);
    }

    public void RefreshPowerGenerationCapacites(ProductionStatistics statistics, PlanetFactory planet)
    {
        int[] powerGenId2Index = ItemProto.powerGenId2Index;

        if (_windExecutor.IsUsed)
        {
            int num = powerGenId2Index[_windExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _windExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _windExecutor.GeneratorCount;
            statistics.totalGenCapacity += _windExecutor.TotalCapacityCurrentTick;
        }

        if (_solarExecutor.IsUsed)
        {
            int num = powerGenId2Index[_solarExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _solarExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _solarExecutor.GeneratorCount;
            statistics.totalGenCapacity += _solarExecutor.TotalCapacityCurrentTick;
        }

        if (_gammaPowerGeneratorExecutor.IsUsed)
        {
            int num = powerGenId2Index[_gammaPowerGeneratorExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _gammaPowerGeneratorExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _gammaPowerGeneratorExecutor.GeneratorCount;
            statistics.totalGenCapacity += _gammaPowerGeneratorExecutor.TotalCapacityCurrentTick;
        }

        if (_geothermalGeneratorExecutor.IsUsed)
        {
            int num = powerGenId2Index[_geothermalGeneratorExecutor.PrototypeId.Value];
            statistics.genCapacities[num] += _geothermalGeneratorExecutor.TotalCapacityCurrentTick;
            statistics.genCount[num] += _geothermalGeneratorExecutor.GeneratorCount;
            statistics.totalGenCapacity += _geothermalGeneratorExecutor.TotalCapacityCurrentTick;
        }


        GeneratorIDWithGenerators<OptimizedFuelGenerator>[] fuelGenerators = _fuelGeneratorExecutor.Generators;
        long[] fuelGeneratorsTotalCapacityCurrentTick = _fuelGeneratorExecutor.TotalGeneratorCapacitiesCurrentTick;
        for (int i = 0; i < fuelGenerators.Length; i++)
        {
            GeneratorIDWithGenerators<OptimizedFuelGenerator> fuelGenerator = fuelGenerators[i];

            int num = powerGenId2Index[fuelGenerator.GeneratorID.PrototypeId];
            statistics.genCount[num] += fuelGenerator.OptimizedFuelGenerators.Length;

            long totalCapacityCurrentTick = fuelGeneratorsTotalCapacityCurrentTick[i];
            statistics.genCapacities[num] += totalCapacityCurrentTick;
            statistics.totalGenCapacity += totalCapacityCurrentTick;
        }

        if (_powerExchangerExecutor.IsUsed)
        {
            int num = powerGenId2Index[_powerExchangerExecutor.PrototypeId.Value];

            // Game code takes negative values of total capacity. I inverted the source
            // so it didn't need to be done here.
            statistics.genCapacities[num] += _powerExchangerExecutor.TotalGenerationCapacityCurrentTick;
            statistics.genCount[num] += _powerExchangerExecutor.GeneratorCount;
            statistics.totalGenCapacity += _powerExchangerExecutor.TotalGenerationCapacityCurrentTick;
        }
    }

    public void RefreshPowerConsumptionDemands(ProductionStatistics statistics, PlanetFactory planet)
    {
        EntityData[] entityPool = planet.entityPool;
        PowerSystem powerSystem = planet.powerSystem;
        int[] powerConId2Index = ItemProto.powerConId2Index;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        int[] leftoverConsumers = _networkNonOptimizedPowerConsumerIndexes;
        for (int i = 0; i < leftoverConsumers.Length; i++)
        {
            int consumerIndex = leftoverConsumers[i];
            int num = powerConId2Index[entityPool[consumerPool[consumerIndex].entityId].protoId];
            statistics.conDemands[num] += consumerPool[consumerIndex].requiredEnergy;
            statistics.conCount[num]++;
            statistics.totalConDemand += consumerPool[consumerIndex].requiredEnergy;
        }

        if (_powerExchangerExecutor.IsUsed)
        {
            int num = powerConId2Index[_powerExchangerExecutor.PrototypeId.Value];
            statistics.conDemands[num] += _powerExchangerExecutor.TotalConsumptionCapacityCurrentTick;
            statistics.conCount[num] += _powerExchangerExecutor.GeneratorCount;
            statistics.totalConDemand += _powerExchangerExecutor.TotalConsumptionCapacityCurrentTick;
        }
    }

    public void Save(PlanetFactory planet)
    {
        _windExecutor.Save(planet);
        _solarExecutor.Save(planet);
        _gammaPowerGeneratorExecutor.Save(planet);
        _geothermalGeneratorExecutor.Save(planet);
        _fuelGeneratorExecutor.Save(planet);
        _powerExchangerExecutor.Save(planet);
    }
}
