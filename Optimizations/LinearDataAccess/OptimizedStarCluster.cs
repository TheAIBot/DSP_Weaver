﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Weaver.Optimizations.LinearDataAccess.Inserters;
using Weaver.Optimizations.LinearDataAccess.Inserters.Types;

#nullable enable
namespace Weaver.Optimizations.LinearDataAccess;

internal static class OptimizedStarCluster
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedPlanet = [];
    private static readonly Queue<PlanetFactory> _newPlanets = [];
    private static readonly Queue<PlanetFactory> _planetsToReOptimize = [];

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(OptimizedStarCluster));
    }

    public static OptimizedPlanet GetOptimizedPlanet(PlanetFactory planet) => _planetToOptimizedPlanet[planet];

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(OptimizedPlanet)}");

        _planetToOptimizedPlanet.Clear();

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
        WeaverFixes.Logger.LogMessage($"Saving {nameof(OptimizedPlanet)}");

        foreach (OptimizedPlanet optimizedPlanet in _planetToOptimizedPlanet.Values)
        {
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

                WeaverFixes.Logger.LogMessage($"Adding planet: {newPlanet.planet.displayName}");
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

                    WeaverFixes.Logger.LogMessage($"Optimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
                    planetToOptimizedPlanet.Value.Initialize();
                }

                continue;
            }

            if (planetToOptimizedPlanet.Value.Status == OptimizedPlanetStatus.Stopped)
            {
                continue;
            }

            WeaverFixes.Logger.LogMessage($"DeOptimizing planet: {planetToOptimizedPlanet.Key.planet.displayName}");
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

            WeaverFixes.Logger.LogMessage($"DeOptimizing planet: {planetToReOptimize.planet.displayName}");
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
        InserterPartExecute(__instance, x => x._optimizedBiInserterExecutor);
        InserterPartExecute(__instance, x => x._optimizedInserterExecutor);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void InserterPartExecute<T>(WorkerThreadExecutor __instance, Func<OptimizedPlanet, InserterExecutor<T>?> inserterExecutorSelector)
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
            if (optimizedInserterExecutor == null)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = optimizedInserterExecutor.inserterCount;
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
            if (optimizedInserterExecutor == null)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = optimizedInserterExecutor.inserterCount;
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
            if (optimizedInserterExecutor == null)
            {
                inserterCount = planet.factorySystem.inserterCursor;
            }
            else
            {
                inserterCount = optimizedInserterExecutor.inserterCount;
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
                    else
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
                else
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
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.AssemblerPartExecute))]
    public static bool AssemblerPartExecute(WorkerThreadExecutor __instance)
    {
        if (__instance.assemblerFactories == null)
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }
        for (int i = 0; i < __instance.assemblerFactoryCnt; i++)
        {
            if (__instance.assemblerFactories[i].factorySystem == null)
            {
                continue;
            }

            PlanetFactory planet = __instance.assemblerFactories[i];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];

            try
            {
                if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
                {
                    optimizedPlanet.GameTick(planet, __instance.assemblerTime, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                }
                else
                {
                    bool isActive = __instance.assemblerLocalPlanet == __instance.assemblerFactories[i].planet;
                    __instance.assemblerFactories[i].factorySystem.GameTick(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                }
            }
            catch (Exception ex)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Assembler Factory idx:" + i.ToString() + " Assembler gametick " + ex;
                __instance.hasErrorMessage = true;
            }

            try
            {
                if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
                {
                    optimizedPlanet._producingLabExecutor.GameTickLabProduceMode(planet, __instance.assemblerTime, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                }
                else
                {
                    bool isActive = __instance.assemblerLocalPlanet == __instance.assemblerFactories[i].planet;
                    __instance.assemblerFactories[i].factorySystem.GameTickLabProduceMode(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                }
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Lab Produce Factory idx:" + i.ToString() + " lab produce gametick " + ex2;
                __instance.hasErrorMessage = true;
            }
        }

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
    static IEnumerable<CodeInstruction> ReplaceSingleThreadedSpraycoaterLogicWithParallelOptimizedLogic(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        CodeMatch[] sprayCoaterGameTickCall = [
            // factories[num3].cargoTraffic.SpraycoaterGameTick();
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameData), nameof(GameData.factories))),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Ldelem_Ref),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetFactory), nameof(PlanetFactory.cargoTraffic))),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(CargoTraffic), nameof(CargoTraffic.SpraycoaterGameTick))),
        ];

        CodeMatch[] afterSpraycoaterLoop = [
            new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Storage),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.BeginSample)))
        ];

        codeMatcher.MatchForward(false, sprayCoaterGameTickCall)
            .ThrowIfNotMatch($"Failed to find {nameof(sprayCoaterGameTickCall)}")
            .RemoveInstructions(sprayCoaterGameTickCall.Length)
            .MatchForward(false, afterSpraycoaterLoop)
            .ThrowIfNotMatch($"Failed to find {nameof(afterSpraycoaterLoop)}")
            .Insert([
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OptimizedPlanet), nameof(OptimizedPlanet.ParallelSpraycoaterLogic)))
            ]);

        return codeMatcher.InstructionEnumeration();
    }

    private static void DeOptimizeDueToNonPlayerAction(PlanetFactory planet)
    {
        OptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
        {
            WeaverFixes.Logger.LogMessage($"DeOptimizing planet: {planet.planet.displayName}");
            optimizedPlanet.Save();
        }
        optimizedPlanet.OptimizeDelayInTicks = 200;
    }
}
