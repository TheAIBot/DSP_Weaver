﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.Optimizations.Labs;
using Weaver.Optimizations.Statistics;
using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations;

internal static class OptimizedStarCluster
{
    private static readonly Dictionary<PlanetFactory, IOptimizedPlanet> _planetToOptimizedPlanet = [];
    private static readonly Dictionary<FactoryProductionStat, IOptimizedPlanet> _planetProductionStatisticsToOptimizedPlanet = [];
    private static readonly Queue<PlanetFactory> _newPlanets = [];
    private static readonly Queue<PlanetFactory> _planetsToReOptimize = [];
    public static readonly StarClusterResearchManager _starClusterResearchManager = new();
    public static readonly DysonSphereManager _dysonSphereManager = new();
    private static readonly DysonSphereStatisticsManager _dysonSphereStatisticsManager = new();
    private static readonly WorkStealingMultiThreadedFactorySimulation _workStealingMultiThreadedFactorySimulation = new(_starClusterResearchManager, _dysonSphereManager);
    private static bool _clearOptimizedPlanetsOnNextTick = false;
    private static bool _firstUpdate = true;

    private static readonly Random random = new Random();
    private static bool _debugEnableHeavyReOptimization = false;
    private static bool _enableStatistics = false;

    public static bool ForceOptimizeLocalPlanet { get; set; } = false;

    public static void EnableOptimization(Harmony harmony)
    {
        harmony.PatchAll(typeof(OptimizedStarCluster));
    }

    public static void DebugEnableHeavyReOptimization()
    {
        _debugEnableHeavyReOptimization = true;
    }

    public static void EnableStatistics()
    {
        _enableStatistics = true;
    }

