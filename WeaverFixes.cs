using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;



using Weaver.Optimizations;
using Weaver.Optimizations.Statistics;

namespace Weaver;

internal static class ModInfo
{
    public const string Guid = "Weaver";
    public const string Name = "Weaver";
    public const string Version = "2.1.1";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
public class WeaverFixes : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(ModInfo.Name);
    private const string _ModCompatibilityConfigName = "WarnAboutModIncompatibility";
    private static readonly bool _enableDebugOptions = false;
    private bool _firstUpdate = true;
    private bool _warnAbountModIncompatibility;

    private static readonly ModIncompatibility[] _incompatibleMods = [
        // https://thunderstore.io/c/dyson-sphere-program/p/HiddenCirno/GenesisBook/
        new ModIncompatibility("GenesisBook",
                               "org.LoShin.GenesisBook", 
                               IncompatibilityDegree.CompletelyIncompatible,
                               null,
                               "Patches base game code which Weaver never calls."),

        // https://thunderstore.io/c/dyson-sphere-program/p/blacksnipebiu/Multfuntion_mod/
        new ModIncompatibility("Multfuntion mod",
                               "cn.blacksnipe.dsp.Multfuntion_mod",
                               IncompatibilityDegree.PartiallyCompatible,
                               "Some functionality may not work.",
                               "Most patches are for logic that weaver never calls."),

        // https://thunderstore.io/c/dyson-sphere-program/p/jinxOAO/MoreMegaStructure/
        new ModIncompatibility("MoreMegaStructure",
                               "Gnimaerd.DSP.plugin.MoreMegaStructure",
                               IncompatibilityDegree.PartiallyCompatible,
                               "Mega structures do not work unless player is present on planet.",
                               "Patches base game code which Weaver never calls."),

        // https://thunderstore.io/c/dyson-sphere-program/p/Eirshy/VeinityProject/
        new ModIncompatibility("VeinityProject",
                               "eirshy.dsp.VeinityProject",
                               IncompatibilityDegree.CompletelyIncompatible,
                               null,
                               "Patches base game code which Weaver never calls."),

        // https://thunderstore.io/c/dyson-sphere-program/p/blacksnipebiu/PlanetMiner/
        new ModIncompatibility("PlanetMiner",
                               "crecheng.PlanetMiner",
                               IncompatibilityDegree.CompletelyIncompatible,
                               null,
                               "It depends on the research lab update which weaver never calls for optimized planets.")
    ];

    private void Awake()
    {
        _warnAbountModIncompatibility = Config.Bind(ModInfo.Guid, _ModCompatibilityConfigName, true, "Displays incompatible mods, if any, on game launch.").Value;

        var harmony = new Harmony(ModInfo.Guid);
        // These changes parallelize calculating statistics
        //harmony.PatchAll(typeof(ProductionStatisticsPatches));
        harmony.PatchAll(typeof(KillStatisticsPatches));
        harmony.PatchAll(typeof(TrafficStatisticsPatches));
        //harmony.PatchAll(typeof(CustomChartsPatches));


        OptimizedStarCluster.EnableOptimization(harmony);
        //OptimizedStarCluster.DebugEnableHeavyReOptimization();
        //OptimizedStarCluster.EnableStatistics();
        //GraphStatistics.Enable(harmony);
        //GameStatistics.MemoryStatistics.EnableGameStatistics(harmony);
    }

    //private static bool _isOptimizeLocalPlanetKeyDown = false;
    //private static bool _viewBeltsOnLocalOptimizedPlanet = false;
    //private static bool _viewEntityIdsOnLocalPlanet = false;

    public void Update()
    {
        if (_firstUpdate)
        {
            _firstUpdate = false;

            if (_warnAbountModIncompatibility)
            {
                CheckForAndWarnAboutIncompatibleMods();
            }
        }
    }

