using System.Runtime.InteropServices;

namespace Weaver.Optimizations.PowerSystems.Generators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
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

    public readonly long EnergyCap_GTH()
    {
        float currentStrength = gthStrength * gthAffectStrength * warmup;
        return (long)(genEnergyPerTick * currentStrength);
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
