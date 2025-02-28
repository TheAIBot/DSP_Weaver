using HarmonyLib;
using Weaver.Benchmarking;

namespace Weaver.Optimizations.LoadBalance;

public class InserterThreadLoadBalance
{
    internal static int[]? _inserterPerThreadItemCount = null;
    internal static bool enableStatisticsLoadBalancing = false;

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(WorkerThreadExecutorBenchmarkPatches));
        Harmony.CreateAndPatchAll(typeof(MultithreadSystemBenchmarkDisplayPatches));
        MultithreadSystemBenchmarkDisplayPatches._logResults = false;

        Harmony.CreateAndPatchAll(typeof(InserterThreadLoadBalance));
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
        if (_inserterPerThreadItemCount == null || _inserterPerThreadItemCount.Length != GameMain.multithreadSystem.usedThreadCnt)
        {
            _inserterPerThreadItemCount = new int[GameMain.multithreadSystem.usedThreadCnt];
            enableStatisticsLoadBalancing = false;
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

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WorkerThreadExecutor),
                  nameof(WorkerThreadExecutor.CalculateMissionIndex),
                  [typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)],
                  [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
    private static void CalculateMissionIndex_Loadbalance_Postfix(WorkerThreadExecutor __instance, int _curThreadIdx, int _start, int _end)
    {
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
