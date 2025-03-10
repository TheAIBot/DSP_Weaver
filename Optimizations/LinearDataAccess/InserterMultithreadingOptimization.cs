using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

public class LinearInserterDataAccessOptimization
{
    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(LinearInserterDataAccessOptimization));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            CompactInserters(planet, factory);
            InserterLinearAccessToAssemblers(planet, factory);
        }
    }

    private static void CompactInserters(PlanetFactory planet, FactorySystem factory)
    {
        List<Graph> graphs = Graphifier.ToInserterGraphs(factory);
        if (graphs.Count == 0)
        {
            return;
        }

        Graphifier.CombineSmallGraphs(graphs);

        InserterComponent[] oldInserters = factory.inserterPool;
        List<InserterComponent> newInserters = [];
        newInserters.Add(new InserterComponent() { id = 0 });

        foreach (Graph graph in graphs)
        {
            foreach (Node inserterNode in graph.GetAllNodes()
                                               .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                               .OrderBy(x => (int)x.EntityTypeIndex.EntityType)
                                               .ThenBy(x => x.EntityId))
            {
                InserterComponent inserterCopy = oldInserters[inserterNode.EntityTypeIndex.Index];
                inserterCopy.id = newInserters.Count;
                planet.entityPool[inserterCopy.entityId].inserterId = inserterCopy.id;
                newInserters.Add(inserterCopy);
            }
        }

        factory.SetInserterCapacity(newInserters.Count);
        newInserters.CopyTo(factory.inserterPool);
        factory.inserterCursor = factory.inserterPool.Length;
        factory.inserterRecycleCursor = 0;

    }

    private static void InserterLinearAccessToAssemblers(PlanetFactory planet, FactorySystem factory)
    {
        List<Graph> graphs = Graphifier.ToInserterGraphs(factory);

        AssemblerComponent[] oldAssemblers = factory.assemblerPool;
        List<AssemblerComponent> newAssemblers = [];
        newAssemblers.Add(new AssemblerComponent() { id = 0 });
        HashSet<int> seenAssemblerIDs = [];

        foreach (var inserterNode in graphs.SelectMany(x => x.GetAllNodes())
                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Inserter)
                                           .OrderBy(x => x.EntityTypeIndex.Index))
        {
            ref readonly InserterComponent inserter = ref factory.inserterPool[inserterNode.EntityTypeIndex.Index];
            if (inserter.pickTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.pickTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                if (!seenAssemblerIDs.Add(inserter.pickTarget))
                {
                    continue;
                }

                AssemblerComponent assemblerCopy = oldAssemblers[oldAssemblerId];
                assemblerCopy.id = newAssemblers.Count;
                planet.entityPool[inserter.pickTarget].assemblerId = assemblerCopy.id;
                newAssemblers.Add(assemblerCopy);
            }

            if (inserter.insertTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.insertTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                if (!seenAssemblerIDs.Add(inserter.insertTarget))
                {
                    continue;
                }

                AssemblerComponent assemblerCopy = oldAssemblers[oldAssemblerId];
                assemblerCopy.id = newAssemblers.Count;
                planet.entityPool[inserter.insertTarget].assemblerId = assemblerCopy.id;
                newAssemblers.Add(assemblerCopy);
            }
        }

        factory.SetAssemblerCapacity(newAssemblers.Count);
        newAssemblers.CopyTo(factory.assemblerPool);
        factory.assemblerCursor = factory.assemblerPool.Length;
        factory.assemblerRecycleCursor = 0;
    }

    //[HarmonyPrefix]
    //[HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.Export))]
    public static bool Export_DebugStackTrace(FactorySystem __instance, BinaryWriter w)
    {
        w.Write(0);
        PerformanceMonitor.BeginData(ESaveDataEntry.Miner);
        w.Write(__instance.minerCapacity);
        w.Write(__instance.minerCursor);
        w.Write(__instance.minerRecycleCursor);
        for (int i = 1; i < __instance.minerCursor; i++)
        {
            __instance.minerPool[i].Export(w);
        }
        for (int j = 0; j < __instance.minerRecycleCursor; j++)
        {
            w.Write(__instance.minerRecycle[j]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Miner);
        PerformanceMonitor.BeginData(ESaveDataEntry.Inserter);
        w.Write(__instance.inserterCapacity);
        w.Write(__instance.inserterCursor);
        w.Write(__instance.inserterRecycleCursor);
        WeaverFixes.Logger.LogMessage($"Inserter Capacity: {__instance.inserterCapacity}");
        WeaverFixes.Logger.LogMessage($"Inserter Cursor: {__instance.inserterCursor}");
        WeaverFixes.Logger.LogMessage($"Inserter Recycle Cursor: {__instance.inserterRecycleCursor}");
        WeaverFixes.Logger.LogMessage($"Array Length: {__instance.inserterPool.Length}");
        for (int k = 1; k < __instance.inserterCursor; k++)
        {
            __instance.inserterPool[k].Export(w);
        }
        for (int l = 0; l < __instance.inserterRecycleCursor; l++)
        {
            w.Write(__instance.inserterRecycle[l]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Inserter);
        PerformanceMonitor.BeginData(ESaveDataEntry.Assembler);
        w.Write(__instance.assemblerCapacity);
        w.Write(__instance.assemblerCursor);
        w.Write(__instance.assemblerRecycleCursor);
        for (int m = 1; m < __instance.assemblerCursor; m++)
        {
            __instance.assemblerPool[m].Export(w);
        }
        for (int n = 0; n < __instance.assemblerRecycleCursor; n++)
        {
            w.Write(__instance.assemblerRecycle[n]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Assembler);
        PerformanceMonitor.BeginData(ESaveDataEntry.Fractionator);
        w.Write(__instance.fractionatorCapacity);
        w.Write(__instance.fractionatorCursor);
        w.Write(__instance.fractionatorRecycleCursor);
        for (int num = 1; num < __instance.fractionatorCursor; num++)
        {
            __instance.fractionatorPool[num].Export(w);
        }
        for (int num2 = 0; num2 < __instance.fractionatorRecycleCursor; num2++)
        {
            w.Write(__instance.fractionatorRecycle[num2]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Fractionator);
        PerformanceMonitor.BeginData(ESaveDataEntry.Ejector);
        w.Write(__instance.ejectorCapacity);
        w.Write(__instance.ejectorCursor);
        w.Write(__instance.ejectorRecycleCursor);
        for (int num3 = 1; num3 < __instance.ejectorCursor; num3++)
        {
            __instance.ejectorPool[num3].Export(w);
        }
        for (int num4 = 0; num4 < __instance.ejectorRecycleCursor; num4++)
        {
            w.Write(__instance.ejectorRecycle[num4]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Ejector);
        PerformanceMonitor.BeginData(ESaveDataEntry.Silo);
        w.Write(__instance.siloCapacity);
        w.Write(__instance.siloCursor);
        w.Write(__instance.siloRecycleCursor);
        for (int num5 = 1; num5 < __instance.siloCursor; num5++)
        {
            __instance.siloPool[num5].Export(w);
        }
        for (int num6 = 0; num6 < __instance.siloRecycleCursor; num6++)
        {
            w.Write(__instance.siloRecycle[num6]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Silo);
        PerformanceMonitor.BeginData(ESaveDataEntry.Lab);
        w.Write(__instance.labCapacity);
        w.Write(__instance.labCursor);
        w.Write(__instance.labRecycleCursor);
        for (int num7 = 1; num7 < __instance.labCursor; num7++)
        {
            __instance.labPool[num7].Export(w);
        }
        for (int num8 = 0; num8 < __instance.labRecycleCursor; num8++)
        {
            w.Write(__instance.labRecycle[num8]);
        }
        PerformanceMonitor.EndData(ESaveDataEntry.Lab);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }
}