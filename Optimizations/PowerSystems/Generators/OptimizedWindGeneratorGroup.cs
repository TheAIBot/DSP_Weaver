using System.Runtime.InteropServices;

namespace Weaver.Optimizations.PowerSystems.Generators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal readonly struct OptimizedWindGeneratorGroup
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
        return (long)(windStrength * _genEnergyPerTick) * _componentCount;
    }
}
