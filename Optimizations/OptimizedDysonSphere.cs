using HarmonyLib;
using System.Collections.Generic;

namespace Weaver.Optimizations;

internal sealed class OptimizedDysonSphere
{
    private static readonly Dictionary<DysonSphere, OptimizedDysonSphere> _dysonSphereToOptimizedDysonSphere = [];
    private static readonly Dictionary<DysonSwarm, OptimizedDysonSphere> _dysonSwarmToOptimizedDysonSphere = [];
    private readonly DysonSphere _dysonSphere;
    private bool _needToRecalculatePower;

    public OptimizedDysonSphere(DysonSphere dysonSphere)
    {
        _dysonSphere = dysonSphere;
        _needToRecalculatePower = true;
    }

    public static void Reset()
    {
        _dysonSphereToOptimizedDysonSphere.Clear();
        _dysonSwarmToOptimizedDysonSphere.Clear();
        if (GameMain.data.dysonSpheres == null)
        {
            return;
        }

        foreach (DysonSphere? dysonSphere in GameMain.data.dysonSpheres)
        {
            if (dysonSphere == null)
            {
                continue;
            }

            var optimizedDysonSphere = new OptimizedDysonSphere(dysonSphere);
            _dysonSphereToOptimizedDysonSphere.Add(dysonSphere, optimizedDysonSphere);

            if (dysonSphere.swarm == null)
            {
                continue;
            }

            _dysonSwarmToOptimizedDysonSphere.Add(dysonSphere.swarm, optimizedDysonSphere);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.Init))]
    public static void DysonSphere_Init(DysonSphere __instance)
    {
        var optimizedDysonSphere = new OptimizedDysonSphere(__instance);
        _dysonSphereToOptimizedDysonSphere.Add(__instance, optimizedDysonSphere);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.Init))]
    public static void DysonSwarm_Init(DysonSwarm __instance)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance.dysonSphere);
        _dysonSwarmToOptimizedDysonSphere.Add(__instance, optimizedDysonSphere);
    }

    public static OptimizedDysonSphere GetOptimizedDysonSphere(DysonSphere dysonSphere)
    {
        return _dysonSphereToOptimizedDysonSphere[dysonSphere];
    }

    public static OptimizedDysonSphere GetOptimizedDysonSphere(DysonSwarm dysonSwarm)
    {
        return _dysonSwarmToOptimizedDysonSphere[dysonSwarm];
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.BeforeGameTick))]
    public static bool DysonSphere_BeforeGameTick(DysonSphere __instance)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance);
        if (optimizedDysonSphere.ShouldRecalculatePower())
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        // Ray receivers add how much energy they would like to use to this value before planets calculate power usage.
        // Then when planets start simulating they figure out how much energy they can actually use compared to how much
        // is available. That's why this needs to be reset every tick even though nothing else needs to be updated.
        __instance.energyReqCurrentTick = 0;
        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    internal void MarkNeedToRecalculatePower()
    {
        // Avoid writing to the same value from multiple threads when it has already been updated
        if (_needToRecalculatePower)
        {
            return;
        }

        _needToRecalculatePower = true;
    }

    internal bool ShouldRecalculatePower()
    {
        bool value = _needToRecalculatePower;
        _needToRecalculatePower = false;
        return value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RemoveLayer), [typeof(DysonSphereLayer)])]
    public static void DysonSphere_RemoveLayerDysonSphereLayer(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RemoveLayer), [typeof(int)])]
    public static void DysonSphere_RemoveLayerId(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.ConstructSp))]
    public static void DysonSphere_ConstructSp(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    // Not entire sure what this does so lets just invalidate when it is called, just to be sure
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.AddDysonNodeRData))]
    public static void DysonSphere_AddDysonNodeRData(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    // Not entire sure what this does so lets just invalidate when it is called, just to be sure
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RemoveDysonNodeRData))]
    public static void DysonSphere_RemoveDysonNodeRData(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.AutoConstruct))]
    public static void DysonSphere_AutoConstruct(DysonSphere __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddSolarSail))]
    public static void DysonSwarm_AddSolarSail(DysonSwarm __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.RemoveSolarSail))]
    public static void DysonSwarm_RemoveSolarSail(DysonSwarm __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AutoConstruct))]
    public static void DysonSwarm_AutoConstruct(DysonSwarm __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.RemoveSailsByOrbit))]
    public static void DysonSwarm_RemoveSailsByOrbit(DysonSwarm __instance)
    {
        GetOptimizedDysonSphere(__instance).MarkNeedToRecalculatePower();
    }
}