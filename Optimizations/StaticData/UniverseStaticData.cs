using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.StaticData;

internal sealed class UniverseStaticData
{
    public ReadonlyArray<AssemblerRecipe> AssemblerRecipes { get; private set; } = new ReadonlyArray<AssemblerRecipe>([]);
    public ReadonlyArray<ReadonlyArray<OptimizedItemId>> AssemblerRecipesRequires { get; private set; } = new ReadonlyArray<ReadonlyArray<OptimizedItemId>>([]);
    public ReadonlyArray<ReadonlyArray<OptimizedItemId>> AssemblerRecipesProducts { get; private set; } = new ReadonlyArray<ReadonlyArray<OptimizedItemId>>([]);
    public ReadonlyArray<FractionatorConfiguration> FractionatorConfigurations { get; private set; } = new ReadonlyArray<FractionatorConfiguration>([]);
    public ReadonlyArray<ProducingLabRecipe> ProducingLabRecipes { get; private set; } = new ReadonlyArray<ProducingLabRecipe>([]);
    public ReadonlyArray<PowerConsumerType> PowerConsumerTypes { get; private set; } = new ReadonlyArray<PowerConsumerType>([]);
    public ReadonlyArray<BiInserterGrade> BiInserterGrades { get; private set; } = new ReadonlyArray<BiInserterGrade>([]);
    public ReadonlyArray<InserterGrade> InserterGrades { get; private set; } = new ReadonlyArray<InserterGrade>([]);

    public void UpdateAssemblerRecipes(ReadonlyArray<AssemblerRecipe> assemblerRecipes)
    {
        AssemblerRecipes = assemblerRecipes;
    }

    public void UpdateAssemblerRecipesRequires(ReadonlyArray<ReadonlyArray<OptimizedItemId>> assemblerRecipesRequires)
    {
        AssemblerRecipesRequires = assemblerRecipesRequires;
    }

    public void UpdateAssemblerRecipesProducts(ReadonlyArray<ReadonlyArray<OptimizedItemId>> assemblerRecipesProducts)
    {
        AssemblerRecipesProducts = assemblerRecipesProducts;
    }

    public void UpdateFractionatorConfigurations(ReadonlyArray<FractionatorConfiguration> fractionatorConfigurations)
    {
        FractionatorConfigurations = fractionatorConfigurations;
    }

    public void UpdateProducingLabRecipes(ReadonlyArray<ProducingLabRecipe> producingLabRecipes)
    {
        ProducingLabRecipes = producingLabRecipes;
    }

    public void UpdatePowerConsumerTypes(ReadonlyArray<PowerConsumerType> powerConsumerTypes)
    {
        PowerConsumerTypes = powerConsumerTypes;
    }

    public void UpdateBiInserterGrades(ReadonlyArray<BiInserterGrade> biInserterGrades)
    {
        BiInserterGrades = biInserterGrades;
    }

    public void UpdateInserterGrades(ReadonlyArray<InserterGrade> inserterGrades)
    {
        InserterGrades = inserterGrades;
    }

    public void PrintUsage()
    {
        WeaverFixes.Logger.LogMessage("Universe static data statistics");
        WeaverFixes.Logger.LogMessage($"\t{nameof(AssemblerRecipes),20}: {AssemblerRecipes.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(AssemblerRecipesRequires),20}: {AssemblerRecipesRequires.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(AssemblerRecipesProducts),20}: {AssemblerRecipesProducts.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(FractionatorConfigurations),20}: {FractionatorConfigurations.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(ProducingLabRecipes),20}: {ProducingLabRecipes.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(PowerConsumerTypes),20}: {PowerConsumerTypes.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(BiInserterGrades),20}: {BiInserterGrades.Length,6:N0}");
        WeaverFixes.Logger.LogMessage($"\t{nameof(InserterGrades),20}: {InserterGrades.Length,6:N0}");
    }
}