using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations;

internal interface IMemorySize
{
    int GetSize();
}

internal sealed class DataDeduplicator<T> where T : struct, IEquatable<T>, IMemorySize
{
    private readonly Dictionary<T, int> _uniqueValueToIndex;
    private readonly List<T> _uniqueValues;
    private bool _dataUpdated;

    public int TotalBytes { get; private set; }
    public int BytesDeduplicated { get; private set; }

    public DataDeduplicator()
    {
        _uniqueValueToIndex = [];
        _uniqueValues = [];
        _dataUpdated = true;
    }

    public int GetDeduplicatedValueIndex(ref readonly T potentiallyDuplicatedValue)
    {
        int dataSize = potentiallyDuplicatedValue.GetSize();
        TotalBytes += dataSize;

        if (!_uniqueValueToIndex.TryGetValue(potentiallyDuplicatedValue, out int deduplicatedIndex))
        {
            _dataUpdated = true;
            deduplicatedIndex = _uniqueValues.Count;
            _uniqueValues.Add(potentiallyDuplicatedValue);
            _uniqueValueToIndex.Add(potentiallyDuplicatedValue, deduplicatedIndex);
        }
        else
        {
            BytesDeduplicated += dataSize;
        }


        return deduplicatedIndex;
    }

    public bool TryGetUpdatedData([NotNullWhen(true)] out T[]? updatedData)
    {
        if (!_dataUpdated)
        {
            updatedData = null;
            return false;
        }

        _dataUpdated = false;
        updatedData = _uniqueValues.ToArray();
        return true;
    }

    public void Clear()
    {
        _uniqueValueToIndex.Clear();
        _uniqueValues.Clear();
        _dataUpdated = true;
        TotalBytes = 0;
        BytesDeduplicated = 0;
    }
}

internal sealed class UniverseInserterStaticDataBuilder<TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>, IMemorySize
{
    private readonly DataDeduplicator<TInserterGrade> _inserterGrades = new DataDeduplicator<TInserterGrade>();

    public int TotalBytes => _inserterGrades.TotalBytes;
    public int BytesDeduplicated => _inserterGrades.BytesDeduplicated;

    public int AddInserterGrade(ref readonly TInserterGrade inserterGrade)
    {
        return _inserterGrades.GetDeduplicatedValueIndex(in inserterGrade);
    }

    public bool TryGetUpdatedData([NotNullWhen(true)]  out TInserterGrade[]? updatedData)
    {
        return _inserterGrades.TryGetUpdatedData(out updatedData);
    }

    public void Clear()
    {
        _inserterGrades.Clear();
    }
}

