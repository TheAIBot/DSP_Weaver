using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Weaver.Benchmarking;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

public class InserterMultithreadingOptimization
{
    private static readonly TimeIndexedCollectionStatistic _inserterTickTimes = new TimeIndexedCollectionStatistic(200);
    private static readonly List<ExecutableGraph<InserterExecutableGraphAction>> _inserterExecutables = [];
    private static long? _gameTime;

    public static void EnableOptimization(Harmony harmony)
    {
        harmony.PatchAll(typeof(InserterMultithreadingOptimization));
    }

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogInfo($"Initializing {nameof(InserterMultithreadingOptimization)}");

        _inserterTickTimes.Clear();
        _inserterExecutables.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            List<Graph> graphs = Graphifier.ToInserterGraphs(factory);
            Graphifier.SplitLargeGraphs(graphs);
            Graphifier.CombineSmallGraphs(graphs);


            foreach (var graph in graphs)
            {
                var executableGraph = new ExecutableGraph<InserterExecutableGraphAction>(planet, graph, EntityType.Inserter, new InserterExecutableGraphAction());
                _inserterExecutables.Add(executableGraph);
            }
        }

        WeaverFixes.Logger.LogInfo($"Created {_inserterExecutables.Count} executable graphs");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PrepareInserterData))]
    private static bool PrepareInserterData_Prefix(MultithreadSystem __instance, long _time)
    {
        _gameTime = _time;
        __instance.missionOrders |= (uint)MissionOrderType.Inserter;

        foreach (var executableGraph in _inserterExecutables)
        {
            executableGraph.Prepare();
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Schedule))]
    private static bool Schedule_Prefix(MultithreadSystem __instance)
    {
        return __instance.missionOrders == (uint)MissionOrderType.Inserter ? HarmonyConstants.SKIP_ORIGINAL_METHOD : HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Complete))]
    private static bool Complete_Prefix(MultithreadSystem __instance)
    {
        if (__instance.missionOrders != (uint)MissionOrderType.Inserter)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        if (_gameTime == null)
        {
            throw new InvalidOperationException($"{nameof(_gameTime)} is null.");
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        _inserterTickTimes.EnsureCapacity(1);
        _inserterTickTimes.StartSampling(0);
        Parallel.ForEach(_inserterExecutables, parallelOptions, static executableGraph => executableGraph.Execute(_gameTime.Value));
        _inserterTickTimes.EndSampling(0);

        if (_gameTime.Value % 60 == 0)
        {
            WeaverFixes.Logger.LogInfo($"Inserter tick {_inserterTickTimes.GetAverageTimeInMilliseconds(0):N2}");
        }

        __instance.isRevAllThreadCompleteSignal = true;
        __instance.missionOrders = 0u;

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
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

internal static class LinqExtenstions
{
    public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> enumerable, int chunkSize)
    {
        List<T> chunk = new List<T>();
        IEnumerator<T> enumerator = enumerable.GetEnumerator();
        while (enumerator.MoveNext())
        {
            chunk.Add(enumerator.Current);
            if (chunk.Count == chunkSize)
            {
                yield return chunk.ToArray();
                chunk.Clear();
            }
        }

        if (chunk.Count != 0)
        {
            yield return chunk.ToArray();
        }
    }
}