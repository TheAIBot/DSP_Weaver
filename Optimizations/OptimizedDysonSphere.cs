using HarmonyLib;
using System.Collections.Generic;
using System.Threading;
using static UnityEngine.PostProcessing.MotionBlurComponent.FrameBlendingFilter;

namespace Weaver.Optimizations;

internal sealed class OptimizedDysonSphere
{
    private static readonly Dictionary<DysonSphere, OptimizedDysonSphere> _dysonSphereToOptimizedDysonSphere = [];
    private static readonly Dictionary<DysonSwarm, OptimizedDysonSphere> _dysonSwarmToOptimizedDysonSphere = [];
    private readonly DysonSphere _dysonSphere;
    private readonly long[] _dysonSphereLayersPowerGenerated;
    private bool _needToRecalculatePower;

    public OptimizedDysonSphere(DysonSphere dysonSphere)
    {
        _dysonSphere = dysonSphere;
        _dysonSphereLayersPowerGenerated = new long[dysonSphere.layersSorted.Length];
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.Init))]
    public static void DysonSphere_Init(DysonSphere __instance)
    {
        var optimizedDysonSphere = new OptimizedDysonSphere(__instance);
        _dysonSphereToOptimizedDysonSphere.Add(__instance, optimizedDysonSphere);
        _dysonSwarmToOptimizedDysonSphere.Add(__instance.swarm, optimizedDysonSphere);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.ConstructSp))]
    public static bool DysonSphere_ConstructSp(DysonSphere __instance, DysonNode node)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance);

        object obj = node.ConstructSp();
        if (obj == null)
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }
        if (obj is DysonNode dysonNode)
        {
            __instance.UpdateProgress(dysonNode);
            optimizedDysonSphere.AddDysonNodeToPowerGenerated(dysonNode.layerId - 1);
        }
        else if (obj is DysonFrame dysonFrame)
        {
            __instance.UpdateProgress(dysonFrame);
            optimizedDysonSphere.AddDysonFrameToPowerGenerated(dysonFrame.layerId - 1);
        }
        int[] array = __instance.productRegister;
        if (array != null)
        {
            lock (array)
            {
                array[ProductionStatistics.DYSON_STRUCTURE_ID]++;
            }
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonShell), nameof(DysonShell.Construct))]
    public static void DysonShell_Construct(DysonShell __instance)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance.dysonSphere);
        optimizedDysonSphere.AddDysonShellToPowerGenerated(__instance.layerId - 1);
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
    public static bool DysonSphere_BeforeGameTick_Prefix(DysonSphere __instance)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance);
        if (optimizedDysonSphere.ShouldRecalculatePower())
        {
            return HarmonyConstants.EXECUTE_ORIGINAL_METHOD;
        }

        optimizedDysonSphere.OptimizedBeforeGameTick();
        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.BeforeGameTick))]
    public static void DysonSphere_BeforeGameTick_Postfix(DysonSphere __instance)
    {
        OptimizedDysonSphere optimizedDysonSphere = GetOptimizedDysonSphere(__instance);
        if (!optimizedDysonSphere.ShouldRecalculatePower())
        {
            return;
        }

        optimizedDysonSphere.ResetPowerGenerated();
    }

    public void OptimizedBeforeGameTick()
    {
        _dysonSphere.energyReqCurrentTick = 0L;
        _dysonSphere.energyGenCurrentTick = 0L;
        _dysonSphere.energyGenOriginalCurrentTick = 0L;
        _dysonSphere.swarm.energyGenCurrentTick = _dysonSphere.swarm.sailCount * _dysonSphere.energyGenPerSail;
        _dysonSphere.energyGenCurrentTick += _dysonSphere.swarm.energyGenCurrentTick;
        _dysonSphere.grossRadius = _dysonSphere.swarm.grossRadius;

        DeepProfiler.BeginSample(DPEntry.DysonShell);
        long[] dysonSphereLayersPowerGenerated = _dysonSphereLayersPowerGenerated;
        for (int i = 0; i < dysonSphereLayersPowerGenerated.Length; i++)
        {
            DysonSphereLayer dysonSphereLayer = _dysonSphere.layersSorted[i];
            if (dysonSphereLayer == null)
            {
                continue;
            }

            if (dysonSphereLayer.grossRadius > _dysonSphere.grossRadius)
            {
                _dysonSphere.grossRadius = dysonSphereLayer.grossRadius;
            }

            dysonSphereLayer.energyGenCurrentTick = dysonSphereLayersPowerGenerated[i];
            _dysonSphere.energyGenCurrentTick += dysonSphereLayersPowerGenerated[i];
        }
        DeepProfiler.EndSample(DPEntry.DysonShell);

        _dysonSphere.energyGenOriginalCurrentTick = _dysonSphere.energyGenCurrentTick;
        _dysonSphere.energyGenCurrentTick = (long)((double)_dysonSphere.energyGenCurrentTick * _dysonSphere.energyDFHivesDebuffCoef);
    }

    public void AddDysonNodeToPowerGenerated(int layerIndex)
    {
        Interlocked.Add(ref _dysonSphereLayersPowerGenerated[layerIndex], _dysonSphere.energyGenPerNode);
    }

    public void AddDysonFrameToPowerGenerated(int layerIndex)
    {
        Interlocked.Add(ref _dysonSphereLayersPowerGenerated[layerIndex], _dysonSphere.energyGenPerFrame);
    }

    public void AddDysonShellToPowerGenerated(int layerIndex)
    {
        Interlocked.Add(ref _dysonSphereLayersPowerGenerated[layerIndex], _dysonSphere.energyGenPerShell);
    }

    public void ResetPowerGenerated()
    {
        for (int i = 0; i < _dysonSphere.layersSorted.Length; i++)
        {
            DysonSphereLayer dysonSphereLayer = _dysonSphere.layersSorted[i];
            if (dysonSphereLayer == null)
            {
                _dysonSphereLayersPowerGenerated[i] = 0;
                continue;
            }

            _dysonSphereLayersPowerGenerated[i] = dysonSphereLayer.energyGenCurrentTick;
        }

        _needToRecalculatePower = false;
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
        return _needToRecalculatePower;
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