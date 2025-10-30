using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal sealed class SubFactoryNeedsBuilder
{
    private readonly GroupNeeds[] _groupNeeds = new GroupNeeds[ArrayExtensions.GetEnumValuesEnumerable<EntityType>().Max(x => (int)x) + 1];
    private readonly Dictionary<NeedsPattern, short> _needsPatternToNeedsPatternIndex = [];
    private readonly List<short> _needsPatternsFlat = [];
    private readonly List<ComponentNeeds> _needsBits = [];

    public const int MAX_NEEDS_LENGTH = 8;

    public GroupNeedsBuilder CreateGroupNeedsBuilder(EntityType entityType) => new GroupNeedsBuilder(this, entityType, _needsBits.Count);

    public void AddNeeds(short patternIndex, int[] needs)
    {
        _needsBits.Add(new ComponentNeeds(patternIndex, PatternToBits(needs)));
    }

    public short AddPattern(NeedsPattern needsPattern, int largestGroupPatternSize)
    {
        if (_needsPatternToNeedsPatternIndex.TryGetValue(needsPattern, out short patternIndex))
        {
            return patternIndex;
        }

        int newPatternIndex = _needsPatternsFlat.Count;
        if (newPatternIndex > short.MaxValue || newPatternIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(newPatternIndex)} fits in a short is not correct.");
        }

        for (int i = 0; i < largestGroupPatternSize; i++)
        {
            if (i >= needsPattern.Pattern.Length)
            {
                _needsPatternsFlat.Add(0);
                continue;
            }

            int patternValue = needsPattern.Pattern[i];
            if (patternValue > short.MaxValue || patternValue < short.MinValue)
            {
                throw new InvalidOperationException($"Assumption that {nameof(patternValue)} fits in a short is not correct.");
            }

            _needsPatternsFlat.Add((short)patternValue);
        }

        _needsPatternToNeedsPatternIndex.Add(needsPattern, (short)newPatternIndex);
        return (short)newPatternIndex;
    }

    public void CompleteGroup(EntityType entityType, GroupNeeds groupNeeds)
    {
        _groupNeeds[(int)entityType] = groupNeeds;
    }

    public SubFactoryNeeds Build()
    {
        return new SubFactoryNeeds(_groupNeeds, _needsPatternsFlat.ToArray(), _needsBits.ToArray());
    }

    private static byte PatternToBits(int[] needs)
    {
        byte bits = 0;
        for (int i = 0; i < needs.Length; i++)
        {
            bits |= (byte)((needs[i] == 0 ? 0 : 1) << i);
        }

        return bits;
    }
}