    private static void CheckForAndWarnAboutIncompatibleMods()
    {

        ModIncompatibility[] presentIncompatibleMods = _incompatibleMods.Where(x => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(x.ModGuid))
                                                                        .ToArray();
        if (presentIncompatibleMods.Length == 0)
        {
            return;
        }

        StringBuilder warningMessage = new StringBuilder();
        ModIncompatibility[] fullyIncompatible = presentIncompatibleMods.Where(x => x.IncompatibilityDegree == IncompatibilityDegree.CompletelyIncompatible)
                                                                        .ToArray();
        if (fullyIncompatible.Length > 0)
        {
            warningMessage.AppendLine("The following mods are not compatible with Weaver:");
            foreach (var mod in fullyIncompatible)
            {
                warningMessage.AppendLine($"\t{mod.ModName}");
            }
        }
        ModIncompatibility[] partiallyIncompatible = presentIncompatibleMods.Where(x => x.IncompatibilityDegree == IncompatibilityDegree.PartiallyCompatible)
                                                                            .ToArray();
        if (partiallyIncompatible.Length > 0)
        {
            if (fullyIncompatible.Length != 0)
            {
                warningMessage.AppendLine();
            }

            warningMessage.AppendLine("The following mods are partially incompatible. Some functionality will not work with Weaver.");
            foreach (var mod in partiallyIncompatible)
            {
                warningMessage.AppendLine($"\t{mod.ModName}");
            }
        }

        warningMessage.AppendLine();
        warningMessage.AppendLine("This warning can be disabled by setting the Weaver");
        warningMessage.AppendLine($"config {_ModCompatibilityConfigName} to false.");

        var messageBox = UIMessageBox.Show("Incompatible mods", warningMessage.ToString(), "Close", 0);

        // Allow the message box to take up as much space as necessary to display the text
        messageBox.m_MessageText.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
    }

    //public void Update()
    //{
    //    if (!_enableDebugOptions)
    //    {
    //        return;
    //    }

    //    if (Input.GetKeyDown(KeyCode.V) && !_isOptimizeLocalPlanetKeyDown)
    //    {
    //        OptimizedStarCluster.ForceOptimizeLocalPlanet = !OptimizedStarCluster.ForceOptimizeLocalPlanet;
    //        _isOptimizeLocalPlanetKeyDown = true;
    //    }
    //    else
    //    {
    //        _isOptimizeLocalPlanetKeyDown = false;
    //    }

    //    if (Input.GetKeyDown(KeyCode.B) && !_viewBeltsOnLocalOptimizedPlanet)
    //    {
    //        OptimizedTerrestrialPlanet.ViewBeltsOnLocalOptimizedPlanet = !OptimizedTerrestrialPlanet.ViewBeltsOnLocalOptimizedPlanet;
    //        _viewBeltsOnLocalOptimizedPlanet = true;
    //    }
    //    else
    //    {
    //        _viewBeltsOnLocalOptimizedPlanet = false;
    //    }

    //    if (Input.GetKeyDown(KeyCode.F10) && !_viewEntityIdsOnLocalPlanet)
    //    {
    //        _viewEntityIdsOnLocalPlanet = true;
    //    }
    //    else
    //    {
    //        _viewEntityIdsOnLocalPlanet = false;
    //    }
    //}

    //// from TestConstructSystem
    //private void OnGUI()
    //{
    //    if (!_enableDebugOptions)
    //    {
    //        return;
    //    }

    //    GameData data = GameMain.data;
    //    if (data == null)
    //    {
    //        return;
    //    }
    //    PlanetFactory? planetFactory = ((data.localPlanet == null) ? null : data.localPlanet.factory);
    //    if (planetFactory == null)
    //    {
    //        return;
    //    }
    //    if (_viewEntityIdsOnLocalPlanet)
    //    {
    //        EntityData[] entityPool = planetFactory.entityPool;
    //        int entityCursor = planetFactory.entityCursor;
    //        for (int i = 1; i < entityCursor; i++)
    //        {
    //            if (entityPool[i].id == i)
    //            {
    //                Vector3 vector = Camera.main.WorldToScreenPoint(entityPool[i].pos);
    //                vector.y = (float)Screen.height - vector.y;
    //                GUI.Label(new Rect(vector.x, vector.y - 50f, 270f, 450f), i.ToString());
    //            }
    //        }
    //    }
    //}

    private record struct ModIncompatibility(string ModName,
                                             string ModGuid,
                                             IncompatibilityDegree IncompatibilityDegree, 
                                             string? IncompatibilityDescription, 
                                             string TechnicalIncompatibilityDescription);

    private enum IncompatibilityDegree
    {
        PartiallyCompatible,
        CompletelyIncompatible
    }
}