internal sealed class CompareArrayCollections<T> : IEqualityComparer<IList<T>>
    where T : IEquatable<T>
{
    public bool Equals(IList<T> x, IList<T> y)
    {
        if (x.Count != y.Count)
        {
            return false;
        }

        for (int i = 0; i < x.Count; i++)
        {
            if (!x[i].Equals(y[i]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(IList<T> value)
    {
        var hashCode = new HashCode();
        hashCode.Add(value.Count);
        for (int i = 0; i < value.Count; i++)
        {
            hashCode.Add(value[i]);
        }

        return hashCode.ToHashCode();
    }
}

internal interface IComparableArrayDeduplicator
{
    int TotalBytes { get; }
    int BytesDeduplicated { get; }
}

internal sealed class ComparableArrayDeduplicator<T> : IComparableArrayDeduplicator
    where T : IEquatable<T>
{
    private readonly HashSet<IList<T>> _arrays = new(new CompareArrayCollections<T>());

    public int TotalBytes { get; private set; }
    public int BytesDeduplicated { get; private set; }

    public T[] Deduplicate(IList<T> toDeduplicate, int itemSize)
    {
        int deduplicateSize = itemSize * toDeduplicate.Count;
        TotalBytes += deduplicateSize;

        if (_arrays.TryGetValue(toDeduplicate, out IList<T> deduplicated))
        {
            BytesDeduplicated += deduplicateSize;
            return (T[])deduplicated;
        }

        T[] array;
        if (toDeduplicate is List<T> toDeduplicateList)
        {
            array = toDeduplicateList.ToArray();
        }
        else
        {
            array = (T[])toDeduplicate;
        }

        _arrays.Add(array);
        return array;
    }
}

internal sealed class UniverseStaticDataBuilder
{
    private static bool _printStatistics = false;
    private readonly DataDeduplicator<AssemblerRecipe> _assemblerRecipes = new();
    private readonly DataDeduplicator<FractionatorConfiguration> _fractionatorConfigurations = new();
    private readonly DataDeduplicator<ProducingLabRecipe> _producingLabRecipes = new();
    private readonly DataDeduplicator<PowerConsumerType> _powerConsumerTypes = new();
    private readonly Dictionary<Type, IComparableArrayDeduplicator> _typeToComparableArrayDeduplicator = [];
    public UniverseInserterStaticDataBuilder<BiInserterGrade> BiInserterGrades { get; } = new();
    public UniverseInserterStaticDataBuilder<InserterGrade> InserterGrades { get; } = new();

    public UniverseStaticData UniverseStaticData { get; } = new UniverseStaticData();

    public static void EnableStatistics()
    {
        _printStatistics = true;
    }

    public int AddAssemblerRecipe(ref readonly AssemblerRecipe assemblerRecipe)
    {
        return _assemblerRecipes.GetDeduplicatedValueIndex(in assemblerRecipe);
    }

    public int AddFractionatorConfiguration(ref readonly FractionatorConfiguration fractionatorConfiguration)
    {
        return _fractionatorConfigurations.GetDeduplicatedValueIndex(in fractionatorConfiguration);
    }

    public int AddProducingLabRecipe(ref readonly ProducingLabRecipe producingLabRecipe)
    {
        return _producingLabRecipes.GetDeduplicatedValueIndex(in producingLabRecipe);
    }

    public int AddPowerConsumerType(ref readonly PowerConsumerType powerConsumerType)
    {
        return _powerConsumerTypes.GetDeduplicatedValueIndex(in powerConsumerType);
    }

    public T[] DeduplicateArrayUnmanaged<T>(IList<T> toDeduplicate)
        where T : unmanaged, IEquatable<T>
    {
        return DeduplicateArray(toDeduplicate, Marshal.SizeOf<T>());
    }

    public T[] DeduplicateArray<T>(IList<T> toDeduplicate)
        where T : IEquatable<T>, IMemorySize
    {
        if (toDeduplicate.Count == 0)
        {
            return [];
        }

        return DeduplicateArray(toDeduplicate, toDeduplicate[0].GetSize());
    }

    public void UpdateStaticDataIfRequired()
    {
        bool dataUpdated = false;
        if (_assemblerRecipes.TryGetUpdatedData(out AssemblerRecipe[]? assemblerRecipes))
        {
            //WeaverFixes.Logger.LogMessage($"Assembler recipe count: {assemblerRecipes.Length}");
            UniverseStaticData.UpdateAssemblerRecipes(assemblerRecipes);
            dataUpdated = true;
        }
        if (_fractionatorConfigurations.TryGetUpdatedData(out FractionatorConfiguration[]? fractionatorConfiguration))
        {
            //WeaverFixes.Logger.LogMessage($"Fractionator configuration count: {fractionatorConfiguration.Length}");
            UniverseStaticData.UpdateFractionatorConfigurations(fractionatorConfiguration);
            dataUpdated = true;
        }
        if (_producingLabRecipes.TryGetUpdatedData(out ProducingLabRecipe[]? producingLabRecipe))
        {
            //WeaverFixes.Logger.LogMessage($"Producing lab recipe count: {producingLabRecipe.Length}");
            UniverseStaticData.UpdateProducingLabRecipes(producingLabRecipe);
            dataUpdated = true;
        }
        if (_powerConsumerTypes.TryGetUpdatedData(out PowerConsumerType[]? powerConsumerTypes))
        {
            //WeaverFixes.Logger.LogMessage($"Power consumer type count: {powerConsumerTypes.Length}");
            UniverseStaticData.UpdatePowerConsumerTypes(powerConsumerTypes);
            dataUpdated = true;
        }
        if (BiInserterGrades.TryGetUpdatedData(out BiInserterGrade[]? biInserterGrades))
        {
            //WeaverFixes.Logger.LogMessage($"Bi-inserter grade count: {biInserterGrades.Length}");
            UniverseStaticData.UpdateBiInserterGrades(biInserterGrades);
            dataUpdated = true;
        }
        if (InserterGrades.TryGetUpdatedData(out InserterGrade[]? inserterGrades))
        {
            //WeaverFixes.Logger.LogMessage($"Inserter grade count: {inserterGrades.Length}");
            UniverseStaticData.UpdateInserterGrades(inserterGrades);
            dataUpdated = true;
        }

        if (dataUpdated && _printStatistics)
        {
            PrintStaticDataStatistics();
        }
    }

    public void Clear()
    {
        _assemblerRecipes.Clear();
        _fractionatorConfigurations.Clear();
        _producingLabRecipes.Clear();
        BiInserterGrades.Clear();
        InserterGrades.Clear();

        UpdateStaticDataIfRequired();
    }

    private T[] DeduplicateArray<T>(IList<T> toDeduplicate, int itemSize)
    where T : IEquatable<T>
    {
        if (!_typeToComparableArrayDeduplicator.TryGetValue(typeof(T), out IComparableArrayDeduplicator deduplicator))
        {
            deduplicator = new ComparableArrayDeduplicator<T>();
            _typeToComparableArrayDeduplicator.Add(typeof(T), deduplicator);
        }

        return ((ComparableArrayDeduplicator<T>)deduplicator).Deduplicate(toDeduplicate, itemSize);
    }

    private void PrintStaticDataStatistics()
    {
        int totalBytes = 0;
        totalBytes += _assemblerRecipes.TotalBytes;
        totalBytes += _fractionatorConfigurations.TotalBytes;
        totalBytes += _producingLabRecipes.TotalBytes;
        totalBytes += _powerConsumerTypes.TotalBytes;
        totalBytes += BiInserterGrades.TotalBytes;
        totalBytes += InserterGrades.TotalBytes;

        int deduplicatedBytes = 0;
        deduplicatedBytes += _assemblerRecipes.BytesDeduplicated;
        deduplicatedBytes += _fractionatorConfigurations.BytesDeduplicated;
        deduplicatedBytes += _producingLabRecipes.BytesDeduplicated;
        deduplicatedBytes += _powerConsumerTypes.BytesDeduplicated;
        deduplicatedBytes += BiInserterGrades.BytesDeduplicated;
        deduplicatedBytes += InserterGrades.BytesDeduplicated;

        foreach (IComparableArrayDeduplicator item in _typeToComparableArrayDeduplicator.Values)
        {
            totalBytes += item.TotalBytes;
            deduplicatedBytes += item.BytesDeduplicated;
        }

        WeaverFixes.Logger.LogMessage("Static data statistics:");
        WeaverFixes.Logger.LogMessage($"\tTotal:        {totalBytes,12:N0} bytes");
        WeaverFixes.Logger.LogMessage($"\tDeduplicated: {deduplicatedBytes,12:N0} bytes");
    }
}

internal sealed class UniverseStaticData
{
    public AssemblerRecipe[] AssemblerRecipes { get; private set; } = [];
    public FractionatorConfiguration[] FractionatorConfigurations { get; private set; } = [];
    public ProducingLabRecipe[] ProducingLabRecipes { get; private set; } = [];
    public PowerConsumerType[] PowerConsumerTypes { get; private set; } = [];
    public BiInserterGrade[] BiInserterGrades { get; private set; } = [];
    public InserterGrade[] InserterGrades { get; private set; } = [];

    public void UpdateAssemblerRecipes(AssemblerRecipe[] assemblerRecipes)
    {
        AssemblerRecipes = assemblerRecipes;
    }

    public void UpdateFractionatorConfigurations(FractionatorConfiguration[] fractionatorConfigurations)
    {
        FractionatorConfigurations = fractionatorConfigurations;
    }

    public void UpdateProducingLabRecipes(ProducingLabRecipe[] producingLabRecipes)
    {
        ProducingLabRecipes = producingLabRecipes;
    }

    public void UpdatePowerConsumerTypes(PowerConsumerType[] powerConsumerTypes)
    {
        PowerConsumerTypes = powerConsumerTypes;
    }

    public void UpdateBiInserterGrades(BiInserterGrade[] biInserterGrades)
    {
        BiInserterGrades = biInserterGrades;
    }

    public void UpdateInserterGrades(InserterGrade[] inserterGrades)
    {
        InserterGrades = inserterGrades;
    }
}