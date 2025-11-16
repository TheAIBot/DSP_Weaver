using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.StaticData;

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

        foreach (IComparableArrayDeduplicator item in _typeToComparableArrayDeduplicator.Values)
        {
            item.Clear();
        }

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