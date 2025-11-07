using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;

namespace Weaver.Optimizations;

internal sealed class DataDeduplicator<T> where T : struct, IEquatable<T>
{
    private readonly Dictionary<T, int> _uniqueValueToIndex;
    private readonly List<T> _uniqueValues;
    private bool _dataUpdated;

    public DataDeduplicator()
    {
        _uniqueValueToIndex = [];
        _uniqueValues = [];
        _dataUpdated = true;
    }

    public int GetDeduplicatedValueIndex(ref readonly T potentiallyDuplicatedValue)
    {
        if (!_uniqueValueToIndex.TryGetValue(potentiallyDuplicatedValue, out int deduplicatedIndex))
        {
            _dataUpdated = true;
            deduplicatedIndex = _uniqueValues.Count;
            _uniqueValues.Add(potentiallyDuplicatedValue);
            _uniqueValueToIndex.Add(potentiallyDuplicatedValue, deduplicatedIndex);
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
    }
}

internal sealed class UniverseInserterStaticDataBuilder<TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>
{
    private readonly DataDeduplicator<TInserterGrade> _inserterGrades = new DataDeduplicator<TInserterGrade>();

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

internal sealed class UniverseStaticDataBuilder
{
    
    private readonly DataDeduplicator<AssemblerRecipe> _assemblerRecipes = new();
    private readonly DataDeduplicator<FractionatorConfiguration> _fractionatorConfigurations = new();
    private readonly DataDeduplicator<FractionatorRecipeProduct> _fractionatorRecipeProducts = new();
    private readonly DataDeduplicator<ProducingLabRecipe> _producingLabRecipes = new();
    public UniverseInserterStaticDataBuilder<BiInserterGrade> BiInserterGrades { get; } = new();
    public UniverseInserterStaticDataBuilder<InserterGrade> InserterGrades { get; } = new();

    public UniverseStaticData UniverseStaticData { get; } = new UniverseStaticData();

    public int AddAssemblerRecipe(ref readonly AssemblerRecipe assemblerRecipe)
    {
        return _assemblerRecipes.GetDeduplicatedValueIndex(in assemblerRecipe);
    }

    public int AddFractionatorConfiguration(ref readonly FractionatorConfiguration fractionatorConfiguration)
    {
        return _fractionatorConfigurations.GetDeduplicatedValueIndex(in fractionatorConfiguration);
    }

    public int AddFractionatorRecipeProduct(ref readonly FractionatorRecipeProduct fractionatorRecipeProduct)
    {
        return _fractionatorRecipeProducts.GetDeduplicatedValueIndex(in fractionatorRecipeProduct);
    }

    public int AddProducingLabRecipe(ref readonly ProducingLabRecipe producingLabRecipe)
    {
        return _producingLabRecipes.GetDeduplicatedValueIndex(in producingLabRecipe);
    }

    public void UpdateStaticDataIfRequired()
    {
        if (_assemblerRecipes.TryGetUpdatedData(out AssemblerRecipe[]? assemblerRecipes))
        {
            //WeaverFixes.Logger.LogMessage($"Assembler recipe count: {assemblerRecipes.Length}");
            UniverseStaticData.UpdateAssemblerRecipes(assemblerRecipes);
        }
        if (_fractionatorConfigurations.TryGetUpdatedData(out FractionatorConfiguration[]? fractionatorConfiguration))
        {
            //WeaverFixes.Logger.LogMessage($"Fractionator configuration count: {fractionatorConfiguration.Length}");
            UniverseStaticData.UpdateFractionatorConfigurations(fractionatorConfiguration);
        }
        if (_fractionatorRecipeProducts.TryGetUpdatedData(out FractionatorRecipeProduct[]? fractionatorRecipeProduct))
        {
            //WeaverFixes.Logger.LogMessage($"Fractionator recipe count: {fractionatorRecipeProduct.Length}");
            UniverseStaticData.UpdateFractionatorRecipeProducts(fractionatorRecipeProduct);
        }
        if (_producingLabRecipes.TryGetUpdatedData(out ProducingLabRecipe[]? producingLabRecipe))
        {
            //WeaverFixes.Logger.LogMessage($"Producing lab recipe count: {producingLabRecipe.Length}");
            UniverseStaticData.UpdateProducingLabRecipe(producingLabRecipe);
        }
        if (BiInserterGrades.TryGetUpdatedData(out BiInserterGrade[]? biInserterGrades))
        {
            //WeaverFixes.Logger.LogMessage($"Bi-inserter grade count: {biInserterGrades.Length}");
            UniverseStaticData.UpdateBiInserterGrades(biInserterGrades);
        }
        if (InserterGrades.TryGetUpdatedData(out InserterGrade[]? inserterGrades))
        {
            //WeaverFixes.Logger.LogMessage($"Inserter grade count: {inserterGrades.Length}");
            UniverseStaticData.UpdateInserterGrades(inserterGrades);
        }
    }

    public void Clear()
    {
        _assemblerRecipes.Clear();
        _fractionatorConfigurations.Clear();
        _fractionatorRecipeProducts.Clear();
        _producingLabRecipes.Clear();
        BiInserterGrades.Clear();
        InserterGrades.Clear();

        UpdateStaticDataIfRequired();
    }
}

internal sealed class UniverseStaticData
{
    public AssemblerRecipe[] AssemblerRecipes { get; private set; } = [];
    public FractionatorConfiguration[] FractionatorConfigurations { get; private set; } = [];
    public FractionatorRecipeProduct[] FractionatorRecipeProducts { get; private set; } = [];
    public ProducingLabRecipe[] ProducingLabRecipes { get; private set; } = [];
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

    public void UpdateFractionatorRecipeProducts(FractionatorRecipeProduct[] fractionatorRecipeProducts)
    {
        FractionatorRecipeProducts = fractionatorRecipeProducts;
    }

    public void UpdateProducingLabRecipe(ProducingLabRecipe[] producingLabRecipe)
    {
        ProducingLabRecipes = producingLabRecipe;
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