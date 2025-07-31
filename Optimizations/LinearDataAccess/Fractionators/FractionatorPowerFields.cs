using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Fractionators;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct FractionatorPowerFields
{
    public bool isWorking;
    public int fluidInputCount;
    public float fluidInputCargoCount;
    public int fluidInputInc;

    public FractionatorPowerFields(ref readonly FractionatorComponent fractionator)
    {
        isWorking = fractionator.isWorking;
        fluidInputCount = fractionator.fluidInputCount;
        fluidInputCargoCount = fractionator.fluidInputCargoCount;
        fluidInputInc = fractionator.fluidInputInc;
    }

    public readonly int incLevel
    {
        get
        {
            if (fluidInputCount <= 0 || fluidInputInc <= 0)
            {
                return 0;
            }
            int num = fluidInputInc / fluidInputCount;
            if (num >= 10)
            {
                return 10;
            }
            return num;
        }
    }
}
