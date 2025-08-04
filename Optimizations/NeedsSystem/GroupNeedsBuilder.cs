using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal sealed class GroupNeedsBuilder
{
    private readonly IWholeNeedsBuilder _wholeNeedsBuilder;
    private readonly EntityType _groupEntityType;
    private readonly List<int[]> _allNeeds = [];
    private int _maxNeedsSize = int.MinValue;

    public GroupNeedsBuilder(IWholeNeedsBuilder wholeNeedsBuilder, EntityType groupEntityType)
    {
        _wholeNeedsBuilder = wholeNeedsBuilder;
        _groupEntityType = groupEntityType;
    }

    public void AddNeeds(int[] needs, int needsSize)
    {
        _allNeeds.Add(needs);
        _maxNeedsSize = Math.Max(_maxNeedsSize, needsSize);
    }

    public void Complete()
    {
        List<int[]> allNeeds = _allNeeds;
        if (allNeeds.Count == 0)
        {
            return;
        }

        List<short> needsFlat = _wholeNeedsBuilder.GetNeedsFlat();
        int groupStartIndex = needsFlat.Count;
        int maxNeedsSize = _maxNeedsSize;

        for (int i = 0; i < allNeeds.Count; i++)
        {
            for (int needsIndex = 0; needsIndex < maxNeedsSize; needsIndex++)
            {

                needsFlat.Add(GetOrDefault(allNeeds[i], needsIndex));
            }
        }

        _wholeNeedsBuilder.CompletedGroup(_groupEntityType, new GroupNeeds(groupStartIndex, maxNeedsSize));
    }

    private static short GetOrDefault(int[] values, int index)
    {
        if (values.Length <= index)
        {
            return 0;
        }

        return (short)values[index];
    }
}
