using System;
using System.Collections.Generic;

namespace Weaver.Extensions;

internal static class ArrayExtensions
{
    public static int Sum(this int[] array)
    {
        int sum = 0;
        for (int i = 0; i < array.Length; i++)
        {
            sum += array[i];
        }

        return sum;
    }

    public static IEnumerable<T> GetEnumValuesEnumerable<T>()
    {
        foreach (T enumValue in Enum.GetValues(typeof(T)))
        {
            yield return enumValue;
        }
    }

    public static void Fill(this int[] array, int value)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = value;
        }
    }
}
