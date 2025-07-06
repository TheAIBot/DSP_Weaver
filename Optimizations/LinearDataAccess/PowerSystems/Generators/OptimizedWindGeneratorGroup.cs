using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems.Generators;

[StructLayout(LayoutKind.Auto)]
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
