using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

public class LinearInserterDataAccessOptimization
{
    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(LinearInserterDataAccessOptimization));
    }

    [HarmonyPriority(2)] // need to be executed before InserterMultithreadingOptimization
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(LinearInserterDataAccessOptimization)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            PowerSystem power = planet.powerSystem;
            CargoTraffic cargoTraffic = planet.cargoTraffic;
            PlanetTransport transport = planet.transport;
            DefenseSystem defenseSystem = planet.defenseSystem;
            DigitalSystem digitalSystem = planet.digitalSystem;
            HashSystem hashSystem = planet.hashSystemStatic;
            if (factory == null ||
                power == null ||
                cargoTraffic == null ||
                transport == null ||
                defenseSystem == null ||
                digitalSystem == null ||
                hashSystem == null)
            {
                continue;
            }

            CompactInserters(planet, factory);
            InserterLinearAccessToAssemblers(planet, factory);
            //InserterLinearAccessToAssemblerEntities(planet, factory, power, hashSystem);
            //InserterLinearAccessToPowerConsumers(planet, factory, power, cargoTraffic, transport, defenseSystem, digitalSystem);
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
                    goto noPickTarget;
                }

                if (!seenAssemblerIDs.Add(inserter.pickTarget))
                {
                    goto noPickTarget;
                }

                AssemblerComponent assemblerCopy = oldAssemblers[oldAssemblerId];
                assemblerCopy.id = newAssemblers.Count;
                planet.entityPool[inserter.pickTarget].assemblerId = assemblerCopy.id;
                newAssemblers.Add(assemblerCopy);
            }
        noPickTarget:

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

        // Graph should really contain all assemblers but this is a hack until that happens
        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            if (seenAssemblerIDs.Contains(i))
            {
                continue;
            }

            if (oldAssemblers[i].id != i)
            {
                continue;
            }

            AssemblerComponent assemblerCopy = oldAssemblers[i];
            assemblerCopy.id = newAssemblers.Count;
            planet.entityPool[assemblerCopy.entityId].assemblerId = assemblerCopy.id;
            newAssemblers.Add(assemblerCopy);
        }

        factory.SetAssemblerCapacity(newAssemblers.Count);
        newAssemblers.CopyTo(factory.assemblerPool);
        factory.assemblerCursor = factory.assemblerPool.Length;
        factory.assemblerRecycleCursor = 0;
    }

    private static void InserterLinearAccessToAssemblerEntities(PlanetFactory planet,
                                                                FactorySystem factory,
                                                                PowerSystem power,
                                                                HashSystem hashSystem)
    {
        const int connSlotCount = 16;

        EntityData[] oldEntityPool = planet.entityPool;
        AnimData[] oldEntityAnimPool = planet.entityAnimPool;
        SignData[] oldEntitySignPool = planet.entitySignPool;
        int[] oldEntityConnPool = planet.entityConnPool;
        Mutex[] oldEntityMutexs = planet.entityMutexs;
        int[][] oldEntityNeeds = planet.entityNeeds;

        List<EntityData> newEntityPool = [];
        List<AnimData> newEntityAnimPool = [];
        List<SignData> newEntitySignPool = [];
        List<int> newEntityConnPool = [];
        List<Mutex> newEntityMutexs = [];
        List<int[]> newEntityNeeds = [];

        newEntityPool.Add(default);
        newEntityAnimPool.Add(default);
        newEntitySignPool.Add(default);
        newEntityConnPool.AddRange(Enumerable.Repeat(0, connSlotCount));
        newEntityMutexs.Add(default);
        newEntityNeeds.Add(default);

        HashSet<int> allOldAssemblerEntityIds = [];
        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            ref readonly AssemblerComponent assembler = ref factory.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            allOldAssemblerEntityIds.Add(assembler.entityId);
        }

        for (int i = 1; i < planet.entityCursor; i++)
        {
            if (allOldAssemblerEntityIds.Contains(i))
            {
                newEntityPool.Add(default);
                newEntityAnimPool.Add(default);
                newEntitySignPool.Add(default);
                newEntityConnPool.AddRange(Enumerable.Repeat(0, connSlotCount));
                newEntityMutexs.Add(default);
                newEntityNeeds.Add(default);
                continue;
            }

            newEntityPool.Add(oldEntityPool[i]);
            newEntityAnimPool.Add(oldEntityAnimPool[i]);
            newEntitySignPool.Add(oldEntitySignPool[i]);
            newEntityConnPool.AddRange(Enumerable.Range(i, connSlotCount).Select(x => oldEntityConnPool[x]));
            newEntityMutexs.Add(oldEntityMutexs[i]);
            newEntityNeeds.Add(oldEntityNeeds[i]);
        }

        Dictionary<int, int> oldToNewAssemblerEntityId = [];
        for (int i = 1; i < planet.entityCursor; i++)
        {
            if (!allOldAssemblerEntityIds.Contains(i))
            {
                continue;
            }

            EntityData entity = oldEntityPool[i];
            entity.id = newEntityPool.Count;
            entity.hashAddress = hashSystem.UpdateObjectHashAddress(entity.hashAddress, i, entity.pos, EObjectType.None);
            oldToNewAssemblerEntityId.Add(i, entity.id);

            newEntityPool.Add(entity);
            newEntityAnimPool.Add(oldEntityAnimPool[i]);
            newEntitySignPool.Add(oldEntitySignPool[i]);
            newEntityConnPool.AddRange(Enumerable.Range(i, connSlotCount).Select(x => oldEntityConnPool[x]));
            newEntityMutexs.Add(oldEntityMutexs[i]);
            newEntityNeeds.Add(oldEntityNeeds[i]);
        }

        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            ref AssemblerComponent assembler = ref factory.assemblerPool[i];
            if (assembler.id != i)
            {
                continue;
            }

            assembler.entityId = oldToNewAssemblerEntityId[assembler.entityId];
            newEntityNeeds[assembler.entityId] = assembler.needs;
        }

        for (int i = 1; i < factory.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref factory.inserterPool[i];
            if (inserter.id != i)
            {
                continue;
            }

            if (inserter.pickTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.pickTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                //planet.ReadObjectConn(inserter.entityId, 1, out bool isOutput, out int otherObjId, out int otherSlot);
                //// WriteObjectConn only clears the connection it makes so we have to manually clear
                //// the connection we are overriding.
                //planet.ClearObjectConn(inserter.entityId, 1);
                //planet.ClearObjectConn(otherObjId, otherSlot);
                //planet.WriteObjectConn(inserter.entityId, 1, isOutput, oldToNewAssemblerEntityId[inserter.pickTarget], otherSlot);

                inserter.pickTarget = oldToNewAssemblerEntityId[inserter.pickTarget];
            }

            if (inserter.insertTarget != 0)
            {
                int oldAssemblerId = planet.entityPool[inserter.insertTarget].assemblerId;
                if (oldAssemblerId == 0)
                {
                    continue;
                }

                //planet.ReadObjectConn(inserter.entityId, 0, out bool isOutput, out int otherObjId, out int otherSlot);
                //// WriteObjectConn only clears the connection it makes so we have to manually clear
                //// the connection we are overriding.
                //planet.ClearObjectConn(inserter.entityId, 0);
                //planet.ClearObjectConn(otherObjId, otherSlot);
                //planet.WriteObjectConn(inserter.entityId, 0, isOutput, oldToNewAssemblerEntityId[inserter.insertTarget], otherSlot);

                inserter.insertTarget = oldToNewAssemblerEntityId[inserter.insertTarget];
            }
        }

        for (int i = 1; i < factory.minerCursor; i++)
        {
            ref MinerComponent miner = ref factory.minerPool[i];
            if (miner.id != i)
            {
                continue;
            }

            if (oldToNewAssemblerEntityId.TryGetValue(miner.insertTarget, out int newInsertTarget))
            {
                miner.insertTarget = newInsertTarget;
            }
        }

        // Making mutex array larger in SetEntityCapacity requires that
        // the new capacity is not less than the previous one
        int newEntityCapacity = Math.Max(planet.entityCapacity, newEntityPool.Count);
        int newEntityCursor = newEntityPool.Count;
        planet.SetEntityCapacity(newEntityCapacity);

        newEntityPool.CopyTo(planet.entityPool);
        newEntityAnimPool.CopyTo(planet.entityAnimPool);
        newEntitySignPool.CopyTo(planet.entitySignPool);
        newEntityConnPool.CopyTo(planet.entityConnPool);
        newEntityMutexs.CopyTo(planet.entityMutexs);
        newEntityNeeds.CopyTo(planet.entityNeeds);
        planet.entityCursor = newEntityCursor;
        planet.entityRecycleCursor = 0;

        foreach (KeyValuePair<int, int> oldToNewEntityId in oldToNewAssemblerEntityId)
        {
            planet.HandleObjectConnChangeWhenBuild(oldToNewEntityId.Key, oldToNewEntityId.Value);
        }

        //for (int i = 1; i < factory.inserterCursor; i++)
        //{
        //    ref InserterComponent inserter = ref factory.inserterPool[i];
        //    if (inserter.id != i)
        //    {
        //        continue;
        //    }

        //    if (inserter.pickTarget != 0)
        //    {
        //        int oldAssemblerId = planet.entityPool[inserter.pickTarget].assemblerId;
        //        if (oldAssemblerId == 0)
        //        {
        //            continue;
        //        }

        //        //planet.ReadObjectConn(inserter.entityId, 1, out bool isOutput, out int otherObjId, out int otherSlot);
        //        //// WriteObjectConn only clears the connection it makes so we have to manually clear
        //        //// the connection we are overriding.
        //        //planet.ClearObjectConn(inserter.entityId, 1);
        //        //planet.ClearObjectConn(otherObjId, otherSlot);
        //        //planet.WriteObjectConn(inserter.entityId, 1, isOutput, oldToNewAssemblerEntityId[inserter.pickTarget], otherSlot);

        //        inserter.pickTarget = oldToNewAssemblerEntityId[inserter.pickTarget];
        //    }

        //    if (inserter.insertTarget != 0)
        //    {
        //        int oldAssemblerId = planet.entityPool[inserter.insertTarget].assemblerId;
        //        if (oldAssemblerId == 0)
        //        {
        //            continue;
        //        }

        //        //planet.ReadObjectConn(inserter.entityId, 0, out bool isOutput, out int otherObjId, out int otherSlot);
        //        //// WriteObjectConn only clears the connection it makes so we have to manually clear
        //        //// the connection we are overriding.
        //        //planet.ClearObjectConn(inserter.entityId, 0);
        //        //planet.ClearObjectConn(otherObjId, otherSlot);
        //        //planet.WriteObjectConn(inserter.entityId, 0, isOutput, oldToNewAssemblerEntityId[inserter.insertTarget], otherSlot);

        //        inserter.insertTarget = oldToNewAssemblerEntityId[inserter.insertTarget];
        //    }
        //}

        //// Since methods are used to modify the entity conn(whatever that is)
        //// we will have to do it after we've saved our new entity arrays
        //for (int i = 1; i < planet.entityCursor; i++)
        //{
        //    for (int slotIndex = 0; slotIndex < connSlotCount; slotIndex++)
        //    {
        //        planet.ReadObjectConn(i, slotIndex, out bool isOutput, out int otherObjId, out int otherSlot);
        //        if (oldToNewAssemblerEntityId.TryGetValue(otherObjId, out int newOtherObjId))
        //        {
        //            // WriteObjectConn only clears the connection it makes so we have to manually clear
        //            // the connection we are overriding.
        //            planet.ClearObjectConn(i, slotIndex);
        //            planet.ClearObjectConn(otherObjId, otherSlot);


        //            planet.WriteObjectConn(i, slotIndex, isOutput, newOtherObjId, otherSlot);
        //        }
        //    }
        //}
    }

    private static void InserterLinearAccessToPowerConsumers(PlanetFactory planet,
                                                             FactorySystem factory,
                                                             PowerSystem power,
                                                             CargoTraffic cargoTraffic,
                                                             PlanetTransport transport,
                                                             DefenseSystem defenseSystem,
                                                             DigitalSystem digitalSystem)
    {
        List<EntityIndexAndPowerIndex> entityTypeIndexAndPowerConsumerIndexes = [];
        for (int i = 1; i < cargoTraffic.monitorCursor; i++)
        {
            ref readonly MonitorComponent component = ref cargoTraffic.monitorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Monitor, i, component.pcId));
        }
        for (int i = 1; i < cargoTraffic.spraycoaterCursor; i++)
        {
            ref readonly SpraycoaterComponent component = ref cargoTraffic.spraycoaterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.SprayCoater, i, component.pcId));
        }
        for (int i = 1; i < cargoTraffic.pilerCursor; i++)
        {
            ref readonly PilerComponent component = ref cargoTraffic.pilerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Piler, i, component.pcId));
        }
        for (int i = 1; i < factory.minerCursor; i++)
        {
            ref readonly MinerComponent component = ref factory.minerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Miner, i, component.pcId));
        }
        for (int i = 1; i < factory.inserterCursor; i++)
        {
            ref readonly InserterComponent component = ref factory.inserterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Inserter, i, component.pcId));
        }
        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            ref readonly AssemblerComponent component = ref factory.assemblerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Assembler, i, component.pcId));
        }
        for (int i = 1; i < factory.fractionatorCursor; i++)
        {
            ref readonly FractionatorComponent component = ref factory.fractionatorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Fractionator, i, component.pcId));
        }
        for (int i = 1; i < factory.ejectorCursor; i++)
        {
            ref readonly EjectorComponent component = ref factory.ejectorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Ejector, i, component.pcId));
        }
        for (int i = 1; i < factory.siloCursor; i++)
        {
            ref readonly SiloComponent component = ref factory.siloPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Silo, i, component.pcId));
        }
        for (int i = 1; i < factory.labCursor; i++)
        {
            ref readonly LabComponent component = ref factory.labPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Lab, i, component.pcId));
        }
        for (int i = 1; i < transport.stationCursor; i++)
        {
            StationComponent component = transport.stationPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Station, i, component.pcId));
        }
        for (int i = 1; i < transport.dispenserCursor; i++)
        {
            DispenserComponent component = transport.dispenserPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Dispenser, i, component.pcId));
        }
        for (int i = 1; i < digitalSystem.markers.cursor; i++)
        {
            MarkerComponent component = digitalSystem.markers.buffer[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Marker, i, component.pcId));
        }
        for (int i = 1; i < defenseSystem.turrets.cursor; i++)
        {
            ref readonly TurretComponent component = ref defenseSystem.turrets.buffer[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.Turret, i, component.pcId));
        }
        for (int i = 1; i < defenseSystem.fieldGenerators.cursor; i++)
        {
            ref readonly FieldGeneratorComponent component = ref defenseSystem.fieldGenerators.buffer[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.FieldGenerator, i, component.pcId));
        }
        for (int i = 1; i < defenseSystem.battleBases.cursor; i++)
        {
            BattleBaseComponent component = defenseSystem.battleBases.buffer[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            entityTypeIndexAndPowerConsumerIndexes.Add(new EntityIndexAndPowerIndex(EntityType.BattleBase, i, component.pcId));
        }

        HashSet<int> usedPowerConsumerIndexes = [];
        for (int i = 1; i < power.consumerCursor; i++)
        {
            ref readonly PowerConsumerComponent component = ref power.consumerPool[i];
            if (component.id != i)
            {
                continue;
            }

            usedPowerConsumerIndexes.Add(i);
        }

        if (!usedPowerConsumerIndexes.SetEquals(new HashSet<int>(entityTypeIndexAndPowerConsumerIndexes.Select(x => x.PowerConsumerIndex))))
        {
            throw new InvalidOperationException($"""
                Failed to gather all power consuming entities.
                Expected: {usedPowerConsumerIndexes.Count:N0}
                Actual: {new HashSet<int>(entityTypeIndexAndPowerConsumerIndexes.Select(x => x.PowerConsumerIndex)).Count:N0}
                {entityTypeIndexAndPowerConsumerIndexes.First()}
                """);
        }

        PowerConsumerComponent[] oldPowerConsumers = power.consumerPool;
        List<PowerConsumerComponent> newPowerConsumers = [];
        newPowerConsumers.Add(new PowerConsumerComponent() { id = 0 });
        Dictionary<int, int> oldToNewPowerConsumerIndex = [];

        foreach (EntityIndexAndPowerIndex item in entityTypeIndexAndPowerConsumerIndexes.GroupBy(x => x.EntityType)
                                                                   .SelectMany(x => x.OrderBy(y => y.EntityIndex)))
        {
            if (oldToNewPowerConsumerIndex.ContainsKey(item.PowerConsumerIndex))
            {
                continue;
            }

            oldToNewPowerConsumerIndex.Add(item.PowerConsumerIndex, newPowerConsumers.Count);

            PowerConsumerComponent powerConsumerCopy = oldPowerConsumers[item.PowerConsumerIndex];
            powerConsumerCopy.id = newPowerConsumers.Count;
            planet.entityPool[powerConsumerCopy.entityId].powerConId = powerConsumerCopy.id;
            newPowerConsumers.Add(powerConsumerCopy);
        }

        power.SetConsumerCapacity(newPowerConsumers.Count);
        newPowerConsumers.CopyTo(power.consumerPool);
        power.consumerCursor = power.consumerPool.Length;
        power.consumerRecycleCursor = 0;

        for (int i = 1; i < cargoTraffic.monitorCursor; i++)
        {
            ref MonitorComponent component = ref cargoTraffic.monitorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < cargoTraffic.spraycoaterCursor; i++)
        {
            ref SpraycoaterComponent component = ref cargoTraffic.spraycoaterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < cargoTraffic.pilerCursor; i++)
        {
            ref PilerComponent component = ref cargoTraffic.pilerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < cargoTraffic.pilerCursor; i++)
        {
            ref PilerComponent component = ref cargoTraffic.pilerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.minerCursor; i++)
        {
            ref MinerComponent component = ref factory.minerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.inserterCursor; i++)
        {
            ref InserterComponent component = ref factory.inserterPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.assemblerCursor; i++)
        {
            ref AssemblerComponent component = ref factory.assemblerPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.fractionatorCursor; i++)
        {
            ref FractionatorComponent component = ref factory.fractionatorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.ejectorCursor; i++)
        {
            ref EjectorComponent component = ref factory.ejectorPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.siloCursor; i++)
        {
            ref SiloComponent component = ref factory.siloPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < factory.labCursor; i++)
        {
            ref LabComponent component = ref factory.labPool[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < transport.stationCursor; i++)
        {
            StationComponent component = transport.stationPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < transport.dispenserCursor; i++)
        {
            DispenserComponent component = transport.dispenserPool[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < digitalSystem.markers.cursor; i++)
        {
            MarkerComponent component = digitalSystem.markers.buffer[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < defenseSystem.turrets.cursor; i++)
        {
            ref TurretComponent component = ref defenseSystem.turrets.buffer[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < defenseSystem.fieldGenerators.cursor; i++)
        {
            ref FieldGeneratorComponent component = ref defenseSystem.fieldGenerators.buffer[i];
            if (component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }
        for (int i = 1; i < defenseSystem.battleBases.cursor; i++)
        {
            BattleBaseComponent component = defenseSystem.battleBases.buffer[i];
            if (component == null || component.id != i || component.pcId == 0)
            {
                continue;
            }

            component.pcId = oldToNewPowerConsumerIndex[component.pcId];
        }

        for (int networkIndex = 1; networkIndex < power.netCursor; networkIndex++)
        {
            if (power.netPool[networkIndex] == null || power.netPool[networkIndex].id == 0)
            {
                continue;
            }

            PowerNetwork network = power.netPool[networkIndex];

            foreach (PowerNetworkStructures.Node node in network.nodes)
            {
                for (int i = 0; i < node.consumers.Count; i++)
                {
                    if (oldToNewPowerConsumerIndex.TryGetValue(node.consumers[i], out int newConsumerIndex))
                    {
                        node.consumers[i] = newConsumerIndex;
                    }
                }
                node.consumers.Sort();
            }

            for (int i = 0; i < network.consumers.Count; i++)
            {
                if (oldToNewPowerConsumerIndex.TryGetValue(network.consumers[i], out int newConsumerIndex))
                {
                    network.consumers[i] = newConsumerIndex;
                }
            }
            network.consumers.Sort();
        }
    }

    private record struct EntityIndexAndPowerIndex(EntityType EntityType, int EntityIndex, int PowerConsumerIndex);
}

internal sealed class OptimizedPlanet
{
    private static readonly Dictionary<PlanetFactory, OptimizedPlanet> _planetToOptimizedEntities = [];

    InserterExecutor<OptimizedBiInserter> _optimizedBiInserterExecutor;
    InserterExecutor<OptimizedInserter> _optimizedInserterExecutor;

    private int[] _assemblerNetworkIds;
    public AssemblerState[] _assemblerStates;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    private NetworkIdAndState<LabState>[] _labNetworkIdAndStates;

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(OptimizedPlanet));
    }

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(OptimizedPlanet)}");

        _planetToOptimizedEntities.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            var optimizedInserters = new OptimizedPlanet();
            optimizedInserters.InitializeData(planet);
            _planetToOptimizedEntities.Add(planet, optimizedInserters);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogMessage($"Saving {nameof(OptimizedPlanet)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet == GameMain.localPlanet.factory)
            {
                continue;
            }

            if (_planetToOptimizedEntities.TryGetValue(planet, out OptimizedPlanet optimizedInserters))
            {
                optimizedInserters.Save(planet);
            }
        }
    }

    public void InitializeData(PlanetFactory planet)
    {
        InitializeInserters(planet);
        InitializeAssemblers(planet);
        InitializeMiners(planet);
        InitializeEjectors(planet);
        InitializeLabAssemblers(planet);
    }

    private void InitializeInserters(PlanetFactory planet)
    {
        _optimizedBiInserterExecutor = new InserterExecutor<OptimizedBiInserter>();
        _optimizedBiInserterExecutor.Initialize(planet, x => x.bidirectional);

        _optimizedInserterExecutor = new InserterExecutor<OptimizedInserter>();
        _optimizedInserterExecutor.Initialize(planet, x => !x.bidirectional);
    }

    private void InitializeAssemblers(PlanetFactory planet)
    {
        int[] assemblerNetworkIds = new int[planet.factorySystem.assemblerCursor];
        AssemblerState[] assemblerStates = new AssemblerState[planet.factorySystem.assemblerCursor];

        for (int i = 0; i < planet.factorySystem.assemblerCursor; i++)
        {
            ref readonly AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[i];
            if (assembler.id != i)
            {
                assemblerStates[i] = AssemblerState.InactiveNoAssembler;
                continue;
            }

            assemblerNetworkIds[i] = planet.powerSystem.consumerPool[assembler.pcId].networkId;

            if (assembler.recipeId == 0)
            {
                assemblerStates[i] = AssemblerState.InactiveNoRecipeSet;
            }
            else
            {
                assemblerStates[i] = AssemblerState.Active;
            }

            // set it here so we don't have to set it in the update loop.
            // Need to remember to update it when the assemblers recipe is changed.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
        }

        _assemblerNetworkIds = assemblerNetworkIds;
        _assemblerStates = assemblerStates;
    }

    private void InitializeMiners(PlanetFactory planet)
    {
        int[] minerNetworkIds = new int[planet.factorySystem.minerCursor];

        for (int i = 0; i < planet.factorySystem.minerCursor; i++)
        {
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[i];
            if (miner.id != i)
            {
                continue;
            }

            minerNetworkIds[i] = planet.powerSystem.consumerPool[miner.pcId].networkId;

        }

        _minerNetworkIds = minerNetworkIds;
    }

    private void InitializeEjectors(PlanetFactory planet)
    {
        int[] ejectorNetworkIds = new int[planet.factorySystem.ejectorCursor];

        for (int i = 0; i < planet.factorySystem.ejectorCursor; i++)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[i];
            if (ejector.id != i)
            {
                continue;
            }

            ejectorNetworkIds[i] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }

    private void InitializeLabAssemblers(PlanetFactory planet)
    {
        NetworkIdAndState<LabState>[] labNetworkIdAndStates = new NetworkIdAndState<LabState>[planet.factorySystem.labCursor];

        for (int i = 0; i < planet.factorySystem.labCursor; i++)
        {
            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            if (lab.id != i)
            {
                labNetworkIdAndStates[i] = new NetworkIdAndState<LabState>((int)LabState.InactiveNoAssembler, 0);
                continue;
            }

            LabState state = LabState.Active;
            if (lab.recipeId == 0)
            {
                state = LabState.InactiveNoRecipeSet;
            }
            if (lab.researchMode)
            {
                state = state | LabState.ResearchMode;
            }

            labNetworkIdAndStates[i] = new NetworkIdAndState<LabState>((int)state, planet.powerSystem.consumerPool[lab.pcId].networkId);

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            planet.entityNeeds[lab.entityId] = lab.needs;
        }

        _labNetworkIdAndStates = labNetworkIdAndStates;
    }

    public void Save(PlanetFactory planet)
    {
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkerThreadExecutor), nameof(WorkerThreadExecutor.InserterPartExecute))]
    public static bool InserterPartExecute(WorkerThreadExecutor __instance)
    {
        InserterPartExecute(__instance, x => x._optimizedBiInserterExecutor);
        InserterPartExecute(__instance, x => x._optimizedInserterExecutor);

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public static void InserterPartExecute<T>(WorkerThreadExecutor __instance, Func<OptimizedPlanet, T> inserterExecutorSelector)
        where T : IInserterExecutor
    {
        if (__instance.inserterFactories == null)
        {
            return;
        }
        int totalGalaxyInserterCount = 0;
        for (int planetIndex = 0; planetIndex < __instance.inserterFactoryCnt; planetIndex++)
        {
            PlanetFactory planet = __instance.inserterFactories[planetIndex];
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);
            totalGalaxyInserterCount += optimizedInserterExecutor.inserterCount;
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
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            int totalInsertersIncludingOnThisPlanets = totalInsertersSeenOnPreviousPlanets + optimizedInserterExecutor.inserterCount;
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
            OptimizedPlanet optimizedPlanet = _planetToOptimizedEntities[planet];
            T optimizedInserterExecutor = inserterExecutorSelector(optimizedPlanet);

            bool isActive = __instance.inserterLocalPlanet == __instance.inserterFactories[planetIndex].planet;
            int num5 = _start - totalInsertersSeenOnPreviousPlanets;
            int num6 = _end - totalInsertersSeenOnPreviousPlanets;
            if (_end - _start > optimizedInserterExecutor.inserterCount - num5)
            {
                try
                {
                    if (!isActive)
                    {
                        optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, optimizedInserterExecutor.inserterCount);
                    }
                    else
                    {
                        int planetInserterStartIndex = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num5);
                        int planetInserterEnd = optimizedInserterExecutor.GetUnoptimizedInserterIndex(optimizedInserterExecutor.inserterCount - 1) + 1;
                        __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, planetInserterStartIndex, planetInserterEnd);
                    }
                    totalInsertersSeenOnPreviousPlanets += optimizedInserterExecutor.inserterCount;
                    _start = totalInsertersSeenOnPreviousPlanets;
                }
                catch (Exception ex)
                {
                    __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter first gametick total cursor: " + optimizedInserterExecutor.inserterCount + "  Start & End: " + num5 + "/" + optimizedInserterExecutor.inserterCount + "  " + ex;
                    __instance.hasErrorMessage = true;
                }
                continue;
            }
            try
            {
                if (!isActive)
                {
                    optimizedInserterExecutor.GameTickInserters(planet, optimizedPlanet, __instance.inserterTime, num5, num6);
                }
                else
                {
                    int planetInserterStartIndex = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num5);
                    int planetInserterEnd = optimizedInserterExecutor.GetUnoptimizedInserterIndex(num6 - 1) + 1;
                    __instance.inserterFactories[planetIndex].factorySystem.GameTickInserters(__instance.inserterTime, isActive, planetInserterStartIndex, planetInserterEnd);
                }
                break;
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + planetIndex.ToString() + " Inserter second gametick total cursor: " + optimizedInserterExecutor.inserterCount + "  Start & End: " + num5 + "/" + num6 + "  " + ex2;
                __instance.hasErrorMessage = true;
                break;
            }
        }
    }

    public static int PickFrom(PlanetFactory planet,
                               OptimizedPlanet optimizedPlanet,
                               ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                               ref readonly InserterConnections inserterConnections,
                               int offset,
                               int filter,
                               int[] needs,
                               out byte stack,
                               out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = inserterConnections.PickFrom;
        int objectIndex = typedObjectIndex.Index;
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            if (needs == null)
            {
                return planet.cargoTraffic.TryPickItem(objectIndex, offset, filter, out stack, out inc);
            }
            return planet.cargoTraffic.TryPickItem(objectIndex, offset, filter, needs, out stack, out inc);
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = optimizedPlanet._assemblerStates[objectIndex];
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveOutputFull)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactivePickFrom;
                return 0;
            }

            int[] products = planet.factorySystem.assemblerPool[objectIndex].products;
            int[] produced = planet.factorySystem.assemblerPool[objectIndex].produced;
            if (products == null)
            {
                throw new InvalidOperationException($"{nameof(products)} should only be null if assembler is inactive which the above if statement should have caught.");
            }
            int num = products.Length;
            switch (num)
            {
                case 1:
                    if (produced[0] > 0 && products[0] > 0 && (filter == 0 || filter == products[0]) && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        int value = Interlocked.Decrement(ref produced[0]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                            return products[0];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[0]);
                        }
                    }
                    break;
                case 2:
                    if ((filter == products[0] || filter == 0) && produced[0] > 0 && products[0] > 0 && (needs == null || needs[0] == products[0] || needs[1] == products[0] || needs[2] == products[0] || needs[3] == products[0] || needs[4] == products[0] || needs[5] == products[0]))
                    {
                        int value = Interlocked.Decrement(ref produced[0]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                            return products[0];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[0]);
                        }
                    }
                    if ((filter == products[1] || filter == 0) && produced[1] > 0 && products[1] > 0 && (needs == null || needs[0] == products[1] || needs[1] == products[1] || needs[2] == products[1] || needs[3] == products[1] || needs[4] == products[1] || needs[5] == products[1]))
                    {
                        int value = Interlocked.Decrement(ref produced[1]);
                        if (value >= 0)
                        {
                            optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                            return products[1];
                        }
                        else
                        {
                            Interlocked.Increment(ref produced[1]);
                        }
                    }
                    break;
                default:
                    {
                        for (int i = 0; i < num; i++)
                        {
                            if ((filter == products[i] || filter == 0) && produced[i] > 0 && products[i] > 0 && (needs == null || needs[0] == products[i] || needs[1] == products[i] || needs[2] == products[i] || needs[3] == products[i] || needs[4] == products[i] || needs[5] == products[i]))
                            {
                                int value = Interlocked.Decrement(ref produced[i]);
                                if (value >= 0)
                                {
                                    optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                                    return products[i];
                                }
                                else
                                {
                                    Interlocked.Increment(ref produced[i]);
                                }
                            }
                        }
                        break;
                    }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[objectIndex];
            lock (planet.entityMutexs[ejector.entityId])
            {
                int bulletId = ejector.bulletId;
                int bulletCount = ejector.bulletCount;
                if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && (needs == null || needs[0] == bulletId || needs[1] == bulletId || needs[2] == bulletId || needs[3] == bulletId || needs[4] == bulletId || needs[5] == bulletId))
                {
                    ejector.TakeOneBulletUnsafe(out inc);
                    return bulletId;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            ref SiloComponent silo = ref planet.factorySystem.siloPool[objectIndex];
            lock (planet.entityMutexs[silo.entityId])
            {
                int bulletId2 = silo.bulletId;
                int bulletCount2 = silo.bulletCount;
                if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && (needs == null || needs[0] == bulletId2 || needs[1] == bulletId2 || needs[2] == bulletId2 || needs[3] == bulletId2 || needs[4] == bulletId2 || needs[5] == bulletId2))
                {
                    silo.TakeOneBulletUnsafe(out inc);
                    return bulletId2;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            int inc2;
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            StorageComponent storageComponent2 = storageComponent;
            if (storageComponent != null)
            {
                storageComponent = storageComponent.topStorage;
                while (storageComponent != null)
                {
                    lock (planet.entityMutexs[storageComponent.entityId])
                    {
                        if (storageComponent.lastEmptyItem != 0 && storageComponent.lastEmptyItem != filter)
                        {
                            int itemId = filter;
                            int count = 1;
                            bool flag = false;
                            if (needs == null)
                            {
                                storageComponent.TakeTailItems(ref itemId, ref count, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                                inc = (byte)inc2;
                                flag = count == 1;
                            }
                            else
                            {
                                bool flag2 = storageComponent.TakeTailItems(ref itemId, ref count, needs, out inc2, planet.entityPool[storageComponent.entityId].battleBaseId > 0);
                                inc = (byte)inc2;
                                flag = count == 1 || flag2;
                            }
                            if (count == 1)
                            {
                                storageComponent.lastEmptyItem = -1;
                                return itemId;
                            }
                            if (!flag)
                            {
                                storageComponent.lastEmptyItem = filter;
                            }
                        }
                        if (storageComponent == storageComponent2)
                        {
                            break;
                        }
                        storageComponent = storageComponent.previousStorage;
                        continue;
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            int inc2;
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (stationComponent != null)
            {
                lock (planet.entityMutexs[stationComponent.entityId])
                {
                    int _itemId = filter;
                    int _count = 1;
                    if (needs == null)
                    {
                        stationComponent.TakeItem(ref _itemId, ref _count, out inc2);
                        inc = (byte)inc2;
                    }
                    else
                    {
                        stationComponent.TakeItem(ref _itemId, ref _count, needs, out inc2);
                        inc = (byte)inc2;
                    }
                    if (_count == 1)
                    {
                        return _itemId;
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Lab)
        {
            int[] products2 = planet.factorySystem.labPool[objectIndex].products;
            int[] produced2 = planet.factorySystem.labPool[objectIndex].produced;
            if (products2 == null || produced2 == null)
            {
                return 0;
            }
            for (int j = 0; j < products2.Length; j++)
            {
                if (produced2[j] > 0 && products2[j] > 0 && (filter == 0 || filter == products2[j]) && (needs == null || needs[0] == products2[j] || needs[1] == products2[j] || needs[2] == products2[j] || needs[3] == products2[j] || needs[4] == products2[j] || needs[5] == products2[j]))
                {
                    int value = Interlocked.Decrement(ref produced2[j]);
                    if (value >= 0)
                    {
                        return products2[j];
                    }
                    else
                    {
                        Interlocked.Increment(ref produced2[j]);
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            ref PowerGeneratorComponent powerGenerator = ref planet.powerSystem.genPool[offset];
            int inc2;
            if (offset > 0 && planet.powerSystem.genPool[offset].id == offset)
            {
                lock (planet.entityMutexs[powerGenerator.entityId])
                {
                    if (planet.powerSystem.genPool[offset].fuelCount <= 8)
                    {
                        int result = planet.powerSystem.genPool[objectIndex].PickFuelFrom(filter, out inc2);
                        inc = (byte)inc2;
                        return result;
                    }
                }
            }
            return 0;
        }

        return 0;
    }

    public static int InsertInto(PlanetFactory planet,
                                 OptimizedPlanet optimizedPlanet,
                                 ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                                 ref readonly InserterConnections inserterConnections,
                                 int[]? entityNeeds,
                                 int offset,
                                 int itemId,
                                 byte itemCount,
                                 byte itemInc,
                                 out byte remainInc)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = inserterConnections.InsertInto;
        int objectIndex = typedObjectIndex.Index;
        if (objectIndex == 0)
        {
            return 0;
        }

        if (typedObjectIndex.EntityType == EntityType.Belt)
        {
            if (planet.cargoTraffic.TryInsertItem(objectIndex, offset, itemId, itemCount, itemInc))
            {
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Assembler)
        {
            AssemblerState assemblerState = optimizedPlanet._assemblerStates[objectIndex];
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveInputMissing)
            {
                inserterNetworkIdAndState.State = (int)InserterState.InactiveInsertInto;
                return 0;
            }

            if (entityNeeds == null)
            {
                throw new InvalidOperationException($"Array from {nameof(entityNeeds)} should only be null if assembler is inactive which the above if statement should have caught.");
            }
            ref AssemblerComponent reference = ref planet.factorySystem.assemblerPool[objectIndex];
            int[] requires = reference.requires;
            int num = requires.Length;
            if (0 < num && requires[0] == itemId)
            {
                Interlocked.Add(ref reference.served[0], itemCount);
                Interlocked.Add(ref reference.incServed[0], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1] == itemId)
            {
                Interlocked.Add(ref reference.served[1], itemCount);
                Interlocked.Add(ref reference.incServed[1], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2] == itemId)
            {
                Interlocked.Add(ref reference.served[2], itemCount);
                Interlocked.Add(ref reference.incServed[2], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3] == itemId)
            {
                Interlocked.Add(ref reference.served[3], itemCount);
                Interlocked.Add(ref reference.incServed[3], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4] == itemId)
            {
                Interlocked.Add(ref reference.served[4], itemCount);
                Interlocked.Add(ref reference.incServed[4], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5] == itemId)
            {
                Interlocked.Add(ref reference.served[5], itemCount);
                Interlocked.Add(ref reference.incServed[5], itemInc);
                remainInc = 0;
                optimizedPlanet._assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.ejectorPool[objectIndex].bulletId == itemId)
            {
                Interlocked.Add(ref planet.factorySystem.ejectorPool[objectIndex].bulletCount, itemCount);
                Interlocked.Add(ref planet.factorySystem.ejectorPool[objectIndex].bulletInc, itemInc);
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            if (entityNeeds[0] == itemId && planet.factorySystem.siloPool[objectIndex].bulletId == itemId)
            {
                Interlocked.Add(ref planet.factorySystem.siloPool[objectIndex].bulletCount, itemCount);
                Interlocked.Add(ref planet.factorySystem.siloPool[objectIndex].bulletInc, itemInc);
                remainInc = 0;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Lab)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            ref LabComponent reference2 = ref planet.factorySystem.labPool[objectIndex];
            if (reference2.researchMode)
            {
                int[] matrixServed = reference2.matrixServed;
                int[] matrixIncServed = reference2.matrixIncServed;
                if (matrixServed == null)
                {
                    return 0;
                }
                int num2 = itemId - 6001;
                if (num2 >= 0 && num2 < 6)
                {
                    Interlocked.Add(ref matrixServed[num2], 3600 * itemCount);
                    Interlocked.Add(ref matrixIncServed[num2], 3600 * itemInc);
                    remainInc = 0;
                    return itemCount;
                }
            }
            else
            {
                int[] requires2 = reference2.requires;
                int[] served = reference2.served;
                int[] incServed = reference2.incServed;
                if (requires2 == null)
                {
                    return 0;
                }
                int num3 = requires2.Length;
                for (int i = 0; i < num3; i++)
                {
                    if (requires2[i] == itemId)
                    {
                        Interlocked.Add(ref served[i], itemCount);
                        Interlocked.Add(ref incServed[i], itemInc);
                        remainInc = 0;
                        return itemCount;
                    }
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Storage)
        {
            StorageComponent storageComponent = planet.factoryStorage.storagePool[objectIndex];
            while (storageComponent != null)
            {
                lock (planet.entityMutexs[storageComponent.entityId])
                {
                    if (storageComponent.lastFullItem != itemId)
                    {
                        int num4 = 0;
                        num4 = ((planet.entityPool[storageComponent.entityId].battleBaseId != 0) ? storageComponent.AddItemFilteredBanOnly(itemId, itemCount, itemInc, out var remainInc2) : storageComponent.AddItem(itemId, itemCount, itemInc, out remainInc2, useBan: true));
                        remainInc = (byte)remainInc2;
                        if (num4 == itemCount)
                        {
                            storageComponent.lastFullItem = -1;
                        }
                        else
                        {
                            storageComponent.lastFullItem = itemId;
                        }
                        if (num4 != 0 || storageComponent.nextStorage == null)
                        {
                            return num4;
                        }
                    }
                    storageComponent = storageComponent.nextStorage;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Station)
        {
            if (entityNeeds == null)
            {
                return 0;
            }
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
            {
                lock (planet.entityMutexs[stationComponent.entityId])
                {
                    if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
                    {
                        stationComponent.warperCount += itemCount;
                        remainInc = 0;
                        return itemCount;
                    }
                }
            }
            StationStore[] storage = stationComponent.storage;
            for (int j = 0; j < entityNeeds.Length && j < storage.Length; j++)
            {
                if (entityNeeds[j] == itemId && storage[j].itemId == itemId)
                {
                    Interlocked.Add(ref storage[j].count, itemCount);
                    Interlocked.Add(ref storage[j].inc, itemInc);
                    remainInc = 0;
                    return itemCount;
                }
            }

            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.PowerGenerator)
        {
            PowerGeneratorComponent[] genPool = planet.powerSystem.genPool;
            ref PowerGeneratorComponent powerGenerator = ref genPool[objectIndex];
            lock (planet.entityMutexs[powerGenerator.entityId])
            {
                if (itemId == powerGenerator.fuelId)
                {
                    if (powerGenerator.fuelCount < 10)
                    {
                        ref short fuelCount = ref powerGenerator.fuelCount;
                        fuelCount += itemCount;
                        ref short fuelInc = ref powerGenerator.fuelInc;
                        fuelInc += itemInc;
                        remainInc = 0;
                        return itemCount;
                    }
                    return 0;
                }
                if (powerGenerator.fuelId == 0)
                {
                    int[] array = ItemProto.fuelNeeds[powerGenerator.fuelMask];
                    if (array == null || array.Length == 0)
                    {
                        return 0;
                    }
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (array[k] == itemId)
                        {
                            powerGenerator.SetNewFuel(itemId, itemCount, itemInc);
                            remainInc = 0;
                            return itemCount;
                        }
                    }
                    return 0;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Splitter)
        {
            switch (offset)
            {
                case 0:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltA, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 1:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltB, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 2:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltC, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
                case 3:
                    if (planet.cargoTraffic.TryInsertItem(planet.cargoTraffic.splitterPool[objectIndex].beltD, 0, itemId, itemCount, itemInc))
                    {
                        remainInc = 0;
                        return itemCount;
                    }
                    break;
            }
            return 0;
        }

        return 0;
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
            bool isActive = __instance.assemblerLocalPlanet == __instance.assemblerFactories[i].planet;
            try
            {
                if (__instance.assemblerFactories[i].factorySystem != null)
                {
                    if (!isActive)
                    {
                        PlanetFactory planet = __instance.assemblerFactories[i];
                        OptimizedPlanet optimizedInserters = _planetToOptimizedEntities[planet];
                        optimizedInserters.GameTick(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
                    else
                    {
                        __instance.assemblerFactories[i].factorySystem.GameTick(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }

                }
            }
            catch (Exception ex)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Assembler Factory idx:" + i.ToString() + " Assembler gametick " + ex;
                __instance.hasErrorMessage = true;
            }
            try
            {
                if (__instance.assemblerFactories[i].factorySystem != null)
                {
                    if (!isActive)
                    {
                        PlanetFactory planet = __instance.assemblerFactories[i];
                        OptimizedPlanet optimizedInserters = _planetToOptimizedEntities[planet];
                        optimizedInserters.GameTickLabProduceMode(planet, __instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
                    else
                    {
                        __instance.assemblerFactories[i].factorySystem.GameTickLabProduceMode(__instance.assemblerTime, isActive, __instance.usedThreadCnt, __instance.curThreadIdx, 4);
                    }
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

    private void GameTick(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        EntityData[] entityPool = planet.entityPool;
        VeinData[] veinPool = planet.veinPool;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SignData[] entitySignPool = planet.entitySignPool;
        int[][] entityNeeds = planet.entityNeeds;
        FactorySystem factorySystem = planet.factorySystem;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        float num = 1f / 60f;
        AstroData[] astroPoses = null;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            float num2;
            float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
            if (num2 < 5f / 12f)
            {
                num2 = 5f / 12f;
            }
            float num4 = history.miningCostRate;
            float miningSpeedScale = history.miningSpeedScale;
            float num5 = history.miningCostRate * 0.40111667f / num2;
            if (num3 > 99.5f)
            {
                num4 = 0f;
                num5 = 0f;
            }
            bool flag2 = isActive && num4 > 0f;
            for (int i = _start; i < _end; i++)
            {
                if (factorySystem.minerPool[i].id != i)
                {
                    continue;
                }
                int entityId = factorySystem.minerPool[i].entityId;
                float num6 = networkServes[_minerNetworkIds[i]];
                uint num7 = factorySystem.minerPool[i].InternalUpdate(planet, veinPool, num6, (factorySystem.minerPool[i].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
                if (isActive)
                {
                    int stationId = entityPool[entityId].stationId;
                    int num8 = (int)Mathf.Floor(entityAnimPool[entityId].time / 10f);
                    entityAnimPool[entityId].time = entityAnimPool[entityId].time % 10f;
                    entityAnimPool[entityId].Step(num7, num * num6);
                    entityAnimPool[entityId].power = num6;
                    if (stationId > 0)
                    {
                        if (factorySystem.minerPool[i].veinCount > 0)
                        {
                            EVeinType veinTypeByItemId = LDB.veins.GetVeinTypeByItemId(veinPool[factorySystem.minerPool[i].veins[0]].productId);
                            entityAnimPool[entityId].state += (uint)((int)veinTypeByItemId * 100);
                        }
                        entityAnimPool[entityId].power += 10f;
                        entityAnimPool[entityId].power += factorySystem.minerPool[i].speed / 10 * 10;
                        if (num7 == 1)
                        {
                            num8 = 3000;
                        }
                        else
                        {
                            num8 -= (int)(num * 1000f);
                            if (num8 < 0)
                            {
                                num8 = 0;
                            }
                        }
                        entityAnimPool[entityId].time += num8 * 10;
                    }
                    if (entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3)
                    {
                        entitySignPool[entityId].signType = ((factorySystem.minerPool[i].minimumVeinAmount < 1000) ? 7u : 0u);
                    }
                }
                if (flag2 && factorySystem.minerPool[i].type == EMinerType.Vein)
                {
                    if ((long)i % 30L == time % 30)
                    {
                        factorySystem.minerPool[i].GetTotalVeinAmount(veinPool);
                    }
                    if (isActive)
                    {
                        entitySignPool[entityId].count0 = factorySystem.minerPool[i].totalVeinAmount;
                    }
                }
                else
                {
                    if (isActive)
                    {
                        entitySignPool[entityId].count0 = 0f;
                    }
                }
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.assemblerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int k = _start; k < _end; k++)
            {
                if (_assemblerStates[k] != AssemblerState.Active)
                {
                    continue;
                }

                float power = networkServes[_assemblerNetworkIds[k]];
                factorySystem.assemblerPool[k].UpdateNeeds();
                _assemblerStates[k] = AssemblerInternalUpdate(ref factorySystem.assemblerPool[k], power, productRegister, consumeRegister);
            }
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.fractionatorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            for (int l = _start; l < _end; l++)
            {
                if (factorySystem.fractionatorPool[l].id == l)
                {
                    int entityId4 = factorySystem.fractionatorPool[l].entityId;
                    float power2 = networkServes[consumerPool[factorySystem.fractionatorPool[l].pcId].networkId];
                    uint state = factorySystem.fractionatorPool[l].InternalUpdate(factorySystem.factory, power2, entitySignPool, productRegister, consumeRegister);
                    entityAnimPool[entityId4].time = Mathf.Sqrt((float)factorySystem.fractionatorPool[l].fluidInputCount * 0.025f);
                    entityAnimPool[entityId4].state = state;
                    entityAnimPool[entityId4].power = power2;
                }
            }
        }
        lock (factorySystem.ejectorPool)
        {
            if (factorySystem.ejectorCursor - factorySystem.ejectorRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSwarm swarm = null;
        if (factorySystem.factory.dysonSphere != null)
        {
            swarm = factorySystem.factory.dysonSphere.swarm;
        }
        if (WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.ejectorCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            int[] ejectorNetworkIds = _ejectorNetworkIds;
            for (int m = _start; m < _end; m++)
            {
                if (factorySystem.ejectorPool[m].id == m)
                {
                    float power3 = networkServes[ejectorNetworkIds[m]];
                    uint num11 = factorySystem.ejectorPool[m].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);

                    if (isActive)
                    {
                        int entityId5 = factorySystem.ejectorPool[m].entityId;
                        entityAnimPool[entityId5].state = num11;

                        if (entitySignPool[entityId5].signType == 0 || entitySignPool[entityId5].signType > 3)
                        {
                            entitySignPool[entityId5].signType = ((factorySystem.ejectorPool[m].orbitId <= 0 && !factorySystem.ejectorPool[m].autoOrbit) ? 5u : 0u);
                        }
                    }
                }
            }
        }
        lock (factorySystem.siloPool)
        {
            if (factorySystem.siloCursor - factorySystem.siloRecycleCursor > 1)
            {
                astroPoses = factorySystem.planet.galaxy.astrosData;
            }
        }
        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        bool flag3 = dysonSphere != null && dysonSphere.autoNodeCount > 0;
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, factorySystem.siloCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out _start, out _end))
        {
            return;
        }
        for (int n = _start; n < _end; n++)
        {
            if (factorySystem.siloPool[n].id == n)
            {
                int entityId6 = factorySystem.siloPool[n].entityId;
                uint num12 = 0u;
                float power4 = networkServes[consumerPool[factorySystem.siloPool[n].pcId].networkId];
                num12 = factorySystem.siloPool[n].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
                entityAnimPool[entityId6].state = num12;
                entityNeeds[entityId6] = factorySystem.siloPool[n].needs;
                if (entitySignPool[entityId6].signType == 0 || entitySignPool[entityId6].signType > 3)
                {
                    entitySignPool[entityId6].signType = ((!flag3) ? 9u : 0u);
                }
            }
        }
    }

    private AssemblerState AssemblerInternalUpdate(ref AssemblerComponent assembler, float power, int[] productRegister, int[] consumeRegister)
    {
        if (power < 0.1f)
        {
            // Lets not deal with missing power for now. Just check every tick.
            return AssemblerState.Active;
        }

        if (assembler.extraTime >= assembler.extraTimeSpend)
        {
            int num = assembler.products.Length;
            if (num == 1)
            {
                assembler.produced[0] += assembler.productCounts[0];
                lock (productRegister)
                {
                    productRegister[assembler.products[0]] += assembler.productCounts[0];
                }
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    assembler.produced[i] += assembler.productCounts[i];
                    lock (productRegister)
                    {
                        productRegister[assembler.products[i]] += assembler.productCounts[i];
                    }
                }
            }
            assembler.extraCycleCount++;
            assembler.extraTime -= assembler.extraTimeSpend;
        }
        if (assembler.time >= assembler.timeSpend)
        {
            assembler.replicating = false;
            if (assembler.products.Length == 1)
            {
                switch (assembler.recipeType)
                {
                    case ERecipeType.Smelt:
                        if (assembler.produced[0] + assembler.productCounts[0] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    case ERecipeType.Assemble:
                        if (assembler.produced[0] > assembler.productCounts[0] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                    default:
                        if (assembler.produced[0] > assembler.productCounts[0] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                        break;
                }
                assembler.produced[0] += assembler.productCounts[0];
                lock (productRegister)
                {
                    productRegister[assembler.products[0]] += assembler.productCounts[0];
                }
            }
            else
            {
                int num2 = assembler.products.Length;
                if (assembler.recipeType == ERecipeType.Refine)
                {
                    for (int j = 0; j < num2; j++)
                    {
                        if (assembler.produced[j] > assembler.productCounts[j] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Particle)
                {
                    for (int k = 0; k < num2; k++)
                    {
                        if (assembler.produced[k] > assembler.productCounts[k] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Chemical)
                {
                    for (int l = 0; l < num2; l++)
                    {
                        if (assembler.produced[l] > assembler.productCounts[l] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Smelt)
                {
                    for (int m = 0; m < num2; m++)
                    {
                        if (assembler.produced[m] + assembler.productCounts[m] > 100)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else if (assembler.recipeType == ERecipeType.Assemble)
                {
                    for (int n = 0; n < num2; n++)
                    {
                        if (assembler.produced[n] > assembler.productCounts[n] * 9)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                else
                {
                    for (int num3 = 0; num3 < num2; num3++)
                    {
                        if (assembler.produced[num3] > assembler.productCounts[num3] * 19)
                        {
                            return AssemblerState.InactiveOutputFull;
                        }
                    }
                }
                for (int num4 = 0; num4 < num2; num4++)
                {
                    assembler.produced[num4] += assembler.productCounts[num4];
                    lock (productRegister)
                    {
                        productRegister[assembler.products[num4]] += assembler.productCounts[num4];
                    }
                }
            }
            assembler.extraSpeed = 0;
            assembler.speedOverride = assembler.speed;
            assembler.extraPowerRatio = 0;
            assembler.cycleCount++;
            assembler.time -= assembler.timeSpend;
        }
        if (!assembler.replicating)
        {
            int num5 = assembler.requireCounts.Length;
            for (int num6 = 0; num6 < num5; num6++)
            {
                if (assembler.incServed[num6] <= 0)
                {
                    assembler.incServed[num6] = 0;
                }
                if (assembler.served[num6] < assembler.requireCounts[num6] || assembler.served[num6] == 0)
                {
                    assembler.time = 0;
                    return AssemblerState.InactiveInputMissing;
                }
            }
            int num7 = ((num5 > 0) ? 10 : 0);
            for (int num8 = 0; num8 < num5; num8++)
            {
                int num9 = assembler.split_inc_level(ref assembler.served[num8], ref assembler.incServed[num8], assembler.requireCounts[num8]);
                num7 = ((num7 < num9) ? num7 : num9);
                if (!assembler.incUsed)
                {
                    assembler.incUsed = num9 > 0;
                }
                if (assembler.served[num8] == 0)
                {
                    assembler.incServed[num8] = 0;
                }
                lock (consumeRegister)
                {
                    consumeRegister[assembler.requires[num8]] += assembler.requireCounts[num8];
                }
            }
            if (num7 < 0)
            {
                num7 = 0;
            }
            if (assembler.productive && !assembler.forceAccMode)
            {
                assembler.extraSpeed = (int)((double)assembler.speed * Cargo.incTableMilli[num7] * 10.0 + 0.1);
                assembler.speedOverride = assembler.speed;
                assembler.extraPowerRatio = Cargo.powerTable[num7];
            }
            else
            {
                assembler.extraSpeed = 0;
                assembler.speedOverride = (int)((double)assembler.speed * (1.0 + Cargo.accTableMilli[num7]) + 0.1);
                assembler.extraPowerRatio = Cargo.powerTable[num7];
            }
            assembler.replicating = true;
        }
        if (assembler.replicating && assembler.time < assembler.timeSpend && assembler.extraTime < assembler.extraTimeSpend)
        {
            assembler.time += (int)(power * (float)assembler.speedOverride);
            assembler.extraTime += (int)(power * (float)assembler.extraSpeed);
        }
        if (!assembler.replicating)
        {
            throw new InvalidOperationException("I do not think this is possible. Not sure why it is in the game.");
            //return 0u;
        }
        return AssemblerState.Active;
    }

    private void GameTickLabProduceMode(PlanetFactory planet, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
        if (!WorkerThreadExecutor.CalculateMissionIndex(1, planet.factorySystem.labCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
        {
            return;
        }

        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        int[] consumeRegister = obj.consumeRegister;
        float[] networkServes = planet.powerSystem.networkServes;
        for (int i = _start; i < _end; i++)
        {
            NetworkIdAndState<LabState> networkIdAndState = _labNetworkIdAndStates[i];
            if (((LabState)networkIdAndState.State & LabState.Inactive) == LabState.Inactive)
            {
                continue;
            }
            if (((LabState)networkIdAndState.State & LabState.ResearchMode) == LabState.ResearchMode)
            {
                continue;
            }

            ref LabComponent lab = ref planet.factorySystem.labPool[i];
            lab.UpdateNeedsAssemble();

            float power = networkServes[networkIdAndState.Index];
            lab.InternalUpdateAssemble(power, productRegister, consumeRegister);
        }
    }
    public static TypedObjectIndex GetAsTypedObjectIndex(int index, EntityData[] entities)
    {
        ref readonly EntityData entity = ref entities[index];
        if (entity.beltId != 0)
        {
            return new TypedObjectIndex(EntityType.Belt, entity.beltId);
        }
        else if (entity.assemblerId != 0)
        {
            return new TypedObjectIndex(EntityType.Assembler, entity.assemblerId);
        }
        else if (entity.ejectorId != 0)
        {
            return new TypedObjectIndex(EntityType.Ejector, entity.ejectorId);
        }
        else if (entity.siloId != 0)
        {
            return new TypedObjectIndex(EntityType.Silo, entity.siloId);
        }
        else if (entity.labId != 0)
        {
            return new TypedObjectIndex(EntityType.Lab, entity.labId);
        }
        else if (entity.storageId != 0)
        {
            return new TypedObjectIndex(EntityType.Storage, entity.storageId);
        }
        else if (entity.stationId != 0)
        {
            return new TypedObjectIndex(EntityType.Station, entity.stationId);
        }
        else if (entity.powerGenId != 0)
        {
            return new TypedObjectIndex(EntityType.PowerGenerator, entity.powerGenId);
        }
        else if (entity.splitterId != 0)
        {
            return new TypedObjectIndex(EntityType.Splitter, entity.splitterId);
        }
        else if (entity.inserterId != 0)
        {
            return new TypedObjectIndex(EntityType.Inserter, entity.inserterId);
        }

        throw new InvalidOperationException("Unknown entity type.");
    }

    public static int[]? GetEntityNeeds(PlanetFactory planet, int entityIndex)
    {
        ref readonly EntityData entity = ref planet.entityPool[entityIndex];
        if (entity.beltId != 0)
        {
            return null;
        }
        else if (entity.assemblerId != 0)
        {
            return planet.factorySystem.assemblerPool[entity.assemblerId].needs;
        }
        else if (entity.ejectorId != 0)
        {
            return planet.factorySystem.ejectorPool[entity.ejectorId].needs;
        }
        else if (entity.siloId != 0)
        {
            return planet.factorySystem.siloPool[entity.siloId].needs;
        }
        else if (entity.labId != 0)
        {
            return planet.factorySystem.labPool[entity.labId].needs;
        }
        else if (entity.storageId != 0)
        {
            return null;
        }
        else if (entity.stationId != 0)
        {
            return planet.transport.stationPool[entity.stationId].needs;
        }
        else if (entity.powerGenId != 0)
        {
            return null;
        }
        else if (entity.splitterId != 0)
        {
            return null;
        }
        else if (entity.inserterId != 0)
        {
            return null;
        }

        throw new InvalidOperationException("Unknown entity type.");
    }
}

internal readonly struct InserterConnections
{
    public readonly TypedObjectIndex PickFrom;
    public readonly TypedObjectIndex InsertInto;

    public InserterConnections(TypedObjectIndex pickFrom, TypedObjectIndex insertInto)
    {
        PickFrom = pickFrom;
        InsertInto = insertInto;
    }
}

internal readonly struct TypedObjectIndex
{
    private readonly uint _value;

    public readonly EntityType EntityType => (EntityType)(_value >> 24);
    public readonly int Index => (int)(0x00_ff_ff_ff & _value);

    public TypedObjectIndex(EntityType entityType, int index)
    {
        _value = ((uint)entityType << 24) | (uint)index;
    }
}

internal struct NetworkIdAndState<T> where T : Enum
{
    private uint _value;

    public int State
    {
        get { return (int)(_value >> 24); }
        set { _value = (_value & 0x00_ff_ff_ff) | ((uint)value << 24); }
    }
    public readonly int Index => (int)(0x00_ff_ff_ff & _value);

    public NetworkIdAndState(int state, int index)
    {
        _value = ((uint)state << 24) | (uint)index;
    }
}

[Flags]
internal enum InserterState
{
    Active
        = 0b0000,
    Inactive
        = 0b1000,
    InactiveNoInserter
        = 0b0001 | Inactive,
    InactiveNotCompletelyConnected
        = 0b0010 | Inactive,
    InactivePickFrom
        = 0b0100 | Inactive,
    InactiveInsertInto
        = 0b0101 | Inactive
}

[Flags]
internal enum AssemblerState
{
    Active
        = 0b0000,
    Inactive
        = 0b1000,
    InactiveNoAssembler
        = 0b0001 | Inactive,
    InactiveNoRecipeSet
        = 0b0010 | Inactive,
    InactiveOutputFull
        = 0b0011 | Inactive,
    InactiveInputMissing
        = 0b0100 | Inactive
}

[Flags]
internal enum LabState
{
    Active
        = 0b00000,
    Inactive
        = 0b01000,
    InactiveNoAssembler
        = 0b00001 | Inactive,
    InactiveNoRecipeSet
        = 0b00010 | Inactive,
    InactiveOutputFull
        = 0b00011 | Inactive,
    InactiveInputMissing
        = 0b00100 | Inactive,
    ResearchMode
        = 0b10000
}

internal record struct InserterGrade(int Delay, byte StackInput, byte StackOutput, bool Bidirectional);

public enum OptimizedInserterStage : byte
{
    Picking,
    Sending,
    Inserting,
    Returning
}

internal sealed class InserterExecutor<T> : IInserterExecutor<T>
    where T : struct, IInserter<T>
{
    private T[] _optimizedInserters;
    private InserterGrade[] _inserterGrades;
    public NetworkIdAndState<InserterState>[] _inserterNetworkIdAndStates;
    public InserterConnections[] _inserterConnections;
    public int[][] _inserterConnectionNeeds;
    public int[] _optimizedInserterToInserterIndex;

    public int inserterCount => _optimizedInserters.Length;

    public void Initialize(PlanetFactory planet, Func<InserterComponent, bool> inserterSelector)
    {
        (List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates,
         List<InserterConnections> inserterConnections,
         List<int[]> inserterConnectionNeeds,
         List<InserterGrade> inserterGrades,
         Dictionary<InserterGrade, int> inserterGradeToIndex,
         List<T> optimizedInserters,
         List<int> optimizedInserterToInserterIndex)
            = InitializeInserters<T>(planet, inserterSelector);

        _inserterNetworkIdAndStates = inserterNetworkIdAndStates.ToArray();
        _inserterConnections = inserterConnections.ToArray();
        _inserterConnectionNeeds = inserterConnectionNeeds.ToArray();
        _inserterGrades = inserterGrades.ToArray();
        _optimizedInserters = optimizedInserters.ToArray();
        _optimizedInserterToInserterIndex = optimizedInserterToInserterIndex.ToArray();
    }

    public T Create(ref readonly InserterComponent inserter, int grade)
    {
        return default(T).Create(in inserter, grade);
    }

    public int GetUnoptimizedInserterIndex(int optimizedInserterIndex)
    {
        return _optimizedInserterToInserterIndex[optimizedInserterIndex];
    }

    public void GameTickInserters(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, int _start, int _end)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[][] entityNeeds = planet.entityNeeds;
        InserterComponent[] unoptimizedInserters = planet.factorySystem.inserterPool;
        _end = ((_end > _optimizedInserters.Length) ? _optimizedInserters.Length : _end);
        for (int inserterIndex = _start; inserterIndex < _end; inserterIndex++)
        {
            ref NetworkIdAndState<InserterState> networkIdAndState = ref _inserterNetworkIdAndStates[inserterIndex];
            InserterState inserterState = (InserterState)networkIdAndState.State;
            if (inserterState != InserterState.Active)
            {
                if (inserterState == InserterState.InactiveNoInserter ||
                    inserterState == InserterState.InactiveNotCompletelyConnected)
                {
                    continue;
                }
                else if (inserterState == InserterState.InactivePickFrom)
                {
                    if (!IsObjectPickFromActive(optimizedPlanet, inserterIndex))
                    {
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
                else if (inserterState == InserterState.InactiveInsertInto)
                {
                    if (!IsObjectInsertIntoActive(optimizedPlanet, inserterIndex))
                    {
                        continue;
                    }

                    networkIdAndState.State = (int)InserterState.Active;
                }
            }

            float power2 = networkServes[networkIdAndState.Index];
            ref T optimizedInserter = ref _optimizedInserters[inserterIndex];
            InserterGrade inserterGrade = _inserterGrades[optimizedInserter.grade];
            optimizedInserter.Update(planet,
                                     optimizedPlanet,
                                     power2,
                                     inserterIndex,
                                     ref networkIdAndState,
                                     in _inserterConnections[inserterIndex],
                                     in _inserterConnectionNeeds[inserterIndex],
                                     inserterGrade);
        }
    }

    private bool IsObjectPickFromActive(OptimizedPlanet optimizedPlanet, int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].PickFrom;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return optimizedPlanet._assemblerStates[objectIndex.Index] == AssemblerState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if pick from is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private bool IsObjectInsertIntoActive(OptimizedPlanet optimizedPlanet, int inserterIndex)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterIndex].InsertInto;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return optimizedPlanet._assemblerStates[objectIndex.Index] == AssemblerState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if insert into is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private static (List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates,
                    List<InserterConnections> inserterConnections,
                    List<int[]> inserterConnectionNeeds,
                    List<InserterGrade> inserterGrades,
                    Dictionary<InserterGrade, int> inserterGradeToIndex,
                    List<TInserter> optimizedInserters,
                    List<int> optimizedInserterToInserterIndex)
        InitializeInserters<TInserter>(PlanetFactory planet, Func<InserterComponent, bool> inserterSelector)
        where TInserter : struct, IInserter<TInserter>
    {
        List<NetworkIdAndState<InserterState>> inserterNetworkIdAndStates = [];
        List<InserterConnections> inserterConnections = [];
        List<int[]> inserterConnectionNeeds = [];
        List<InserterGrade> inserterGrades = [];
        Dictionary<InserterGrade, int> inserterGradeToIndex = [];
        List<TInserter> optimizedInserters = [];
        List<int> optimizedInserterToInserterIndex = [];

        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[i];
            if (inserter.id != i || !inserterSelector(inserter))
            {
                continue;
            }

            InserterState? inserterState = null;
            TypedObjectIndex pickFrom = new TypedObjectIndex(EntityType.None, 0);
            if (inserter.pickTarget != 0)
            {
                pickFrom = OptimizedPlanet.GetAsTypedObjectIndex(inserter.pickTarget, planet.entityPool);
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
            }

            TypedObjectIndex insertInto = new TypedObjectIndex(EntityType.None, 0);
            int[]? insertIntoNeeds = null;
            if (inserter.insertTarget != 0)
            {
                insertInto = OptimizedPlanet.GetAsTypedObjectIndex(inserter.insertTarget, planet.entityPool);
                insertIntoNeeds = OptimizedPlanet.GetEntityNeeds(planet, inserter.insertTarget);
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
            }

            inserterNetworkIdAndStates.Add(new NetworkIdAndState<InserterState>((int)(inserterState ?? InserterState.Active), planet.powerSystem.consumerPool[inserter.pcId].networkId));
            inserterConnections.Add(new InserterConnections(pickFrom, insertInto));
            inserterConnectionNeeds.Add(insertIntoNeeds);

            InserterGrade inserterGrade;

            // Need to check when i need to update this again.
            // Probably bi direction is related to some research.
            // Probably the same for stack output.
            byte b = (byte)GameMain.history.inserterStackCountObsolete;
            byte b2 = (byte)GameMain.history.inserterStackInput;
            byte stackOutput = (byte)GameMain.history.inserterStackOutput;
            bool inserterBidirectional = GameMain.history.inserterBidirectional;
            int delay = ((b > 1) ? 110000 : 0);
            int delay2 = ((b2 > 1) ? 40000 : 0);

            if (inserter.grade == 3)
            {
                inserterGrade = new InserterGrade(delay, b, 1, false);
            }
            else if (inserter.grade == 4)
            {
                inserterGrade = new InserterGrade(delay2, b2, stackOutput, inserterBidirectional);
            }
            else
            {
                inserterGrade = new InserterGrade(0, 1, 1, false);
            }

            if (!inserterGradeToIndex.TryGetValue(inserterGrade, out int inserterGradeIndex))
            {
                inserterGradeIndex = inserterGrades.Count;
                inserterGrades.Add(inserterGrade);
                inserterGradeToIndex.Add(inserterGrade, inserterGradeIndex);
            }

            optimizedInserters.Add(default(TInserter).Create(in inserter, inserterGradeIndex));
            optimizedInserterToInserterIndex.Add(i);
        }

        return (inserterNetworkIdAndStates,
                inserterConnections,
                inserterConnectionNeeds,
                inserterGrades,
                inserterGradeToIndex,
                optimizedInserters,
                optimizedInserterToInserterIndex);
    }
}

internal interface IInserterExecutor
{
    int inserterCount { get; }

    void Initialize(PlanetFactory planet, Func<InserterComponent, bool> inserterSelector);

    int GetUnoptimizedInserterIndex(int optimizedInserterIndex);

    void GameTickInserters(PlanetFactory planet, OptimizedPlanet optimizedPlanet, long time, int _start, int _end);
}

internal interface IInserterExecutor<T> : IInserterExecutor
    where T : IInserter<T>
{
    T Create(ref readonly InserterComponent inserter, int grade);
}

internal interface IInserter<T>
{
    public byte grade { get; }

    T Create(ref readonly InserterComponent inserter, int grade);

    void Update(PlanetFactory planet,
                OptimizedPlanet optimizedPlanet,
                float power,
                int inserterIndex,
                ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                ref readonly InserterConnections inserterConnections,
                ref readonly int[] inserterConnectionNeeds,
                InserterGrade inserterGrade);
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedBiInserter : IInserter<OptimizedBiInserter>
{
    public byte grade { get; }
    public readonly int pcId;
    public readonly bool careNeeds;
    public readonly short pickOffset;
    public readonly short insertOffset;
    public readonly int filter;
    public OptimizedInserterStage stage;
    public int itemId;
    public short itemCount;
    public short itemInc;
    public int stackCount;
    public int idleTick;

    public OptimizedBiInserter(ref readonly InserterComponent inserter, int grade)
    {
        grade = (byte)grade;
        pcId = inserter.pcId;
        careNeeds = inserter.careNeeds;
        pickOffset = inserter.pickOffset;
        insertOffset = inserter.insertOffset;
        filter = inserter.filter;
        stage = ToOptimizedInserterStage(inserter.stage);
        itemId = inserter.itemId;
        itemCount = inserter.itemCount;
        itemInc = inserter.itemInc;
        stackCount = inserter.stackCount;
        idleTick = inserter.idleTick;
    }

    public OptimizedBiInserter Create(ref readonly InserterComponent inserter, int grade)
    {
        return new OptimizedBiInserter(in inserter, grade);
    }

    public void Update(PlanetFactory planet,
                       OptimizedPlanet optimizedPlanet,
                       float power,
                       int inserterIndex,
                       ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                       ref readonly InserterConnections inserterConnections,
                       ref readonly int[] inserterConnectionNeeds,
                       InserterGrade inserterGrade)
    {
        if (power < 0.1f)
        {
            // Not sure it is worth optimizing low power since it should be a rare occurrence in a large factory
            inserterNetworkIdAndState.State = (int)InserterState.Active;
            return;
        }
        bool flag = false;
        int num = 1;
        do
        {
            byte stack;
            byte inc;
            if (itemId == 0)
            {
                int num2 = 0;
                if (careNeeds)
                {
                    if (idleTick-- < 1)
                    {
                        int[] array = inserterConnectionNeeds;
                        if (array != null && (array[0] != 0 || array[1] != 0 || array[2] != 0 || array[3] != 0 || array[4] != 0 || array[5] != 0))
                        {
                            num2 = OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, filter, array, out stack, out inc);
                            if (num2 > 0)
                            {
                                itemId = num2;
                                itemCount += stack;
                                itemInc += inc;
                                stackCount++;
                                flag = true;
                            }
                            else
                            {
                                num = 0;
                            }
                        }
                        else
                        {
                            idleTick = 9;
                            num = 0;
                        }
                    }
                    else
                    {
                        num = 0;
                    }
                }
                else
                {
                    num2 = OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, filter, null, out stack, out inc);
                    if (num2 > 0)
                    {
                        itemId = num2;
                        itemCount += stack;
                        itemInc += inc;
                        stackCount++;
                        flag = true;
                    }
                    else
                    {
                        num = 0;
                    }
                }
            }
            else
            {
                if (stackCount >= inserterGrade.StackInput)
                {
                    continue;
                }
                if (filter == 0 || filter == itemId)
                {
                    if (careNeeds)
                    {
                        if (idleTick-- < 1)
                        {
                            int[] array2 = inserterConnectionNeeds;
                            if (array2 != null && (array2[0] != 0 || array2[1] != 0 || array2[2] != 0 || array2[3] != 0 || array2[4] != 0 || array2[5] != 0))
                            {
                                int num44 = OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, itemId, array2, out stack, out inc);
                                if (num44 > 0)
                                {
                                    itemCount += stack;
                                    itemInc += inc;
                                    stackCount++;
                                    flag = true;
                                    inserterNetworkIdAndState.State = (int)InserterState.Active;
                                }
                                else
                                {
                                    num = 0;
                                }
                            }
                            else
                            {
                                idleTick = 10;
                                num = 0;
                            }
                        }
                        else
                        {
                            num = 0;
                        }
                    }
                    else if (OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, itemId, null, out stack, out inc) > 0)
                    {
                        itemCount += stack;
                        itemInc += inc;
                        stackCount++;
                        flag = true;
                    }
                    else
                    {
                        num = 0;
                    }
                }
                else
                {
                    num = 0;
                }
            }
        }
        while (num-- > 0);
        num = 1;
        int num3 = 0;
        do
        {
            if (itemId == 0 || stackCount == 0)
            {
                itemId = 0;
                stackCount = 0;
                itemCount = 0;
                itemInc = 0;
                break;
            }
            if (idleTick-- >= 1)
            {
                break;
            }
            TypedObjectIndex num4 = ((inserterGrade.StackOutput > 1) ? inserterConnections.InsertInto : default);
            if (num4.EntityType == EntityType.Belt && num4.Index > 0)
            {
                int num5 = itemCount;
                int num6 = itemInc;
                planet.cargoTraffic.TryInsertItemToBeltWithStackIncreasement(num4.Index, insertOffset, itemId, inserterGrade.StackOutput, ref num5, ref num6);
                if (num5 < itemCount)
                {
                    num3 = itemId;
                }
                itemCount = (short)num5;
                itemInc = (short)num6;
                stackCount = ((itemCount > 0) ? ((itemCount - 1) / 4 + 1) : 0);
                if (stackCount == 0)
                {
                    itemId = 0;
                    itemCount = 0;
                    itemInc = 0;
                    break;
                }
                num = 0;
                continue;
            }

            int[]? insertIntoNeeds = inserterConnectionNeeds;
            if (careNeeds)
            {

                if (insertIntoNeeds == null || (insertIntoNeeds[0] == 0 && insertIntoNeeds[1] == 0 && insertIntoNeeds[2] == 0 && insertIntoNeeds[3] == 0 && insertIntoNeeds[4] == 0 && insertIntoNeeds[5] == 0))
                {
                    idleTick = 10;
                    break;
                }
            }
            int num7 = itemCount / stackCount;
            int num8 = (int)((float)itemInc / (float)itemCount * (float)num7 + 0.5f);
            byte remainInc = (byte)num8;
            int num9 = OptimizedPlanet.InsertInto(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, insertIntoNeeds, insertOffset, itemId, (byte)num7, (byte)num8, out remainInc);
            if (num9 <= 0)
            {
                break;
            }
            if (remainInc == 0 && num9 == num7)
            {
                stackCount--;
            }
            itemCount -= (short)num9;
            itemInc -= (short)(num8 - remainInc);
            num3 = itemId;
            if (stackCount == 0)
            {
                itemId = 0;
                itemCount = 0;
                itemInc = 0;
                break;
            }
        }
        while (num-- > 0);
        if (flag || num3 > 0)
        {
            stage = OptimizedInserterStage.Sending;
        }
        else if (itemId > 0)
        {
            stage = OptimizedInserterStage.Inserting;
        }
        else
        {
            stage = ((stage == OptimizedInserterStage.Sending) ? OptimizedInserterStage.Returning : OptimizedInserterStage.Picking);
        }
    }

    private static OptimizedInserterStage ToOptimizedInserterStage(EInserterStage inserterStage) => inserterStage switch
    {
        EInserterStage.Picking => OptimizedInserterStage.Picking,
        EInserterStage.Sending => OptimizedInserterStage.Sending,
        EInserterStage.Inserting => OptimizedInserterStage.Inserting,
        EInserterStage.Returning => OptimizedInserterStage.Returning,
        _ => throw new ArgumentOutOfRangeException(nameof(inserterStage))
    };
}

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedInserter : IInserter<OptimizedInserter>
{
    public byte grade { get; }
    public readonly int pcId;
    public readonly bool careNeeds;
    public readonly short pickOffset;
    public readonly short insertOffset;
    public readonly int filter;
    public OptimizedInserterStage stage;
    public int speed; // Perhaps a constant at 10.000? Need to validate
    public int time;
    public int stt; // Probably not a constant but can probably be moved to inserterGrade. Need to validate
    public int itemId;
    public short itemCount;
    public short itemInc;
    public int stackCount;
    public int idleTick;

    public OptimizedInserter(ref readonly InserterComponent inserter, int grade)
    {
        grade = (byte)grade;
        pcId = inserter.pcId;
        careNeeds = inserter.careNeeds;
        pickOffset = inserter.pickOffset;
        insertOffset = inserter.insertOffset;
        filter = inserter.filter;
        stage = ToOptimizedInserterStage(inserter.stage);
        speed = inserter.speed;
        time = inserter.time;
        stt = inserter.stt;
        itemId = inserter.itemId;
        itemCount = inserter.itemCount;
        itemInc = inserter.itemInc;
        stackCount = inserter.stackCount;
        idleTick = inserter.idleTick;
    }

    public OptimizedInserter Create(ref readonly InserterComponent inserter, int grade)
    {
        return new OptimizedInserter(in inserter, grade);
    }

    public void Update(PlanetFactory planet,
                       OptimizedPlanet optimizedPlanet,
                       float power,
                       int inserterIndex,
                       ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                       ref readonly InserterConnections inserterConnections,
                       ref readonly int[] inserterConnectionNeeds,
                       InserterGrade inserterGrade)
    {
        if (power < 0.1f)
        {
            return;
        }
        switch (stage)
        {
            case OptimizedInserterStage.Picking:
                {
                    byte stack;
                    byte inc;
                    if (itemId == 0)
                    {
                        int num = 0;
                        if (careNeeds)
                        {
                            if (idleTick-- < 1)
                            {
                                int[] array = inserterConnectionNeeds;
                                if (array != null && (array[0] != 0 || array[1] != 0 || array[2] != 0 || array[3] != 0 || array[4] != 0 || array[5] != 0))
                                {
                                    num = OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, filter, array, out stack, out inc);
                                    if (num > 0)
                                    {
                                        itemId = num;
                                        itemCount += stack;
                                        itemInc += inc;
                                        stackCount++;
                                        time = 0;
                                    }
                                }
                                else
                                {
                                    idleTick = 9;
                                }
                            }
                        }
                        else
                        {
                            num = OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, filter, null, out stack, out inc);
                            if (num > 0)
                            {
                                itemId = num;
                                itemCount += stack;
                                itemInc += inc;
                                stackCount++;
                                time = 0;
                            }
                        }
                    }
                    else if (stackCount < inserterGrade.StackInput)
                    {
                        if (careNeeds)
                        {
                            if (idleTick-- < 1)
                            {
                                int[] array2 = inserterConnectionNeeds;
                                if (array2 != null && (array2[0] != 0 || array2[1] != 0 || array2[2] != 0 || array2[3] != 0 || array2[4] != 0 || array2[5] != 0))
                                {
                                    if (OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, itemId, array2, out stack, out inc) > 0)
                                    {
                                        itemCount += stack;
                                        itemInc += inc;
                                        stackCount++;
                                        time = 0;
                                    }
                                }
                                else
                                {
                                    idleTick = 10;
                                }
                            }
                        }
                        else if (OptimizedPlanet.PickFrom(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, pickOffset, itemId, null, out stack, out inc) > 0)
                        {
                            itemCount += stack;
                            itemInc += inc;
                            stackCount++;
                            time = 0;
                        }
                    }
                    if (itemId > 0)
                    {
                        time += speed;
                        if (stackCount == inserterGrade.StackInput || time >= inserterGrade.Delay)
                        {
                            time = (int)(power * (float)speed);
                            stage = OptimizedInserterStage.Sending;
                        }
                    }
                    else
                    {
                        time = 0;
                    }
                    break;
                }
            case OptimizedInserterStage.Sending:
                time += (int)(power * (float)speed);
                if (time >= stt)
                {
                    stage = OptimizedInserterStage.Inserting;
                    time -= stt;
                }
                if (itemId == 0)
                {
                    stage = OptimizedInserterStage.Returning;
                    time = stt - time;
                }
                break;
            case OptimizedInserterStage.Inserting:
                if (itemId == 0 || stackCount == 0)
                {
                    itemId = 0;
                    stackCount = 0;
                    itemCount = 0;
                    itemInc = 0;
                    time += (int)(power * (float)speed);
                    stage = OptimizedInserterStage.Returning;
                }
                else
                {
                    if (idleTick-- >= 1)
                    {
                        break;
                    }
                    TypedObjectIndex num2 = ((inserterGrade.StackOutput > 1) ? inserterConnections.InsertInto : default);
                    if (num2.EntityType == EntityType.Belt && num2.Index > 0)
                    {
                        int num3 = itemCount;
                        int num4 = itemInc;
                        planet.cargoTraffic.TryInsertItemToBeltWithStackIncreasement(num2.Index, insertOffset, itemId, inserterGrade.StackOutput, ref num3, ref num4);
                        itemCount = (short)num3;
                        itemInc = (short)num4;
                        stackCount = ((itemCount > 0) ? ((itemCount - 1) / 4 + 1) : 0);
                        if (stackCount == 0)
                        {
                            itemId = 0;
                            time += (int)(power * (float)speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                        break;
                    }
                    int[] array3 = inserterConnectionNeeds;
                    if (careNeeds)
                    {
                        if (array3 == null || (array3[0] == 0 && array3[1] == 0 && array3[2] == 0 && array3[3] == 0 && array3[4] == 0 && array3[5] == 0))
                        {
                            idleTick = 10;
                            break;
                        }
                    }
                    int num5 = itemCount / stackCount;
                    int num6 = (int)((float)itemInc / (float)itemCount * (float)num5 + 0.5f);
                    byte remainInc = (byte)num6;
                    int num7 = OptimizedPlanet.InsertInto(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, array3, insertOffset, itemId, (byte)num5, (byte)num6, out remainInc);
                    if (num7 > 0)
                    {
                        if (remainInc == 0 && num7 == num5)
                        {
                            stackCount--;
                        }
                        itemCount -= (short)num7;
                        itemInc -= (short)(num6 - remainInc);
                        if (stackCount == 0)
                        {
                            itemId = 0;
                            time += (int)(power * (float)speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                    }
                }
                break;
            case OptimizedInserterStage.Returning:
                time += (int)(power * (float)speed);
                if (time >= stt)
                {
                    stage = OptimizedInserterStage.Picking;
                    time = 0;
                }
                break;
        }
    }

    private static OptimizedInserterStage ToOptimizedInserterStage(EInserterStage inserterStage) => inserterStage switch
    {
        EInserterStage.Picking => OptimizedInserterStage.Picking,
        EInserterStage.Sending => OptimizedInserterStage.Sending,
        EInserterStage.Inserting => OptimizedInserterStage.Inserting,
        EInserterStage.Returning => OptimizedInserterStage.Returning,
        _ => throw new ArgumentOutOfRangeException(nameof(inserterStage))
    };
}