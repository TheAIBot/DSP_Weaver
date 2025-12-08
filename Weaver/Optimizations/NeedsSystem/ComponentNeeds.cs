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
}
