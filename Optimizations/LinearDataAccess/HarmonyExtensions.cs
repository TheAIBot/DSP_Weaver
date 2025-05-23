using HarmonyLib;

namespace Weaver.Optimizations.LinearDataAccess;

internal static class HarmonyExtensions
{
    public static void ReplaceCode(this CodeMatcher codeMatcher, CodeMatch[] start, bool replaceStart, CodeMatch[] end, bool replaceEnd, CodeInstruction[] replaceWith)
    {
        codeMatcher.MatchForward(!replaceStart, start)
                   .ThrowIfNotMatch($"Failed to find {nameof(start)}");
        int startPosition = codeMatcher.Pos;
        codeMatcher.MatchForward(replaceEnd, end)
                   .ThrowIfNotMatch($"Failed to find {nameof(end)}");
        int endPosition = codeMatcher.Pos;
        codeMatcher.Start()
                   .Advance(startPosition)
                   .RemoveInstructions(endPosition - startPosition + 1)
                   .Insert(replaceWith);
    }

    public static void PrintRelativeRangeOfInstructions(this CodeMatcher codeMatcher, int startOffset, int length)
    {
        codeMatcher.Advance(startOffset);
        for (int i = 0; i < length; i++)
        {
            WeaverFixes.Logger.LogMessage(codeMatcher.Instruction);
            codeMatcher.Advance(1);
        }
    }
}