    public static IOptimizedPlanet GetOptimizedPlanet(PlanetFactory planet) => _planetToOptimizedPlanet[planet];
    public static bool TryGetOptimizedPlanet(FactoryProductionStat productionStatistics, [NotNullWhen(true)] out IOptimizedPlanet? optimizedPlanet) => _planetProductionStatisticsToOptimizedPlanet.TryGetValue(productionStatistics, out optimizedPlanet);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.End))]
    public static void End()
    {
        WeaverFixes.Logger.LogInfo($"Marking optimized planets to be cleared.");
        // Set clear optimized planets in the specific case where a new save is created which circumvents
        // the logic in LoadCurrentGame_Postfix which is other wise supposed to do it.
        // Can not actually clear planets in here because this runs when exiting a game but before the
        // "last save played" auto save is run which would result in the games data not being updated
        // with the optimized information before the game is saved.
        _clearOptimizedPlanetsOnNextTick = true;
    }

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogInfo($"Initializing {nameof(OptimizedStarCluster)}");

        PrepareOptimizedStarClusterForGame();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            TryAddNewPlanet(GameMain.data.factories[i]);
        }

        _dysonSphereStatisticsManager.FindAllDysonSphereProductRegisters();
    }

    private static void PrepareOptimizedStarClusterForGame()
    {
        _clearOptimizedPlanetsOnNextTick = false;
        _firstUpdate = true;
        _planetToOptimizedPlanet.Clear();
        _planetProductionStatisticsToOptimizedPlanet.Clear();
        _dysonSphereStatisticsManager.ClearDysonSphereProductRegisters();
        _workStealingMultiThreadedFactorySimulation.Clear();
        KillStatisticsPatches.Clear();
        TrafficStatisticsPatches.Clear();
    }

    private static void TryAddNewPlanet(PlanetFactory planet)
    {
        if (planet.factorySystem == null)
        {
            return;
        }

        if (OptimizedGasPlanet.IsGasPlanet(planet))
        {
            var optimizedPlanet = new OptimizedGasPlanet(planet);
            _planetToOptimizedPlanet.Add(planet, optimizedPlanet);
            _planetProductionStatisticsToOptimizedPlanet.Add(GameMain.statistics.production.factoryStatPool[planet.index], optimizedPlanet);
        }
        else
        {
            var optimizedPlanet = new OptimizedTerrestrialPlanet(planet,
                                                                 _starClusterResearchManager,
                                                                 _dysonSphereManager);
            _planetToOptimizedPlanet.Add(planet, optimizedPlanet);
            _planetProductionStatisticsToOptimizedPlanet.Add(GameMain.statistics.production.factoryStatPool[planet.index], optimizedPlanet);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogInfo($"Saving optimized planets");

        foreach (KeyValuePair<PlanetFactory, IOptimizedPlanet> planetToOptimizedPlanet in _planetToOptimizedPlanet)
        {
            if (planetToOptimizedPlanet.Value.Status != OptimizedPlanetStatus.Running)
            {
                continue;
            }

            WeaverFixes.Logger.LogInfo($"Saving planet: {planetToOptimizedPlanet.Key.planet.displayName}");
            planetToOptimizedPlanet.Value.Save();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameThreadController), nameof(GameThreadController.LogicFrame))]
    public static bool GameThreadController_LogicFrame(GameThreadController __instance)
    {
        if (_clearOptimizedPlanetsOnNextTick)
        {
            PrepareOptimizedStarClusterForGame();
        }

        lock (_newPlanets)
        {
            if (_newPlanets.Count > 0)
            {
                while (_newPlanets.Count > 0)
                {
                    PlanetFactory newPlanet = _newPlanets.Dequeue();

                    WeaverFixes.Logger.LogInfo($"Adding planet: {newPlanet.planet.displayName}");
                    TryAddNewPlanet(newPlanet);
                }

                _dysonSphereStatisticsManager.FindAllDysonSphereProductRegisters();
            }
        }

        foreach (KeyValuePair<PlanetFactory, IOptimizedPlanet> planetToOptimizedPlanet in _planetToOptimizedPlanet)
        {
            if (GameMain.localPlanet?.factory != planetToOptimizedPlanet.Key ||
                ForceOptimizeLocalPlanet)
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

        if (_firstUpdate)
        {
            _firstUpdate = false;

            if (_enableStatistics)
            {
                _workStealingMultiThreadedFactorySimulation.PrintWorkStatistics();
            }
        }

        if (_planetsToReOptimize.Count > 0)
        {
            // Avoid lag spike by spreading load over multiple ticks
            PlanetFactory planetToReOptimize = _planetsToReOptimize.Dequeue();
            IOptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planetToReOptimize];
            if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
            {
                return HarmonyConstants.SKIP_ORIGINAL_METHOD;
            }

            WeaverFixes.Logger.LogInfo($"DeOptimizing planet: {planetToReOptimize.planet.displayName}");
            optimizedPlanet.Save();
        }

        if (_debugEnableHeavyReOptimization && GameMain.gameTick % 10 == 0)
        {
            PlanetFactory[] planets = _planetToOptimizedPlanet.Keys.ToArray();
            int randomPlanet = random.Next(0, planets.Length);
            _planetsToReOptimize.Enqueue(planets[randomPlanet]);
        }


        DeepProfiler.BeginSample(DPEntry.Scheduling, -1, 2L);
        __instance.EnsureThreadCount();
        __instance.threadManager.samplePerformanceCounters = DeepProfiler.watchEnabled;
        DeepProfiler.EndSample();

        //DeepProfiler.watchEnabled = true;
        ExecuteSimulation(__instance.gameLogic, GameMain.data.factories);
        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
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
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.RequestDysonSpherePower))]
    public static bool PowerSystem_RequestDysonSpherePower(PowerSystem __instance)
    {
        IOptimizedPlanet optimizedPlanet = GetOptimizedPlanet(__instance.factory);
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.RefreshPowerGenerationCapacitesWithFactory))]
    public static bool ProductionStatistics_RefreshPowerGenerationCapacitesWithFactory(ProductionStatistics __instance, PlanetFactory factory)
    {
        // Game code has this check for some reason
        if (factory == null)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        IOptimizedPlanet optimizedPlanet = GetOptimizedPlanet(factory);
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        // Nothing to do on gas planets
        if (optimizedPlanet is not OptimizedTerrestrialPlanet terrestrialPlanet)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        terrestrialPlanet.RefreshPowerGenerationCapacites(__instance, factory);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductionStatistics), nameof(ProductionStatistics.RefreshPowerConsumptionDemandsWithFactory))]
    public static bool ProductionStatistics_RefreshPowerConsumptionDemandsWithFactory(ProductionStatistics __instance, PlanetFactory factory)
    {
        // Game code has this check for some reason
        if (factory == null)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        IOptimizedPlanet optimizedPlanet = GetOptimizedPlanet(factory);
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        // Nothing to do on gas planets
        if (optimizedPlanet is not OptimizedTerrestrialPlanet terrestrialPlanet)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        terrestrialPlanet.RefreshPowerConsumptionDemands(__instance, factory);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactoryProductionStat), nameof(FactoryProductionStat.GameTick))]
    public static bool FactoryProductionStat_GameTick(FactoryProductionStat __instance, long time)
    {
        if (_dysonSphereStatisticsManager.IsDysonSphereStatistics(__instance))
        {
            _dysonSphereStatisticsManager.DysonSphereGameTick(__instance, time);
        }

        if (!TryGetOptimizedPlanet(__instance, out IOptimizedPlanet? optimizedPlanet))
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactoryProductionStat), nameof(FactoryProductionStat.ClearRegisters))]
    public static bool FactoryProductionStat_ClearRegisters(FactoryProductionStat __instance)
    {
        if (_dysonSphereStatisticsManager.IsDysonSphereStatistics(__instance))
        {
            _dysonSphereStatisticsManager.DysonSphereClearRegisters(__instance);
        }

        if (!TryGetOptimizedPlanet(__instance, out IOptimizedPlanet? optimizedPlanet))
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        if (optimizedPlanet.Status == OptimizedPlanetStatus.Stopped)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        __instance.powerGenRegister = 0L;
        __instance.powerConRegister = 0L;
        __instance.powerDisRegister = 0L;
        __instance.powerChaRegister = 0L;
        __instance.hashRegister = 0L;

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.BeginSample))]
    public static void DeepProfiler_BeginSample(DPEntry entry, ref int thread, long detail)
    {
        UpdateThreadIndexIfRequired(ref thread);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.EndSample), [typeof(int), typeof(long)])]
    public static void DeepProfiler_EndSampleNonTyped(ref int thread, long detail)
    {
        UpdateThreadIndexIfRequired(ref thread);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.EndSample), [typeof(DPEntry), typeof(int)])]
    public static void DeepProfiler_EndSampleTyped(DPEntry entry, ref int thread)
    {
        UpdateThreadIndexIfRequired(ref thread);
    }

    public static void ReOptimizeAllPlanets()
    {
        lock (_planetsToReOptimize)
        {
            foreach (PlanetFactory planetToReOptimize in _planetToOptimizedPlanet.Where(x => x.Value.Status == OptimizedPlanetStatus.Running)
                                                                     .Select(x => x.Key))
            {
                _planetsToReOptimize.Enqueue(planetToReOptimize);
            }
        }
    }

    private static void ExecuteSimulation(GameLogic gameLogic, PlanetFactory?[] planets)
    {
        _workStealingMultiThreadedFactorySimulation.Simulate(gameLogic, planets);
    }

    //private static void ExecuteParallelDefense()
    //{
    //    PerformanceMonitor.BeginSample(ECpuWorkEntry.Defense);
    //    long time = GameMain.gameTick;

    //    // Can only parallelize on a solar system level due to enemies in space.
    //    // Need to to defer all UI notifications to UI thread.
    //    //Parallel.ForEach(_planetToOptimizedPlanet.Values, x => x.GameTickDefense(time));


    //    foreach (IOptimizedPlanet optimizedPlanet in _planetToOptimizedPlanet.Values)
    //    {
    //        if (optimizedPlanet is not OptimizedTerrestrialPlanet terrestrialPlanet)
    //        {
    //            continue;
    //        }

    //        terrestrialPlanet.GameTickDefense(time);
    //        terrestrialPlanet.DefenseGameTickUIThread(time);
    //    }
    //    PerformanceMonitor.EndSample(ECpuWorkEntry.Defense);
    //}

    private static void DeOptimizeDueToNonPlayerAction(PlanetFactory planet)
    {
        IOptimizedPlanet optimizedPlanet = _planetToOptimizedPlanet[planet];
        if (optimizedPlanet.Status == OptimizedPlanetStatus.Running)
        {
            WeaverFixes.Logger.LogInfo($"DeOptimizing planet: {planet.planet.displayName}");
            optimizedPlanet.Save();
        }
        optimizedPlanet.OptimizeDelayInTicks = 200;
    }

    private static void UpdateThreadIndexIfRequired(ref int thread)
    {
        const int mainThreadIndex = -1;
        if (thread != mainThreadIndex)
        {
            return;
        }

        int? newThreadIndex = ThreadLocalData.ThreadIndex.Value;
        if (!newThreadIndex.HasValue)
        {
            return;
            //throw new InvalidOperationException("Thread index from thread local storage was null.");
        }

        thread = newThreadIndex.Value;
    }
}