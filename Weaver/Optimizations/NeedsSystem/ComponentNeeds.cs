namespace Weaver.Optimizations.NeedsSystem;

internal struct ComponentNeeds
{
    public readonly short PatternIndex { get; }
    public byte Needs { get; set; }

    public bool HasAnyNeeds => Needs != 0;

    public ComponentNeeds(short patternIndex, byte needs)
    {
        PatternIndex = patternIndex;
        Needs = needs;
    }

    public readonly bool GetNeeds(int index)
    {
        return ((Needs >> index) & 1) == 1;
    }

    public readonly bool AnyMatch(short[] needsPatterns, int match, int needsSize)
    {
        for (int i = 0; i < needsSize; i++)
        {
            if (GetNeeds(i) && needsPatterns[PatternIndex + i] == match)
            {
                return true;
            }
        }

        return false;
    }
}
