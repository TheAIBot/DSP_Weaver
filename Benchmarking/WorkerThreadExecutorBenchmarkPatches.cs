using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Weaver.Benchmarking;

public class WorkerThreadExecutorBenchmarkPatches
{
    internal static readonly Dictionary<uint, TimeIndexedCollectionStatistic> _missionComputeTimes = [];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Prefix()
    {
        foreach (var computeTime in _missionComputeTimes.Values)
        {
            computeTime.Clear();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.ComputerThread))]
    private static void ComputerThread_Prefix(WorkerThreadExecutor __instance, out uint __state)
    {
        if (!_missionComputeTimes.TryGetValue(__instance.threadMissionOrders, out TimeIndexedCollectionStatistic computeTimes))
        {
            __state = 0;
            return;
        }

        __state = __instance.threadMissionOrders;
        computeTimes.StartSampling(__instance.curThreadIdx);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.ComputerThread))]
    private static void ComputerThread_Postfix(WorkerThreadExecutor __instance, uint __state)
    {
        if (!_missionComputeTimes.TryGetValue(__state, out TimeIndexedCollectionStatistic computeTimes))
        {
            return;
        }

        computeTimes.EndSampling(__instance.curThreadIdx);
    }
}

public class MultithreadSystemBenchmarkDisplayPatches
{
    private static TimeIndexedCollectionStatistic? _latestComputeTimes = null;
    private static uint _latestMissionOrder = 0;
    internal static bool _logResults = true;
    internal const int _sampleCount = 60;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Prefix()
    {
        _latestComputeTimes = null;
        _latestMissionOrder = 0;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Schedule))]
    private static void Schedule_Prefix(MultithreadSystem __instance)
    {
        if (!WorkerThreadExecutorBenchmarkPatches._missionComputeTimes.TryGetValue(__instance.missionOrders, out _latestComputeTimes))
        {
            _latestComputeTimes = new TimeIndexedCollectionStatistic(_sampleCount);
            WorkerThreadExecutorBenchmarkPatches._missionComputeTimes.Add(__instance.missionOrders, _latestComputeTimes);
        }

        _latestComputeTimes.EnsureCapacity(GameMain.multithreadSystem.usedThreadCnt);
        _latestMissionOrder = __instance.missionOrders;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Complete))]
    private static void Complete_Postfix(MultithreadSystem __instance)
    {
        if (_latestComputeTimes == null || !_logResults)
        {
            return;
        }

        LogComputeTimes((MissionOrderType)_latestMissionOrder, _latestComputeTimes);
    }

    internal static void LogComputeTimes(MissionOrderType missionOrder, TimeIndexedCollectionStatistic computeTimes)
    {
        float minTime = float.MaxValue;
        float maxTime = float.MinValue;
        StringBuilder threadTimes = new StringBuilder();
        foreach (TimeIndexedCollectionStatistic.IndexTime indexTime in computeTimes.GetIndexTimes())
        {
            threadTimes.Append($" {indexTime.Index,2}: {indexTime.TimeInMilliseconds,4:N1}");
            minTime = Math.Min(minTime, indexTime.TimeInMilliseconds);
            maxTime = Math.Max(maxTime, indexTime.TimeInMilliseconds);
        }

        string missionName = Enum.GetName(typeof(MissionOrderType), missionOrder);
        WeaverFixes.Logger.LogInfo($"{nameof(MultithreadSystem)} Mission: {missionName,18} Min: {minTime,4:N1} Max: {maxTime,4:N1} Thread Times: {threadTimes}");
    }
}