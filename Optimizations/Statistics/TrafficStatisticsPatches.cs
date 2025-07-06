using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Weaver.Optimizations.Statistics;

public sealed class TrafficStatisticsPatches
{
    private static bool?[]? _isStarUpdated = null;
    private static List<TrafficStat>[]? _starsStatistics = null;
    private static bool?[]? _isPlanetUpdated = null;
    private static List<TrafficStat>[]? _planetsStatistics = null;

    internal static void Clear()
    {
        _isStarUpdated = null;
        _starsStatistics = null;
        _isPlanetUpdated = null;
        _planetsStatistics = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.PrepareTick))]
    public static void PrepareTick(TrafficStatistics __instance, long time)
    {
        if (_isStarUpdated == null || __instance.starTrafficPool.Length != _isStarUpdated.Length)
        {
            _isStarUpdated = new bool?[__instance.starTrafficPool.Length];
            _starsStatistics = new List<TrafficStat>[__instance.starTrafficPool.Length];
            GetTrafficStatsForWholeStartCluster(__instance.starTrafficPool, _starsStatistics);
        }

        if (_isPlanetUpdated == null || __instance.factoryTrafficPool.Length != _isPlanetUpdated.Length)
        {
            _isPlanetUpdated = new bool?[__instance.factoryTrafficPool.Length];
            _planetsStatistics = new List<TrafficStat>[__instance.factoryTrafficPool.Length];
            GetTrafficStatsForWholeStartCluster(__instance.factoryTrafficPool, _planetsStatistics);
        }
    }

    private static void GetTrafficStatsForWholeStartCluster(AstroTrafficStat?[] astroStatistics, List<TrafficStat>[] clusterStatistics)
    {
        for (int astroTrafficStartIndex = 0; astroTrafficStartIndex < astroStatistics.Length; astroTrafficStartIndex++)
        {
            List<TrafficStat> trafficStats = [];
            clusterStatistics[astroTrafficStartIndex] = trafficStats;

            AstroTrafficStat? astroTrafficStat = astroStatistics[astroTrafficStartIndex];
            if (astroTrafficStat == null)
            {
                continue;
            }

            for (int trafficStatIndex = 0; trafficStatIndex < astroTrafficStat.trafficPool.Length; trafficStatIndex++)
            {
                if (astroTrafficStat.trafficPool[trafficStatIndex] == null)
                {
                    continue;
                }
                trafficStats.Add(astroTrafficStat.trafficPool[trafficStatIndex]);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarInputStat))]
    private static void RegisterStarInputStat(int starId, int itemId, int count)
    {
        StarTrafficStatisticsUpdated(starId, itemId, count);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarOutputStat))]
    private static void RegisterStarOutputStat(int starId, int itemId, int count)
    {
        StarTrafficStatisticsUpdated(starId, itemId, count);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterStarInternalStat))]
    private static void RegisterStarInternalStat(int starId, int itemId, int count)
    {
        StarTrafficStatisticsUpdated(starId, itemId, count);
    }

    private static void StarTrafficStatisticsUpdated(int starId, int itemId, int count)
    {
        if (starId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        if (_isStarUpdated == null)
        {
            return;
        }

        if (_isStarUpdated[starId] == true)
        {
            return;
        }

        _isStarUpdated[starId] = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetInputStat))]
    private static void RegisterPlanetInputStat(int planetId, int itemId, int count)
    {
        PlanetTrafficStatisticsUpdated(planetId, itemId, count);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetOutputStat))]
    private static void RegisterPlanetOutputStat(int planetId, int itemId, int count)
    {
        PlanetTrafficStatisticsUpdated(planetId, itemId, count);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrafficStatistics), nameof(TrafficStatistics.RegisterPlanetInternalStat))]
    private static void RegisterPlanetInternalStat(int planetId, int itemId, int count)
    {
        PlanetTrafficStatisticsUpdated(planetId, itemId, count);
    }

    private static void PlanetTrafficStatisticsUpdated(int planetId, int itemId, int count)
    {
        if (planetId <= 0 || itemId <= 0 || count <= 0)
        {
            return;
        }

        if (_isPlanetUpdated == null)
        {
            return;
        }

        PlanetFactory? planet = GameMain.data.galaxy.PlanetById(planetId)?.factory;
        if (planet == null)
        {
            return;
        }
        int planetIndex = planet.index;

        if (_isPlanetUpdated[planetIndex] == true)
        {
            return;
        }

        _isPlanetUpdated[planetIndex] = true;
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

        if (!IsTimeToUpdateStatistics(time))
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }

        bool?[]? isStarUpdated = _isStarUpdated;
        if (isStarUpdated == null)
        {
            throw new InvalidOperationException($"{nameof(isStarUpdated)} was null");
        }
        List<TrafficStat>[]? starsStatistics = _starsStatistics;
        if (starsStatistics == null)
        {
            throw new InvalidOperationException($"{nameof(starsStatistics)} was null");
        }

        bool?[]? isPlanetUpdated = _isPlanetUpdated;
        if (isPlanetUpdated == null)
        {
            throw new InvalidOperationException($"{nameof(isPlanetUpdated)} was null");
        }
        List<TrafficStat>[]? planetsStatistics = _planetsStatistics;
        if (planetsStatistics == null)
        {
            throw new InvalidOperationException($"{nameof(planetsStatistics)} was null");
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = GameMain.multithreadSystem.usedThreadCnt,
        };

        Parallel.For(0, __instance.starTrafficPool.Length + __instance.factoryTrafficPool.Length, parallelOptions, i =>
        {
            if (i < __instance.starTrafficPool.Length)
            {
                TrafficStatGameTick(__instance, __instance.starTrafficPool, isStarUpdated, starsStatistics, i, time);
            }
            else
            {
                TrafficStatGameTick(__instance, __instance.factoryTrafficPool, isPlanetUpdated, planetsStatistics, i - __instance.starTrafficPool.Length, time);
            }
        });

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    // From AstroTrafficStat.GameTick
    private static bool IsTimeToUpdateStatistics(long time)
    {
        if (time % 10 == 0L)
        {
            return true;
        }
        if (time % 60 == 6)
        {
            return true;
        }
        if (time % 600 == 60)
        {
            return true;
        }
        if (time % 3600 == 360)
        {
            return true;
        }
        if (time % 36000 == 3600)
        {
            return true;
        }
        if (time % 360000 == 36000)
        {
            return true;
        }

        return false;
    }

    private static void TrafficStatGameTick(TrafficStatistics trafficStatistics, AstroTrafficStat[] astroStatistics, bool?[] isStatisticsUpdated, List<TrafficStat>[] statistics, int statisticsIndex, long time)
    {
        AstroTrafficStat traffic = astroStatistics[statisticsIndex];
        if (traffic == null)
        {
            return;
        }

        if (isStatisticsUpdated[statisticsIndex] == true || isStatisticsUpdated[statisticsIndex] == null)
        {
            GameTick(traffic, statistics[statisticsIndex], time);
            isStatisticsUpdated[statisticsIndex] = false;
        }
        else
        {
            OptimizeGameTick(traffic, statistics[statisticsIndex], time);
        }

        if (traffic.itemChanged)
        {
            try
            {
                trafficStatistics.RaiseActionEvent(nameof(TrafficStatistics.onItemChange));
            }
            catch (Exception message)
            {
                // Error from original game code
                WeaverFixes.Logger.LogError(message);
            }
        }
    }

    private static void GameTick(AstroTrafficStat astroTrafficStat, List<TrafficStat> trafficStats, long time)
    {
        if (time % 10 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int num3 = 6 + num;
            int num4 = 7 + num;
            int num5 = 13;
            int num6 = 420;
            int num7 = 12 + num;
            int num8 = 14 + num;
            int num9 = 20;
            int num10 = 780;
            int[] itemIds = ItemProto.itemIds;
            int num11 = itemIds.Length;
            int[] inputRegister = astroTrafficStat.inputRegister;
            int[] outputRegister = astroTrafficStat.outputRegister;
            int[] internalRegister = astroTrafficStat.internalRegister;
            for (int i = 0; i < num11; i++)
            {
                int num12 = itemIds[i];
                int num13 = num12;
                int num14 = inputRegister[num12];
                int num15 = outputRegister[num12];
                int num16 = internalRegister[num12];
                // This is all the clear logic that is needed
                inputRegister[num12] = 0;
                outputRegister[num12] = 0;
                internalRegister[num12] = 0;
                int num17 = astroTrafficStat.itemIndices[num13];
                if (num17 <= 0)
                {
                    if (num14 <= 0 && num15 <= 0 && num16 <= 0)
                    {
                        continue;
                    }
                    int num18 = astroTrafficStat.trafficCursor;
                    astroTrafficStat.CreateTrafficStat(num13);
                    astroTrafficStat.itemIndices[num13] = num18;
                    num17 = num18;
                    trafficStats.Add(astroTrafficStat.trafficPool[num17]);
                }
                TrafficStat obj = astroTrafficStat.trafficPool[num17];
                int[] count = obj.count;
                int[] cursor = obj.cursor;
                long[] total = obj.total;
                int num19 = cursor[num];
                int num20 = num14 - count[num19];
                count[num19] = num14;
                total[num] += num20;
                total[num2] += num14;
                cursor[num]++;
                if (cursor[num] >= 60)
                {
                    cursor[num] -= 60;
                }
                int num21 = cursor[num3];
                int num22 = num15 - count[num21];
                count[num21] = num15;
                total[num4] += num22;
                total[num5] += num15;
                cursor[num3]++;
                if (cursor[num3] >= num6)
                {
                    cursor[num3] -= 60;
                }
                int num23 = cursor[num7];
                int num24 = num16 - count[num23];
                count[num23] = num16;
                total[num8] += num24;
                total[num9] += num16;
                cursor[num7]++;
                if (cursor[num7] >= num10)
                {
                    cursor[num7] -= 60;
                }
            }
        }
        if (time % 60 == 6)
        {
            int level = 1;
            astroTrafficStat.ComputeTheMiddleLevel(level);
        }
        if (time % 600 == 60)
        {
            int level2 = 2;
            astroTrafficStat.ComputeTheMiddleLevel(level2);
        }
        if (time % 3600 == 360)
        {
            int level3 = 3;
            astroTrafficStat.ComputeTheMiddleLevel(level3);
        }
        if (time % 36000 == 3600)
        {
            int level4 = 4;
            astroTrafficStat.ComputeTheMiddleLevel(level4);
        }
        if (time % 360000 == 36000)
        {
            int level5 = 5;
            astroTrafficStat.ComputeTheMiddleLevel(level5);
        }
    }

    private static void OptimizeGameTick(AstroTrafficStat astroTrafficStat, List<TrafficStat> trafficStats, long time)
    {
        if (time % 10 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int num3 = 6 + num;
            int num4 = 7 + num;
            int num5 = 13;
            int num6 = 420;
            int num7 = 12 + num;
            int num8 = 14 + num;
            int num9 = 20;
            int num10 = 780;
            for (int i = 0; i < trafficStats.Count; i++)
            {
                int num14 = 0;
                int num15 = 0;
                int num16 = 0;
                TrafficStat obj = trafficStats[i];
                int[] count = obj.count;
                int[] cursor = obj.cursor;
                long[] total = obj.total;
                int num19 = cursor[num];
                int num20 = num14 - count[num19];
                count[num19] = num14;
                total[num] += num20;
                total[num2] += num14;
                cursor[num]++;
                if (cursor[num] >= 60)
                {
                    cursor[num] -= 60;
                }
                int num21 = cursor[num3];
                int num22 = num15 - count[num21];
                count[num21] = num15;
                total[num4] += num22;
                total[num5] += num15;
                cursor[num3]++;
                if (cursor[num3] >= num6)
                {
                    cursor[num3] -= 60;
                }
                int num23 = cursor[num7];
                int num24 = num16 - count[num23];
                count[num23] = num16;
                total[num8] += num24;
                total[num9] += num16;
                cursor[num7]++;
                if (cursor[num7] >= num10)
                {
                    cursor[num7] -= 60;
                }
            }
        }
        if (time % 60 == 6)
        {
            int level = 1;
            astroTrafficStat.ComputeTheMiddleLevel(level);
        }
        if (time % 600 == 60)
        {
            int level2 = 2;
            astroTrafficStat.ComputeTheMiddleLevel(level2);
        }
        if (time % 3600 == 360)
        {
            int level3 = 3;
            astroTrafficStat.ComputeTheMiddleLevel(level3);
        }
        if (time % 36000 == 3600)
        {
            int level4 = 4;
            astroTrafficStat.ComputeTheMiddleLevel(level4);
        }
        if (time % 360000 == 36000)
        {
            int level5 = 5;
            astroTrafficStat.ComputeTheMiddleLevel(level5);
        }
    }
}
