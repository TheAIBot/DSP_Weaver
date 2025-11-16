using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.StaticData;

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