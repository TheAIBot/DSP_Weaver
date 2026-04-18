using System;

namespace Weaver.Extensions;

internal static class ConverterUtilities
{
    public static short ThrowIfNotWithinPositiveShortRange(int value, string name)
    {
        if (value < 0 || value > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, $"{name} was not within the bounds of a short. Value: {value}");
        }

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