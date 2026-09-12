using System;

namespace Weaver.Extensions;

internal static class ConverterUtilities
{
    public static short ThrowIfNotWithinPositiveShortRange(int value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfOutsideZeroToShortMaxValue(value, name);

        return (short)value;
    }

    internal static short[] ConvertToShortArrayOrThrow(int[] values, string name)
    {
        var shortValues = new short[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            ThrowIfNotWithinPositiveShortRange(values[i], name);
            shortValues[i] = (short)values[i];
        }

        return shortValues;
    }
}