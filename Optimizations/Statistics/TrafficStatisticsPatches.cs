using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public sealed class TrafficStatisticsPatches
{
    private static bool[] _isStarUpdated;
    private static bool[] _isPlanetUpdated;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    public static void PrepareTick(TrafficStatistics __instance, long time)
    {
        if (_isStarUpdated == null || __instance.starTrafficPool.Length != _isStarUpdated.Length)
        {
            _isStarUpdated = new bool[__instance.starTrafficPool.Length];
        }

        if (_isPlanetUpdated == null || __instance.factoryTrafficPool.Length != _isPlanetUpdated.Length)
        {
            _isPlanetUpdated = new bool[__instance.factoryTrafficPool.Length];
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.GameTick))]
    private static bool GameTick_Parallelize(TrafficStatistics __instance, long time)
    {
        //Logger.LogMessage("Did the thing! 3");

        // Only enable parallelization if multithreading is enabled.
        // Not sure why one would disable it but hey lets just support it!
        if (!GameMain.multithreadSystem.multithreadSystemEnable)
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        Parallel.For(0, __instance.starTrafficPool.Length, parallelOptions, i =>
        {
            if (!_isStarUpdated[i])
            {
                return;
            }
            _isStarUpdated[i] = false;

            AstroTrafficStat traffic = __instance.starTrafficPool[i];
            if (traffic == null)
            {
                return;
            }
            traffic.GameTick(time);
            if (traffic.itemChanged)
            {
                try
                {
                    __instance.RaiseActionEvent(nameof(TrafficStatistics.onItemChange));
                }
                catch (Exception message)
                {
                    // Error from original game code
                    WeaverFixes.Logger.LogError(message);
                }
            }
        });
        Parallel.For(0, __instance.factoryTrafficPool.Length, parallelOptions, i =>
        {
            if (!_isPlanetUpdated[i])
            {
                return;
            }
            _isPlanetUpdated[i] = false;

            AstroTrafficStat traffic = __instance.factoryTrafficPool[i];
            if (traffic == null)
            {
                return;
            }
            traffic.GameTick(time);
            if (traffic.itemChanged)
            {
                try
                {
                    __instance.RaiseActionEvent(nameof(TrafficStatistics.onItemChange));
                }
                catch (Exception message2)
                {
                    // Error from original game code
                    WeaverFixes.Logger.LogError(message2);
                }
            }
        });

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarInputStat))]
    public static void RegisterStarInputStat(int starId, int itemId, int count)
    {
        if (starId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        if (_isStarUpdated != null)
        {
            _isStarUpdated[starId] = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarOutputStat))]
    public static void RegisterStarOutputStat(int starId, int itemId, int count)
    {
        if (starId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        if (_isStarUpdated != null)
        {
            _isStarUpdated[starId] = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarInternalStat))]
    public static void RegisterStarInternalStat(int starId, int itemId, int count)
    {
        if (starId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        if (_isStarUpdated != null)
        {
            _isStarUpdated[starId] = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetInputStat))]
    public static void RegisterPlanetInputStat(TrafficStatistics __instance, int planetId, int itemId, int count)
    {
        if (planetId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        PlanetFactory planetFactory = __instance.gameData.galaxy.PlanetById(planetId)?.factory;
        if (planetFactory != null && _isPlanetUpdated != null)
        {
            _isPlanetUpdated[planetFactory.index] = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetOutputStat))]
    public static void RegisterPlanetOutputStat(TrafficStatistics __instance, int planetId, int itemId, int count)
    {
        if (planetId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        PlanetFactory planetFactory = __instance.gameData.galaxy.PlanetById(planetId)?.factory;
        if (planetFactory != null && _isPlanetUpdated != null)
        {
            _isPlanetUpdated[planetFactory.index] = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetInternalStat))]
    public static void RegisterPlanetInternalStat(TrafficStatistics __instance, int planetId, int itemId, int count)
    {
        if (planetId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        PlanetFactory planetFactory = __instance.gameData.galaxy.PlanetById(planetId)?.factory;
        if (planetFactory != null && _isPlanetUpdated != null)
        {
            _isPlanetUpdated[planetFactory.index] = true;
        }
    }
}
