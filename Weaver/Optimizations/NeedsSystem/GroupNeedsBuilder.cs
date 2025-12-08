using System;
using System.Collections.Generic;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.NeedsSystem;

internal sealed class GroupNeedsBuilder
{

    private readonly SubFactoryNeedsBuilder _needsBuilder;
    private readonly EntityType _entityType;
    private readonly int _needsStartIndex;
    private readonly List<(int[] Needs, int[] Pattern)> _needsWithPatterns = [];

    public GroupNeedsBuilder(SubFactoryNeedsBuilder needsBuilder, 
                                EntityType entityType,
                                int needsStartIndex)
    {
        _needsBuilder = needsBuilder;
        _entityType = entityType;
        _needsStartIndex = needsStartIndex;
    }

    public void AddNeeds(int[] needs, int[] pattern)
    {
        _needsWithPatterns.Add((needs, pattern));
    }

    public void Complete()
    {
        int largestPatternSize = 0;
        foreach ((int[] Needs, int[] Pattern) item in _needsWithPatterns)
        {
            largestPatternSize = Math.Max(largestPatternSize, item.Pattern.Length);
        }


        foreach ((int[] Needs, int[] Pattern) item in _needsWithPatterns)
        {
            short patternIndex = AddNeedsPattern(item.Needs, item.Pattern, largestPatternSize);
            _needsBuilder.AddNeeds(patternIndex, item.Pattern);
        }

        _needsBuilder.CompleteGroup(_entityType, new GroupNeeds(_needsStartIndex, (byte)largestPatternSize));
    }

    private short AddNeedsPattern(int[] needs, int[] pattern, int largestPatternSize)
    {
        if (pattern.Length > SubFactoryNeedsBuilder.MAX_NEEDS_LENGTH)
        {
            throw new InvalidOperationException($"Assumption that no needs pattern is larger than {SubFactoryNeedsBuilder.MAX_NEEDS_LENGTH} was incorrect. Length: {pattern.Length}");
        }

        if (needs.Length > SubFactoryNeedsBuilder.MAX_NEEDS_LENGTH)
        {
            throw new InvalidOperationException($"Assumption that no needs is larger than {SubFactoryNeedsBuilder.MAX_NEEDS_LENGTH} was incorrect. Length: {needs.Length}");
        }

        var needsPattern = new NeedsPattern(pattern, largestPatternSize);
        return _needsBuilder.AddPattern(needsPattern, largestPatternSize);
    }
}