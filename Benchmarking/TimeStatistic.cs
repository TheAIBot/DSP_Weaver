using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Weaver.Benchmarking;

internal sealed class TimeThreadedIndexedCollectionStatistic
{
    private readonly Stopwatch _timer = new Stopwatch();
    private SampleAverage[] _itemSampleAverages = [];
    private readonly int _maxSampleCount;

    public TimeThreadedIndexedCollectionStatistic(int maxSampleCount)
    {
        _maxSampleCount = maxSampleCount;
    }

    public float GetAverageTimeInMilliseconds(int index)
    {
        if (index >= _itemSampleAverages.Length)
        {
            throw new IndexOutOfRangeException($"{nameof(index)} is out of range. Value: {index}");
        }

        return _itemSampleAverages[index].Average;
    }

    public IEnumerable<IndexTime> GetIndexTimes()
    {
        for (int i = 0; i < _itemSampleAverages.Length; i++)
        {
            if (!_itemSampleAverages[i].IsInitialized)
            {
                continue;
            }

            yield return new IndexTime(i, _itemSampleAverages[i].Average);
        }
    }

    public void StartThreadSampling()
    {
        _timer.Restart();
    }

    public void EndThreadSampling(int index)
    {
        if (index >= _itemSampleAverages.Length)
        {
            throw new IndexOutOfRangeException($"{nameof(index)} is out of range. Value: {index}");
        }

        float time = (float)_timer.Elapsed.TotalMilliseconds;

        ref SampleAverage indexSamples = ref _itemSampleAverages[index];
        indexSamples ??= new SampleAverage();
        indexSamples.EnsureInitialized();

        indexSamples.AddSample(_maxSampleCount, time);
    }

    public void EnsureCapacity(int size)
    {
        if (_itemSampleAverages.Length >= size)
        {
            return;
        }

        Array.Resize(ref _itemSampleAverages, size);
    }

    private class SampleAverage
    {
        private Queue<float> _samples;
        private float _averageSample;

        public float Average => _averageSample / Math.Max(1, _samples.Count);

        public bool IsInitialized => _samples != null;

        public void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            _samples = [];
            _averageSample = 0;
        }

        public void AddSample(int maxSampleCount, float sample)
        {
            if (_samples.Count == maxSampleCount)
            {
                _averageSample -= _samples.Dequeue();
            }

            _samples.Enqueue(sample);
            _averageSample += sample;
        }
    }

    internal record struct IndexTime(int Index, float TimeInMilliseconds);
}


