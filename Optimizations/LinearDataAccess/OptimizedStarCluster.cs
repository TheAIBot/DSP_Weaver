using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Weaver.Optimizations.LinearDataAccess.Labs;
using Weaver.Optimizations.LinearDataAccess.WorkDistributors;

#nullable enable
namespace Weaver.Optimizations.LinearDataAccess;

internal static class OptimizedStarCluster
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedPlanet = [];
    private static readonly Queue<PlanetFactory> _newPlanets = [];
    private static readonly Queue<PlanetFactory> _planetsToReOptimize = [];
    public static readonly StarClusterResearchManager _starClusterResearchManager = new();
    private static readonly WorkStealingMultiThreadedFactorySimulation _workStealingMultiThreadedFactorySimulation = new(_starClusterResearchManager);
    private static bool _clearOptimizedPlanetsOnNextTick = false;

    public static void EnableOptimization(Harmony harmony)
    {
        harmony.PatchAll(typeof(OptimizedStarCluster));
    }

    public static OptimizedPlanet GetOptimizedPlanet(PlanetFactory planet) => _planetToOptimizedPlanet[planet];

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
        WeaverFixes.Logger.LogInfo($"Initializing {nameof(OptimizedPlanet)}");

        _planetToOptimizedPlanet.Clear();
        _workStealingMultiThreadedFactorySimulation.Clear();
        _clearOptimizedPlanetsOnNextTick = false;

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            _planetToOptimizedPlanet.Add(planet, new OptimizedPlanet(planet, _starClusterResearchManager));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogInfo($"Saving optimized planets");

        foreach (KeyValuePair<PlanetFactory, OptimizedPlanet> planetToOptimizedPlanet in _planetToOptimizedPlanet)
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
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    public static void GameData_GameTick()
    {
        if (_clearOptimizedPlanetsOnNextTick)
        {
            _clearOptimizedPlanetsOnNextTick = false;
            _planetToOptimizedPlanet.Clear();
            _workStealingMultiThreadedFactorySimulation.Clear();
        }

        lock (_newPlanets)
        {
            while (_newPlanets.Count > 0)
            {
                PlanetFactory newPlanet = _newPlanets.Dequeue();

                WeaverFixes.Logger.LogInfo($"Adding planet: {newPlanet.planet.displayName}");
                _planetToOptimizedPlanet.Add(newPlanet, new OptimizedPlanet(newPlanet, _starClusterResearchManager));
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

    [HarmonyTranspiler, HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabResearchMode))]
    static IEnumerable<CodeInstruction> DeferResearchUnlockToStarClusterResearchManager(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        CodeMatch[] startOfResearchCompletedBlockToMove = [
            new CodeMatch(OpCodes.Ldloc_1),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameHistoryData), nameof(GameHistoryData.techStates))),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(FactorySystem), nameof(FactorySystem.researchTechId))),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Dictionary<int, TechState>), "Item"))
        ];

        CodeMatch[] endOfResearchCompletedBlockToMove = [
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock)))
        ];

        CodeInstruction[] queueResearchCompleted = [
            // No idea why i need to add it but it gets deleted for some reason
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Dictionary<int, TechState>), "Item")),

            // _starClusterResearchManager.AddResearchedTech(researchTechId, curLevel, ts, techProto)
            new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(OptimizedStarCluster), nameof(OptimizedStarCluster._starClusterResearchManager))),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(FactorySystem), nameof(FactorySystem.researchTechId))),
            new CodeInstruction(OpCodes.Ldloc_S, 29), // curLevel
            new CodeInstruction(OpCodes.Ldloc_S, 13), // ts
            new CodeInstruction(OpCodes.Ldloc_S, 12), // techProto
            new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(StarClusterResearchManager), nameof(StarClusterResearchManager.AddResearchedTech)))
        ];

        codeMatcher.ReplaceCode(startOfResearchCompletedBlockToMove, false, endOfResearchCompletedBlockToMove, false, queueResearchCompleted);
        codeMatcher.ReplaceCode(startOfResearchCompletedBlockToMove, false, endOfResearchCompletedBlockToMove, false, queueResearchCompleted);

        return codeMatcher.InstructionEnumeration();
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
                   .ThrowIfNotMatch($"Failed to find {nameof(multithreadedIfCondition)}");
        codeMatcher.ReplaceCode(beginSamplePowerPerformanceMonitor, true, endSampleDigitalPerformanceMonitor, true, optimizedMultithreading);

        return codeMatcher.InstructionEnumeration();
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

    private static void ExecuteSimulation(PlanetFactory?[] planets)
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