using System;

namespace Weaver.Optimizations.NeedsSystem;

internal record struct GroupNeeds(int GroupStartIndex, int GroupNeedsSize)
{
    public int GetObjectNeedsIndex(int objectIndex)
    {
        return GroupStartIndex + objectIndex * GroupNeedsSize;
    }

    public static void SetIfInRange(int[] copyTo, short[] copyFrom, int copyToIndex, int copyFromIndex)
    {
        if (copyTo.Length <= copyToIndex)
        {
            return;
        }

        copyTo[copyToIndex] = copyFrom[copyFromIndex];
    }

    public static void SetIfInRange(int[] copyTo, int[] copyFrom, int copyToIndex, int copyFromIndex)
    {
        if (copyTo.Length <= copyToIndex)
        {
            return;
        }

        copyTo[copyToIndex] = copyFrom[copyFromIndex];
    }

    public static short GetOrDefaultConvertToShortWithClamping(int[] values, int index, int minAllowedValue, int maxAllowedValue)
    {
        if (values.Length <= index)
        {
            return 0;
        }

        int value = values[index];
        if (value > short.MaxValue || value < short.MinValue)
        {
            value = Math.Min(Math.Max(value, minAllowedValue), maxAllowedValue);
        }

        return (short)value;
    }
}
