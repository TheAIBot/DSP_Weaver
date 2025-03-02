using HarmonyLib;
using System;
using System.Text;
using Weaver.Benchmarking;

namespace Weaver.Optimizations.LoadBalance;

public class InserterThreadLoadBalance
{
    private static uint _updateCounter = 0;
    internal static int[]? _inserterPerThreadItemCount = null;
    internal static bool enableStatisticsLoadBalancing = false;

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(WorkerThreadExecutorBenchmarkPatches));
        Harmony.CreateAndPatchAll(typeof(MultithreadSystemBenchmarkDisplayPatches));
        MultithreadSystemBenchmarkDisplayPatches._logResults = false;

        Harmony.CreateAndPatchAll(typeof(InserterThreadLoadBalance));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Prefix()
    {
        _updateCounter = 0;
        _inserterPerThreadItemCount = null;
        enableStatisticsLoadBalancing = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Schedule))]
    private static void Schedule_Prefix(MultithreadSystem __instance, out bool __state)
    {
        if (__instance.missionOrders != (uint)MissionOrderType.Inserter)
        {
            __state = false;
            return;
        }
        __state = true;

        _updateCounter++;
        //MultithreadSystemBenchmarkDisplayPatches._logResults = true;

        if (_inserterPerThreadItemCount == null || _inserterPerThreadItemCount.Length != GameMain.multithreadSystem.usedThreadCnt)
        {
            _inserterPerThreadItemCount = new int[GameMain.multithreadSystem.usedThreadCnt];
            enableStatisticsLoadBalancing = false;
            return;
        }

        const int updateRateToSampleCountRatio = 4;
        const int updateRate = MultithreadSystemBenchmarkDisplayPatches._sampleCount / updateRateToSampleCountRatio;
        if (_updateCounter % updateRate == 0)
        {
            StringBuilder threadTimes = new StringBuilder();
            for (int i = 0; i < _inserterPerThreadItemCount.Length; i++)
            {
                threadTimes.Append($" {_inserterPerThreadItemCount[i],7:N0}");
            }
            WeaverFixes.Logger.LogMessage($"{nameof(MultithreadSystem)} Sum: {_inserterPerThreadItemCount.Sum()} Thread item counts: {threadTimes}");
            MultithreadSystemBenchmarkDisplayPatches.LogComputeTimes(MissionOrderType.Inserter, WorkerThreadExecutorBenchmarkPatches._missionComputeTimes[(uint)MissionOrderType.Inserter]);
            RebalanceInsertersPerThread(__instance);
        }

        RescaleToCurrentInserterCount(__instance);
    }

    private static void RebalanceInsertersPerThread(MultithreadSystem __instance)
    {
        var computeTimes = WorkerThreadExecutorBenchmarkPatches._missionComputeTimes[(uint)MissionOrderType.Inserter];
        if (!computeTimes.IsFilledWithData(_inserterPerThreadItemCount.Length))
        {
            return;
        }

        float timeSum = 0;
        for (int i = 0; i < _inserterPerThreadItemCount.Length; i++)
        {
            timeSum += computeTimes.GetAverageTimeInMilliseconds(i);
        }

        float averageThreadTime = timeSum / _inserterPerThreadItemCount.Length;

        int insertersUnderAverageTimeCount = 0;
        for (int z = 0; z < _inserterPerThreadItemCount.Length; z++)
        {
            if (computeTimes.GetAverageTimeInMilliseconds(z) > averageThreadTime)
            {
                continue;
            }

            insertersUnderAverageTimeCount++;
        }

        const float maxAllowedPositiveTimeDifference = 1.01f;
        const float redistributionRate = 1.0f;
        for (int i = 0; i < _inserterPerThreadItemCount.Length; i++)
        {
            float threadTime = computeTimes.GetAverageTimeInMilliseconds(i);
            if (threadTime <= averageThreadTime * maxAllowedPositiveTimeDifference)
            {
                continue;
            }

            int insertersRemovedFromThread = (int)(_inserterPerThreadItemCount[i] * ((1.0f - (1.0f / (threadTime / averageThreadTime))) * redistributionRate));
            int insertersPerThread = insertersRemovedFromThread / insertersUnderAverageTimeCount;

            for (int z = 0; z < _inserterPerThreadItemCount.Length; z++)
            {
                if (i == z)
                {
                    continue;
                }

                if (computeTimes.GetAverageTimeInMilliseconds(z) > averageThreadTime)
                {
                    continue;
                }

                _inserterPerThreadItemCount[z] += insertersPerThread;
                _inserterPerThreadItemCount[i] -= insertersPerThread;
            }
        }
    }

    private static void RescaleToCurrentInserterCount(MultithreadSystem __instance)
    {
        PlanetFactory[] inserterFactories = __instance.workerThreadExecutors[0].inserterFactories;
        int inserterFactoryCount = __instance.workerThreadExecutors[0].inserterFactoryCnt;
        int currentInserterCount = 0;
        for (int i = 0; i < inserterFactoryCount; i++)
        {
            currentInserterCount += inserterFactories[i].factorySystem.inserterCursor;
        }

        int previousInserterCount = _inserterPerThreadItemCount.Sum();
        int inserterDifference = currentInserterCount - previousInserterCount;
        int inserterChangePerThread = (inserterDifference + (_inserterPerThreadItemCount.Length - 1)) / _inserterPerThreadItemCount.Length;
        for (int i = 0; i < _inserterPerThreadItemCount.Length; i++)
        {
            _inserterPerThreadItemCount[i] += inserterChangePerThread;
        }

        int checkInserterSum = _inserterPerThreadItemCount.Sum();
        if (checkInserterSum < currentInserterCount)
        {
            throw new InvalidOperationException($"""
                Rescaled inserter count is less than the total inserter count.
                Rescaled count: {checkInserterSum}
                Expected count: {currentInserterCount}
                """);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Schedule))]
    private static void Schedule_Postfix(MultithreadSystem __instance, bool __state)
    {
        if (!__state)
        {
            return;
        }

        enableStatisticsLoadBalancing = true;
        MultithreadSystemBenchmarkDisplayPatches._logResults = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PrepareInserterData))]
    private static void PrepareInserterData_Loadbalance(MultithreadSystem __instance)
    {
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor),
                  nameof(WorkerThreadExecutor.CalculateMissionIndex),
                  [typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)],
                  [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
    private static bool CalculateMissionIndex_Loadbalance_Prefix(WorkerThreadExecutor __instance, ref bool __result, int _curThreadIdx, ref int _start, ref int _end)
    {
        if (!enableStatisticsLoadBalancing)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        // Always use thread if it has items assigned to it
        __result = _inserterPerThreadItemCount[_curThreadIdx] > 0;

        for (int i = 0; i < _curThreadIdx; i++)
        {
            _start += _inserterPerThreadItemCount[i];
        }
        _end = _start + _inserterPerThreadItemCount[_curThreadIdx];

        //WeaverFixes.Logger.LogMessage($"{_curThreadIdx} {_start} {_end}");
        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WorkerThreadExecutor),
                  nameof(WorkerThreadExecutor.CalculateMissionIndex),
                  [typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)],
                  [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
    private static void CalculateMissionIndex_Loadbalance_Postfix(WorkerThreadExecutor __instance, int _curThreadIdx, int _start, int _end)
    {
        //WeaverFixes.Logger.LogMessage($"{_curThreadIdx} {_start} {_end}");
        if (enableStatisticsLoadBalancing)
        {
            return;
        }

        if (_inserterPerThreadItemCount == null)
        {
            return;
        }

        _inserterPerThreadItemCount[_curThreadIdx] = _end - _start;
    }
}

internal static class ArrayExtensions
{
    public static int Sum(this int[] array)
    {
        int sum = 0;
        for (int i = 0; i < array.Length; i++)
        {
            sum += array[i];
        }

        return sum;
    }
}