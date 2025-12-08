using System;
using System.Runtime.CompilerServices;

namespace Weaver.Extensions;

internal static class ExceptionExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        public static void ThrowIfOutsideZeroToShortMaxValue(int value, [CallerArgumentExpression(nameof(value))] string? valueName = null)
        {
            if (value < 0 || value > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException(valueName, $"{valueName} was not within the bounds of 0 to {short.MaxValue:N0}. Value: {value}");
            }
        }

        public static void ThrowIfOutsideZeroToByteMaxValue(int value, [CallerArgumentExpression(nameof(value))] string? valueName = null)
        {
            if (value < 0 || value > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(valueName, $"{valueName} was not within the bounds of 0 to {byte.MaxValue:N0}. Value: {value}");
            }
        }

        public static void ThrowIfOutsideByteStackSize(int value, [CallerArgumentExpression(nameof(value))] string? valueName = null)
        {
            if (value < 0 || value > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(valueName, $"""
                    {valueName} was not within the bounds of 0 to {byte.MaxValue:N0}.
                    Value: {value}
                    This is either due to an error in Weaver or because a mod is being
                    used that modifies the max item stack size of entities.
                    """);

            }
        }

        public static void ThrowIfProliferationOutsideZeroToMaxByteValue(int value, [CallerArgumentExpression(nameof(value))] string? valueName = null)
        {
            if (value < 0 || value > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(valueName, $"""
                    {valueName} was not within the bounds of 0 to {byte.MaxValue:N0}.
                    Value: {value}
                    This is either due to an error in Weaver or due to a mod that increases
                    the max item proliferation or which increases the max item stack size of entities.
                    """);

            }
        }
    }
}
