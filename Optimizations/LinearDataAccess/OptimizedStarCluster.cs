using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

#nullable enable
namespace Weaver.Optimizations.LinearDataAccess;

internal static class OptimizedStarCluster
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedPlanet = [];
    private static readonly Queue<PlanetFactory> _newPlanets = [];
    private static readonly Queue<PlanetFactory> _planetsToReOptimize = [];
    private static readonly WorkStealingMultiThreadedFactorySimulation _workStealingMultiThreadedFactorySimulation = new();

    public static void EnableOptimization(Harmony harmony)
    {
        harmony.PatchAll(typeof(OptimizedStarCluster));
    }

    public static OptimizedPlanet GetOptimizedPlanet(PlanetFactory planet) => _planetToOptimizedPlanet[planet];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.End))]
    public static void End()
    {
        WeaverFixes.Logger.LogInfo($"Clearing optimized planets");
        // Clear optimized planets in the specific case where a new save is created which circumvents
        // the logic in LoadCurrentGame_Postfix which is other wise supposed to do it
        _planetToOptimizedPlanet.Clear();
        _workStealingMultiThreadedFactorySimulation.Clear();
    }

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogInfo($"Initializing {nameof(OptimizedPlanet)}");

        _planetToOptimizedPlanet.Clear();
        _workStealingMultiThreadedFactorySimulation.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            _planetToOptimizedPlanet.Add(planet, new OptimizedPlanet(planet));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogInfo($"Saving {nameof(OptimizedPlanet)}");

        foreach (OptimizedPlanet optimizedPlanet in _planetToOptimizedPlanet.Values)
        {
            if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
            {
                continue;
            }

            optimizedPlanet.Save();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    public static void GameData_GameTick()
    {
        lock (_newPlanets)
        {
            while (_newPlanets.Count > 0)
            {
                PlanetFactory newPlanet = _newPlanets.Dequeue();

                WeaverFixes.Logger.LogInfo($"Adding planet: {newPlanet.planet.displayName}");
                _planetToOptimizedPlanet.Add(newPlanet, new OptimizedPlanet(newPlanet));
            }
        }

        foreach (KeyValuePair<PlanetFactory, OptimizedPlanet> planetToOptimizedPlanet in _planetToOptimizedPlanet)
        {
            if (GameMain.localPlanet?.factory != planetToOptimizedPlanet.Key)
            {
                if (planetToOptimizedPlanet.Value.Status == OptimizedPlanetStatus.Stopped)
                {
                    if (planetToOptimizedPlanet.Value.OptimizeDelayInTicks > 0 &&
                        planetToOptimizedPlanet.Value.OptimizeDelayInTicks-- > 0)
                    {
                        continue;
                    }

                    WeaverFixes.Logger.LogInfo($"Optimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
                    planetToOptimizedPlanet.Value.Initialize();
                }

                continue;
            }

            if (planetToOptimizedPlanet.Value.Status == OptimizedPlanetStatus.Stopped)
            {
                continue;
            }

            WeaverFixes.Logger.LogInfo($"DeOptimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
            planetToOptimizedPlanet.Value.Save();
        }

        if (_planetsToReOptimize.Count > 0)
        {
            // Avoid lag spike by spreading load over multiple ticks
            PlanetFactory planetToReOptimize = _planetsToReOptimize.Dequeue();
            OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planetToReOptimize];
            if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
            {
                return;
            }

            WeaverFixes.Logger.LogInfo($"DeOptimizing planet: {planetToReOptimize.planet.displayName}");
            optimizedPlanet.Save();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.Init))]
    public static void PlanetFactory_Init(PlanetFactory __instance)
    {
        lock (_newPlanets)
        {
            _newPlanets.Enqueue(__instance);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.AddEntityDataWithComponents))]
    public static void PlanetFactory_AddEntityDataWithComponents(PlanetFactory __instance)
    {
        DeOptimizeDueToNonPlayerAction(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    public static void PlanetFactory_RemoveEntityWithComponents(PlanetFactory __instance)
    {
        DeOptimizeDueToNonPlayerAction(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.UpgradeEntityWithComponents))]
    public static void PlanetFactory_UpgradeEntityWithComponents(PlanetFactory __instance)
    {
        DeOptimizeDueToNonPlayerAction(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.InserterPartExecute))]
    public static bool InserterPartExecute(WorkerThreadExecutor __instance)
    {
        InserterPartExecute(__instance, x => x._optimizedBiInserterExecutor, true);
        InserterPartExecute(__instance, x => x._optimizedInserterExecutor, false);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void InserterPartExecute<T>(WorkerThreadExecutor __instance, Func<OptimizedPlanet, InserterExecutor<T>?> inserterExecutorSelector, bool runOriginalLogicOnUnoptimizedPlanets)
        where T : struct, IInserter<T>
    {
        if (__instance.inserterFactories == null)
        {
            return;
        }
        int totalGalaxyInserterCount = 0;
        for (int planetIndex = 0; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
            InserterExecutor<T>? optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            int inserterCount;
            if (optimizedInserterExecutor != null)
            {
                inserterCount = optimizedInserterExecutor.InserterCount;
            }
            else if (runOriginalLogicOnUnoptimizedPlanets)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = 0;
            }

            totalGalaxyInserterCount += inserterCount;
        }
        int minimumMissionCnt = 64;
        if (!WorkerThreadExecutor.CalculateMissionIndex(totalGalaxyInserterCount, __instance.usedThreadCnt, __instance.curThreadIdx, minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }
        int threadStartingPlanetIndex = 0;
        int totalInsertersSeenOnPreviousPlanets = 0;
        for (int planetIndex = 0; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
            InserterExecutor<T>? optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            int inserterCount;
            if (optimizedInserterExecutor != null)
            {
                inserterCount = optimizedInserterExecutor.InserterCount;
            }
            else if (runOriginalLogicOnUnoptimizedPlanets)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = 0;
            }

            int totalInsertersIncludingOnThisPlanets = totalInsertersSeenOnPreviousPlanets + inserterCount;
            if (totalInsertersIncludingOnThisPlanets <= _start)
            {
                totalInsertersSeenOnPreviousPlanets = totalInsertersIncludingOnThisPlanets;
                continue;
            }
            threadStartingPlanetIndex = planetIndex;
            break;
        }
        for (int planetIndex = threadStartingPlanetIndex; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
            InserterExecutor<T>? optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            int inserterCount;
            if (optimizedInserterExecutor != null)
            {
                inserterCount = optimizedInserterExecutor.InserterCount;
            }
            else if (runOriginalLogicOnUnoptimizedPlanets)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = 0;
            }

            int num5 = _start - totalInsertersSeenOnPreviousPlanets;
            int num6 = _end - totalInsertersSeenOnPreviousPlanets;
            if (_end - _start > inserterCount - num5)
            {
                try
                {
                    if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
                    {
                        if (optimizedInserterExecutor == null)
                        {
                            throw new InvalidOperationException("InserterExecutor was null while the optimized planet was running.");
                        }

                        optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, inserterCount);
                    }
                    else if (runOriginalLogicOnUnoptimizedPlanets)
                    {
                        bool isActive = __instance.inserterLocalPlanet == __instance.inserterFactories[planetIndex].planet;
                        __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, num5, inserterCount);
                    }
                    totalInsertersSeenOnPreviousPlanets += inserterCount;
                    _start = totalInsertersSeenOnPreviousPlanets;
                }
                catch (Exception ex)
                {
                    __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter first gametick total cursor: " + inserterCount + "  Start & End: " + num5 + "/" + inserterCount + "  " + ex;
                    __instance.hasErrorMessage = true;
                }
                continue;
            }
            try
            {
                if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
                {
                    if (optimizedInserterExecutor == null)
                    {
                        throw new InvalidOperationException("InserterExecutor was null while the optimized planet was running.");
                    }

                    optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, num6);
                }
                else if (runOriginalLogicOnUnoptimizedPlanets)
                {
                    bool isActive = __instance.inserterLocalPlanet == __instance.inserterFactories[planetIndex].planet;
                    __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, num5, num6);
                }
                break;
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter second gametick total cursor: " + inserterCount + "  Start & End: " + num5 + "/" + num6 + "  " + ex2;
                __instance.hasErrorMessage = true;
                break;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem),
                  nameof(FactorySystem.GameTick),
                  [typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int)])]
    public static bool FactorySystem_GameTick(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet.GameTick(__instance.factory, time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem),
                  nameof(FactorySystem.GameTickLabProduceMode),
                  [typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int)])]
    public static bool FactorySystem_GameTickLabProduceMode(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet._producingLabExecutor.GameTickLabProduceMode(__instance.factory, time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.ParallelGameTickBeforePower))]
    public static bool FactorySystem_ParallelGameTickBeforePower(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet._optimizedPowerSystem.FactorySystem_ParallelGameTickBeforePower(__instance.factory, optimizedPlanet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CargoTraffic), nameof(CargoTraffic.ParallelGameTickBeforePower))]
    public static bool CargoTraffic_ParallelGameTickBeforePower(CargoTraffic __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet._optimizedPowerSystem.CargoTraffic_ParallelGameTickBeforePower(__instance.factory, optimizedPlanet, time, isActive, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    public static bool GameTick(PowerSystem __instance, long time, bool isActive, bool isMultithreadMode = false)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet._optimizedPowerSystem.GameTick(__instance.factory, time, isActive, isMultithreadMode);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabResearchMode))]
    public static bool GameTickLabResearchMode(FactorySystem __instance, long time, bool isActive)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        bool hasResearchedTechnology = optimizedPlanet._researchingLabExecutor.GameTickLabResearchMode(__instance.factory, time);
        if (hasResearchedTechnology)
        {
            foreach (PlanetFactory planetToReOptimize in _planetToOptimizedPlanet.Where(x => x.Value.Status == OptimizedPlanetStatus.Running)
                                                                                 .Select(x => x.Key))
            {
                _planetsToReOptimize.Enqueue(planetToReOptimize);
            }

        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem),
                  nameof(FactorySystem.GameTickLabOutputToNext),
                  [typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int)])]
    public static bool GameTickLabOutputToNext(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[__instance.factory];
        if (optimizedPlanet.Status != OptimizedPlanetStatus.Running)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedPlanet._producingLabExecutor.GameTickLabOutputToNext(time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);
        optimizedPlanet._researchingLabExecutor.GameTickLabOutputToNext(time, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    static IEnumerable<CodeInstruction> ReplaceMultithreadedSimulationLogic(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        // GameMain.multithreadSystem.multithreadSystemEnable
        CodeMatch[] multithreadedIfCondition = [
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MultithreadSystem), nameof(MultithreadSystem.multithreadSystemEnable)))
        ];

        // PerformanceMonitor.BeginSample(ECpuWorkEntry.PowerSystem);
        CodeMatch[] beginSamplePowerPerformanceMonitor = [
            new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.PowerSystem),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.BeginSample)))
        ];

        // PerformanceMonitor.EndSample(ECpuWorkEntry.Digital);
        CodeMatch[] endSampleDigitalPerformanceMonitor = [
            new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Digital),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.EndSample)))
        ];

        CodeInstruction[] optimizedMultithreading;
        if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(ModDependencies.SampleAndHoldSimId, out var _))
        {
            optimizedMultithreading = [
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(AccessTools.TypeByName("SampleAndHoldSim.GameData_Patch"), "workFactories")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OptimizedStarCluster), nameof(ExecuteSimulation)))
            ];
        }
        else
        {
            optimizedMultithreading = [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(GameData), nameof(GameData.factories))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OptimizedStarCluster), nameof(ExecuteSimulation)))
            ];
        }

        codeMatcher.MatchForward(true, multithreadedIfCondition)
                   .ThrowIfNotMatch($"Failed to find {nameof(multithreadedIfCondition)}")
                   .MatchForward(false, beginSamplePowerPerformanceMonitor)
                   .ThrowIfNotMatch($"Failed to find {nameof(beginSamplePowerPerformanceMonitor)}");
        int startPosition = codeMatcher.Pos;
        codeMatcher.MatchForward(true, endSampleDigitalPerformanceMonitor)
                   .ThrowIfNotMatch($"Failed to find {nameof(endSampleDigitalPerformanceMonitor)}");
        int endPosition = codeMatcher.Pos;
        codeMatcher.Start()
                   .Advance(startPosition)
                   .RemoveInstructions(endPosition - startPosition + 1)
                   .Insert(optimizedMultithreading);

        //codeMatcher.Start()
        //           .Advance(startPosition);
        //codeMatcher.Advance(-10);
        //for (int i = 0; i < 50; i++)
        //{
        //    WeaverFixes.Logger.LogMessage(codeMatcher.Instruction);
        //    codeMatcher.Advance(1);
        //}

        return codeMatcher.InstructionEnumeration();
    }

    private static void ExecuteSimulation(PlanetFactory[] planets)
    {
        _workStealingMultiThreadedFactorySimulation.Simulate(planets);
    }

    private static void DeOptimizeDueToNonPlayerAction(PlanetFactory planet)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
        {
            WeaverFixes.Logger.LogInfo($"DeOptimizing planet: {planet.planet.displayName}");
            optimizedPlanet.Save();
        }
        optimizedPlanet.OptimizeDelayInTicks = 200;
    }
}
