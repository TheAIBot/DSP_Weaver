using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.PowerSystems;

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
