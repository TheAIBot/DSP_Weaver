using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Weaver.Benchmarking;

internal sealed class TimeIndexedCollectionStatistic
{
    private Stopwatch[] _timers = [];
    private SampleAverage[] _itemSampleAverages = [];
    private readonly int _maxSampleCount;


    public TimeIndexedCollectionStatistic(int maxSampleCount)
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

    public void StartSampling(int index)
    {
        _timers[index].Restart();
    }

    public void EndSampling(int index)
    {
        if (index >= _itemSampleAverages.Length)
        {
            throw new IndexOutOfRangeException($"{nameof(index)} is out of range. Value: {index}");
        }

        float time = (float)_timers[index].Elapsed.TotalMilliseconds;

        ref SampleAverage indexSamples = ref _itemSampleAverages[index];
        //indexSamples ??= new SampleAverage();
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
        Array.Resize(ref _timers, size);
        for (int i = 0; i < _timers.Length; i++)
        {
            _timers[i] ??= new Stopwatch();
        }
    }

    public bool IsFilledWithData(int length)
    {
        if (_itemSampleAverages.Length > length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"{nameof(length)} is out of range. Value: {length}");
        }

        for (int i = 0; i < length; i++)
        {
            if (!_itemSampleAverages[i].IsFilledWithData(_maxSampleCount))
            {
                return false;
            }
        }

        return true;
    }

    private struct SampleAverage
    {
        private Queue<float> _samples;
        private float _averageSample;

        public readonly float Average => _averageSample / Math.Max(1, _samples.Count);

        public readonly bool IsInitialized => _samples != null;

        public bool IsFilledWithData(int length)
        {
            if (!IsInitialized)
            {
                return false;
            }

            return _samples.Count == length;
        }

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


