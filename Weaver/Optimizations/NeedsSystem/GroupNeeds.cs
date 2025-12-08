using System;

namespace Weaver.Optimizations.NeedsSystem;

internal record struct GroupNeeds(int GroupNeedStartIndex, byte GroupNeedsSize)
{
    public int GetObjectNeedsIndex(int objectIndex)
    {
        if (GroupNeedsSize == 0)
        {
            return 0;
        }

        return GroupNeedStartIndex + objectIndex;
    }

    public static void SetIfInRange(int[] copyTo, short[] copyFrom, int copyToIndex, int copyFromIndex)
    {
        if (copyTo.Length <= copyToIndex)
        {
            return;
        }

        copyTo[copyToIndex] = copyFrom[copyFromIndex];
    }

    public static void SetNeedsIfInRange(int[] copyTo, ComponentNeeds componentNeeds, short[] needsPatterns, int copyIndex)
    {
        if (copyTo.Length <= copyIndex)
        {
            return;
        }

        copyTo[copyIndex] = componentNeeds.GetNeeds(copyIndex) ? needsPatterns[componentNeeds.PatternIndex + copyIndex] : 0;
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