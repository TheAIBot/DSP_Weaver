using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Weaver.Optimizations.StaticData;

internal sealed class DataDeduplicator<T> where T : struct, IEquatable<T>, IMemorySize
{
    private readonly Dictionary<T, int> _uniqueValueToIndex;
    private readonly List<T> _uniqueValues;
    private bool _dataUpdated;

    public int TotalBytes { get; private set; }
    public int BytesDeduplicated { get; private set; }

    public DataDeduplicator()
    {
        _uniqueValueToIndex = [];
        _uniqueValues = [];
        _dataUpdated = true;
    }

    public int GetDeduplicatedValueIndex(ref readonly T potentiallyDuplicatedValue)
    {
        int dataSize = potentiallyDuplicatedValue.GetSize();
        TotalBytes += dataSize;

        if (!_uniqueValueToIndex.TryGetValue(potentiallyDuplicatedValue, out int deduplicatedIndex))
        {
            _dataUpdated = true;
            deduplicatedIndex = _uniqueValues.Count;
            _uniqueValues.Add(potentiallyDuplicatedValue);
            _uniqueValueToIndex.Add(potentiallyDuplicatedValue, deduplicatedIndex);
        }
        else
        {
            BytesDeduplicated += dataSize;
        }


        return deduplicatedIndex;
    }

    public bool TryGetUpdatedData([NotNullWhen(true)] out ReadonlyArray<T>? updatedData)
    {
        if (!_dataUpdated)
        {
            updatedData = null;
            return false;
        }

        _dataUpdated = false;
        updatedData = new ReadonlyArray<T>(_uniqueValues.ToArray());
        return true;
    }

    public void Clear()
    {
        _uniqueValueToIndex.Clear();
        _uniqueValues.Clear();
        _dataUpdated = true;
        TotalBytes = 0;
        BytesDeduplicated = 0;
    }
}
