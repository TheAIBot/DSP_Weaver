using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Weaver.Optimizations.WorkDistributors;

namespace Weaver.Optimizations.Statistics;

internal static class KillStatisticsPatches
{
    private static bool?[]? _isStarUpdated = null;
    private static List<KillStat>[]? _starsStatistics = null;
    private static bool?[]? _isPlanetUpdated = null;
    private static List<KillStat>[]? _planetsStatistics = null;

    internal static void Clear()
    {
        _isStarUpdated = null;
        _starsStatistics = null;
        _isPlanetUpdated = null;
        _planetsStatistics = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.PrepareTick))]
    private static bool PrepareTick_Parallelize(KillStatistics __instance)
    {
        if (_isStarUpdated == null || __instance.starKillStatPool.Length != _isStarUpdated.Length)
        {
            _isStarUpdated = new bool?[__instance.starKillStatPool.Length];
            _starsStatistics = new List<KillStat>[__instance.starKillStatPool.Length];
            GetKillStatsForWholeStartCluster(__instance.starKillStatPool, _starsStatistics);
        }

        if (_isPlanetUpdated == null || __instance.factoryKillStatPool.Length != _isPlanetUpdated.Length)
        {
            _isPlanetUpdated = new bool?[__instance.factoryKillStatPool.Length];
            _planetsStatistics = new List<KillStat>[__instance.factoryKillStatPool.Length];
            GetKillStatsForWholeStartCluster(__instance.factoryKillStatPool, _planetsStatistics);
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    private static void GetKillStatsForWholeStartCluster(AstroKillStat?[] astroStatistics, List<KillStat>[] clusterStatistics)
    {
        for (int astroKillStartIndex = 0; astroKillStartIndex < astroStatistics.Length; astroKillStartIndex++)
        {
            List<KillStat> killStats = [];
            clusterStatistics[astroKillStartIndex] = killStats;

            AstroKillStat? astroKillStat = astroStatistics[astroKillStartIndex];
            if (astroKillStat == null)
            {
                continue;
            }

            for (int killStatIndex = 0; killStatIndex <= ModelProto.maxModelIndex; killStatIndex++)
            {
                if (astroKillStat.killStatPool[killStatIndex] == null)
                {
                    continue;
                }
                killStats.Add(astroKillStat.killStatPool[killStatIndex]);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.RegisterStarKillStat))]
    private static void RegisterStarKillStat(int starId)
    {
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
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.RegisterFactoryKillStat))]
    private static void RegisterFactoryKillStat(int factoryIndex)
    {
        if (_isPlanetUpdated == null)
        {
            return;
        }

        if (_isPlanetUpdated[factoryIndex] == true)
        {
            return;
        }

        _isPlanetUpdated[factoryIndex] = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.GameTick))]
    private static bool GameTick_Parallelize(KillStatistics __instance, long time)
    {
        bool?[]? isStarUpdated = _isStarUpdated;
        if (isStarUpdated == null)
        {
            throw new InvalidOperationException($"{nameof(isStarUpdated)} was null");
        }
        List<KillStat>[]? starsStatistics = _starsStatistics;
        if (starsStatistics == null)
        {
            throw new InvalidOperationException($"{nameof(starsStatistics)} was null");
        }

        bool?[]? isPlanetUpdated = _isPlanetUpdated;
        if (isPlanetUpdated == null)
        {
            throw new InvalidOperationException($"{nameof(isPlanetUpdated)} was null");
        }
        List<KillStat>[]? planetsStatistics = _planetsStatistics;
        if (planetsStatistics == null)
        {
            throw new InvalidOperationException($"{nameof(planetsStatistics)} was null");
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = WeaverThreadHelper.GetParallelism(),
        };

        Parallel.For(0, __instance.starKillStatPool.Length + __instance.factoryKillStatPool.Length, parallelOptions, i =>
        {
            if (i < __instance.starKillStatPool.Length)
            {
                KillStatGameTick(__instance.starKillStatPool, isStarUpdated, starsStatistics, i, time);
            }
            else
            {
                KillStatGameTick(__instance.factoryKillStatPool, isPlanetUpdated, planetsStatistics, i - __instance.starKillStatPool.Length, time);
            }
        });
        __instance.mechaKillStat?.GameTick(time);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    private static void KillStatGameTick(AstroKillStat[] astroStatistics, bool?[] isStatisticsUpdated, List<KillStat>[] statistics, int statisticsIndex, long time)
    {
        if (isStatisticsUpdated[statisticsIndex] == true || isStatisticsUpdated[statisticsIndex] == null)
        {
            GameTick(astroStatistics[statisticsIndex], statistics[statisticsIndex], time);
            isStatisticsUpdated[statisticsIndex] = false;
        }
        else
        {
            OptimizeGameTick(astroStatistics[statisticsIndex], statistics[statisticsIndex], time);
        }
    }

    private static void GameTick(AstroKillStat? astroKillStat, List<KillStat> killStats, long time)
    {
        if (astroKillStat == null)
        {
            return;
        }

        if (time % 1 == 0L)
        {
            int num = 0;
            int num2 = 6;
            int[] killRegister = astroKillStat.killRegister;
            for (int i = 0; i <= ModelProto.maxModelIndex; i++)
            {
                int num3 = killRegister[i];
                killRegister[i] = 0; // This is all the clear logic needed
                if (astroKillStat.killStatPool[i] == null)
                {
                    if (num3 == 0)
                    {
                        continue;
                    }
                    astroKillStat.killStatPool[i] = new KillStat();
                    astroKillStat.killStatPool[i].Init(i);
                    killStats.Add(astroKillStat.killStatPool[i]);
                }
                KillStat obj = astroKillStat.killStatPool[i];
                int[] count = obj.count;
                int[] cursor = obj.cursor;
                int[] total = obj.total;
                int num4 = cursor[num];
                int num5 = num3 - count[num4];
                count[num4] = num3;
                total[num] += num5;
                total[num2] += num3;
                cursor[num]++;
                if (cursor[num] >= 600)
                {
                    cursor[num] -= 600;
                }
            }
        }
        if (time % 6 == 0L)
        {
            int level = 1;
            astroKillStat.ComputeTheMiddleLevel(level);
        }
        if (time % 60 == 0L)
        {
            int level2 = 2;
            astroKillStat.ComputeTheMiddleLevel(level2);
        }
        if (time % 360 == 0L)
        {
            int level3 = 3;
            astroKillStat.ComputeTheMiddleLevel(level3);
        }
        if (time % 3600 == 0L)
        {
            int level4 = 4;
            astroKillStat.ComputeTheMiddleLevel(level4);
        }
        if (time % 36000 == 0L)
        {
            int level5 = 5;
            astroKillStat.ComputeTheMiddleLevel(level5);
        }
    }

    private static void OptimizeGameTick(AstroKillStat? astroKillStat, List<KillStat> killStats, long time)
    {
        if (astroKillStat == null)
        {
            return;
        }

        if (time % 1 == 0L)
        {
            int num = 0;
            int num2 = 6;
            for (int i = 0; i < killStats.Count; i++)
            {
                int num3 = 0;
                KillStat obj = killStats[i];
                int[] count = obj.count;
                int[] cursor = obj.cursor;
                int[] total = obj.total;
                int num4 = cursor[num];
                int num5 = num3 - count[num4];
                count[num4] = num3;
                total[num] += num5;
                total[num2] += num3;
                cursor[num]++;
                if (cursor[num] >= 600)
                {
                    cursor[num] -= 600;
                }
            }
        }
        if (time % 6 == 0L)
        {
            int level = 1;
            astroKillStat.ComputeTheMiddleLevel(level);
        }
        if (time % 60 == 0L)
        {
            int level2 = 2;
            astroKillStat.ComputeTheMiddleLevel(level2);
        }
        if (time % 360 == 0L)
        {
            int level3 = 3;
            astroKillStat.ComputeTheMiddleLevel(level3);
        }
        if (time % 3600 == 0L)
        {
            int level4 = 4;
            astroKillStat.ComputeTheMiddleLevel(level4);
        }
        if (time % 36000 == 0L)
        {
            int level5 = 5;
            astroKillStat.ComputeTheMiddleLevel(level5);
        }
    }
}
