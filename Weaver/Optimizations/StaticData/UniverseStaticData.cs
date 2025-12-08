using Weaver.Optimizations.Assemblers;
using Weaver.Optimizations.Fractionators;
using Weaver.Optimizations.Inserters.Types;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.PowerSystems;

namespace Weaver.Optimizations.StaticData;

internal sealed class UniverseStaticData
{
    public ReadonlyArray<AssemblerRecipe> AssemblerRecipes { get; private set; } = new ReadonlyArray<AssemblerRecipe>([]);
    public ReadonlyArray<FractionatorConfiguration> FractionatorConfigurations { get; private set; } = new ReadonlyArray<FractionatorConfiguration>([]);
    public ReadonlyArray<ProducingLabRecipe> ProducingLabRecipes { get; private set; } = new ReadonlyArray<ProducingLabRecipe>([]);
    public ReadonlyArray<PowerConsumerType> PowerConsumerTypes { get; private set; } = new ReadonlyArray<PowerConsumerType>([]);
    public ReadonlyArray<BiInserterGrade> BiInserterGrades { get; private set; } = new ReadonlyArray<BiInserterGrade>([]);
    public ReadonlyArray<InserterGrade> InserterGrades { get; private set; } = new ReadonlyArray<InserterGrade>([]);

    public void UpdateAssemblerRecipes(ReadonlyArray<AssemblerRecipe> assemblerRecipes)
    {
        AssemblerRecipes = assemblerRecipes;
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
}