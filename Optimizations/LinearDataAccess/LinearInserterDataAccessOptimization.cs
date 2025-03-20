using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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
            InserterLinearAccessToPowerConsumers(planet, factory, power, cargoTraffic, transport, defenseSystem, digitalSystem);
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

internal sealed class OptimizedInserters
{
    private static readonly Dictionary<PlanetFactory, OptimizedInserters> _planetToOptimizedInserters = [];

    private int[] _inserterNetworkIds;
    private InserterState[] _inserterStates;
    private InserterConnections[] _inserterConnections;

    private int[] _assemblerNetworkIds;
    private AssemblerState[] _assemblerStates;

    private int[] _minerNetworkIds;

    private int[] _ejectorNetworkIds;

    private NetworkIdAndState<LabState>[] _labNetworkIdAndStates;

    [Flags]
    private enum InserterState
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
    private enum AssemblerState
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
    private enum LabState
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

    public static void EnableOptimization()
    {
        Harmony.CreateAndPatchAll(typeof(OptimizedInserters));
    }

    [HarmonyPriority(1)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    private static void LoadCurrentGame_Postfix()
    {
        WeaverFixes.Logger.LogMessage($"Initializing {nameof(OptimizedInserters)}");

        _planetToOptimizedInserters.Clear();

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            FactorySystem factory = planet.factorySystem;
            if (factory == null)
            {
                continue;
            }

            var optimizedInserters = new OptimizedInserters();
            optimizedInserters.InitializeData(planet);
            _planetToOptimizedInserters.Add(planet, optimizedInserters);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    private static void SaveCurrentGame_Prefix()
    {
        WeaverFixes.Logger.LogMessage($"Saving {nameof(OptimizedInserters)}");

        for (int i = 0; i < GameMain.data.factoryCount; i++)
        {
            PlanetFactory planet = GameMain.data.factories[i];
            if (planet == GameMain.localPlanet.factory)
            {
                continue;
            }

            if (_planetToOptimizedInserters.TryGetValue(planet, out OptimizedInserters optimizedInserters))
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
        int[] inserterNetworkIds = new int[planet.factorySystem.inserterCursor];
        InserterState[] inserterStates = new InserterState[planet.factorySystem.inserterCursor];
        InserterConnections[] inserterConnections = new InserterConnections[planet.factorySystem.inserterCursor];

        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            ref InserterComponent inserter = ref planet.factorySystem.inserterPool[i];
            if (inserter.id != i)
            {
                inserterStates[i] = InserterState.InactiveNoInserter;
                continue;
            }

            inserterNetworkIds[i] = planet.powerSystem.consumerPool[inserter.pcId].networkId;

            InserterState? inserterState = null;
            TypedObjectIndex pickFrom = new TypedObjectIndex(EntityType.None, 0);
            if (inserter.pickTarget != 0)
            {
                pickFrom = GetAsTypedObjectIndex(inserter.pickTarget, planet.entityPool);
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
            }

            TypedObjectIndex insertInto = new TypedObjectIndex(EntityType.None, 0);
            if (inserter.insertTarget != 0)
            {
                insertInto = GetAsTypedObjectIndex(inserter.insertTarget, planet.entityPool);
            }
            else
            {
                inserterState = InserterState.InactiveNotCompletelyConnected;

                // Done in inserter update so doing it here for the same condition since
                // inserter will not run when inactive
                planet.entitySignPool[inserter.entityId].signType = 10u;
            }

            inserterStates[i] = inserterState ?? InserterState.Active;
            inserterConnections[i] = new InserterConnections(pickFrom, insertInto);

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
                inserter.delay = delay;
                inserter.stackInput = b;
                inserter.stackOutput = 1;
                inserter.bidirectional = false;
            }
            else if (inserter.grade == 4)
            {
                inserter.delay = delay2;
                inserter.stackInput = b2;
                inserter.stackOutput = stackOutput;
                inserter.bidirectional = inserterBidirectional;
            }
            else
            {
                inserter.delay = 0;
                inserter.stackInput = 1;
                inserter.stackOutput = 1;
                inserter.bidirectional = false;
            }
        }

        _inserterNetworkIds = inserterNetworkIds;
        _inserterStates = inserterStates;
        _inserterConnections = inserterConnections;
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
        if (__instance.inserterFactories == null)
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }
        int num = 0;
        for (int i = 0; i < __instance.inserterFactoryCnt; i++)
        {
            num += __instance.inserterFactories[i].factorySystem.inserterCursor;
        }
        int minimumMissionCnt = 64;
        if (!WorkerThreadExecutor.CalculateMissionIndex(num, __instance.usedThreadCnt, __instance.curThreadIdx, minimumMissionCnt, out var _start, out var _end))
        {
            return HarmonyConstants.SKIP_ORIGINAL_METHOD;
        }
        int num2 = 0;
        int num3 = 0;
        for (int j = 0; j < __instance.inserterFactoryCnt; j++)
        {
            int num4 = num3 + __instance.inserterFactories[j].factorySystem.inserterCursor;
            if (num4 <= _start)
            {
                num3 = num4;
                continue;
            }
            num2 = j;
            break;
        }
        for (int k = num2; k < __instance.inserterFactoryCnt; k++)
        {
            bool isActive = __instance.inserterLocalPlanet == __instance.inserterFactories[k].planet;
            int num5 = _start - num3;
            int num6 = _end - num3;
            if (_end - _start > __instance.inserterFactories[k].factorySystem.inserterCursor - num5)
            {
                try
                {
                    if (!isActive)
                    {
                        PlanetFactory planet = __instance.inserterFactories[k];
                        OptimizedInserters optimizedInserters = _planetToOptimizedInserters[planet];
                        optimizedInserters.GameTickInserters(planet, __instance.inserterTime, isActive, num5, __instance.inserterFactories[k].factorySystem.inserterCursor);
                    }
                    else
                    {
                        __instance.inserterFactories[k].factorySystem.GameTickInserters(__instance.inserterTime, isActive, num5, __instance.inserterFactories[k].factorySystem.inserterCursor);
                    }
                    num3 += __instance.inserterFactories[k].factorySystem.inserterCursor;
                    _start = num3;
                }
                catch (Exception ex)
                {
                    __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + k.ToString() + " Inserter first gametick total cursor: " + __instance.inserterFactories[k].factorySystem.inserterCursor + "  Start & End: " + num5 + "/" + __instance.inserterFactories[k].factorySystem.inserterCursor + "  " + ex;
                    __instance.hasErrorMessage = true;
                }
                continue;
            }
            try
            {
                if (!isActive)
                {
                    PlanetFactory planet = __instance.inserterFactories[k];
                    OptimizedInserters optimizedInserters = _planetToOptimizedInserters[planet];
                    optimizedInserters.GameTickInserters(planet, __instance.inserterTime, isActive, num5, num6);
                }
                else
                {
                    __instance.inserterFactories[k].factorySystem.GameTickInserters(__instance.inserterTime, isActive, num5, num6);
                }
                break;
            }
            catch (Exception ex2)
            {
                __instance.errorMessage = "Thread Error Exception!!! Thread idx:" + __instance.curThreadIdx + " Inserter Factory idx:" + k.ToString() + " Inserter second gametick total cursor: " + __instance.inserterFactories[k].factorySystem.inserterCursor + "  Start & End: " + num5 + "/" + num6 + "  " + ex2;
                __instance.hasErrorMessage = true;
                break;
            }
        }

        return HarmonyConstants.SKIP_ORIGINAL_METHOD;
    }

    public void GameTickInserters(PlanetFactory planet, long time, bool isActive, int _start, int _end)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        int[][] entityNeeds = planet.entityNeeds;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = planet.entityPool;
        BeltComponent[] beltPool = planet.cargoTraffic.beltPool;
        _start = ((_start == 0) ? 1 : _start);
        _end = ((_end > planet.factorySystem.inserterCursor) ? planet.factorySystem.inserterCursor : _end);
        for (int j = _start; j < _end; j++)
        {
            InserterState inserterState = _inserterStates[j];
            if (inserterState != InserterState.Active)
            {
                if (inserterState == InserterState.InactiveNoInserter ||
                    inserterState == InserterState.InactiveNotCompletelyConnected)
                {
                    continue;
                }
                else if (inserterState == InserterState.InactivePickFrom)
                {
                    if (!IsObjectPickFromActive(j))
                    {
                        continue;
                    }

                    _inserterStates[j] = InserterState.Active;
                }
                else if (inserterState == InserterState.InactiveInsertInto)
                {
                    if (!IsObjectInsertIntoActive(j))
                    {
                        continue;
                    }

                    _inserterStates[j] = InserterState.Active;
                }
            }

            ref InserterComponent reference2 = ref planet.factorySystem.inserterPool[j];
            float power2 = networkServes[_inserterNetworkIds[j]];
            if (reference2.bidirectional)
            {
                InternalUpdate_Bidirectional(planet, ref reference2, entityNeeds, entityAnimPool, power2, isActive);
            }
            else
            {
                reference2.InternalUpdateNoAnim(planet, entityNeeds, power2);
            }
        }
    }

    private void InternalUpdate_Bidirectional(PlanetFactory planet, ref InserterComponent inserter, int[][] needsPool, AnimData[] animPool, float power, bool isActive)
    {
        if (isActive)
        {
            animPool[inserter.entityId].power = power;
        }
        if (power < 0.1f)
        {
            // Not sure it is worth optimizing low power since it should be a rare occurrence in a large factory
            _inserterStates[inserter.id] = InserterState.Active;
            return;
        }
        bool flag = false;
        int num = 1;
        do
        {
            if (inserter.pickTarget == 0)
            {
                planet.entitySignPool[inserter.entityId].signType = 10u;
                break;
            }
            if (inserter.insertTarget == 0)
            {
                planet.entitySignPool[inserter.entityId].signType = 10u;
                break;
            }
            byte stack;
            byte inc;
            if (inserter.itemId == 0)
            {
                int num2 = 0;
                if (inserter.careNeeds)
                {
                    if (inserter.idleTick-- < 1)
                    {
                        int[] array = needsPool[inserter.insertTarget];
                        if (array != null && (array[0] != 0 || array[1] != 0 || array[2] != 0 || array[3] != 0 || array[4] != 0 || array[5] != 0))
                        {
                            num2 = PickFrom(planet, inserter.id, inserter.pickTarget, inserter.pickOffset, inserter.filter, array, out stack, out inc);
                            if (num2 > 0)
                            {
                                inserter.itemId = num2;
                                inserter.itemCount += stack;
                                inserter.itemInc += inc;
                                inserter.stackCount++;
                                flag = true;
                            }
                            else
                            {
                                num = 0;
                            }
                        }
                        else
                        {
                            inserter.idleTick = 9;
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
                    num2 = PickFrom(planet, inserter.id, inserter.pickTarget, inserter.pickOffset, inserter.filter, null, out stack, out inc);
                    if (num2 > 0)
                    {
                        inserter.itemId = num2;
                        inserter.itemCount += stack;
                        inserter.itemInc += inc;
                        inserter.stackCount++;
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
                if (inserter.stackCount >= inserter.stackInput)
                {
                    continue;
                }
                if (inserter.filter == 0 || inserter.filter == inserter.itemId)
                {
                    if (inserter.careNeeds)
                    {
                        if (inserter.idleTick-- < 1)
                        {
                            int[] array2 = needsPool[inserter.insertTarget];
                            if (array2 != null && (array2[0] != 0 || array2[1] != 0 || array2[2] != 0 || array2[3] != 0 || array2[4] != 0 || array2[5] != 0))
                            {
                                int itemId = PickFrom(planet, inserter.id, inserter.pickTarget, inserter.pickOffset, inserter.itemId, array2, out stack, out inc);
                                if (itemId > 0)
                                {
                                    inserter.itemCount += stack;
                                    inserter.itemInc += inc;
                                    inserter.stackCount++;
                                    flag = true;
                                    _inserterStates[inserter.id] = InserterState.Active;
                                }
                                else
                                {
                                    num = 0;
                                }
                            }
                            else
                            {
                                inserter.idleTick = 10;
                                num = 0;
                            }
                        }
                        else
                        {
                            num = 0;
                        }
                    }
                    else if (PickFrom(planet, inserter.id, inserter.pickTarget, inserter.pickOffset, inserter.itemId, null, out stack, out inc) > 0)
                    {
                        inserter.itemCount += stack;
                        inserter.itemInc += inc;
                        inserter.stackCount++;
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
            if (inserter.insertTarget == 0)
            {
                planet.entitySignPool[inserter.entityId].signType = 10u;
                break;
            }
            if (inserter.itemId == 0 || inserter.stackCount == 0)
            {
                inserter.itemId = 0;
                inserter.stackCount = 0;
                inserter.itemCount = 0;
                inserter.itemInc = 0;
                break;
            }
            if (inserter.idleTick-- >= 1)
            {
                break;
            }
            TypedObjectIndex num4 = ((inserter.stackOutput > 1) ? _inserterConnections[inserter.id].InsertInto : default);
            if (num4.EntityType == EntityType.Belt && num4.Index > 0)
            {
                int num5 = inserter.itemCount;
                int num6 = inserter.itemInc;
                planet.cargoTraffic.TryInsertItemToBeltWithStackIncreasement(num4.Index, inserter.insertOffset, inserter.itemId, inserter.stackOutput, ref num5, ref num6);
                if (num5 < inserter.itemCount)
                {
                    num3 = inserter.itemId;
                }
                inserter.itemCount = (short)num5;
                inserter.itemInc = (short)num6;
                inserter.stackCount = ((inserter.itemCount > 0) ? ((inserter.itemCount - 1) / 4 + 1) : 0);
                if (inserter.stackCount == 0)
                {
                    inserter.itemId = 0;
                    inserter.itemCount = 0;
                    inserter.itemInc = 0;
                    break;
                }
                num = 0;
                continue;
            }
            if (inserter.careNeeds)
            {
                int[] array3 = needsPool[inserter.insertTarget];
                if (array3 == null || (array3[0] == 0 && array3[1] == 0 && array3[2] == 0 && array3[3] == 0 && array3[4] == 0 && array3[5] == 0))
                {
                    inserter.idleTick = 10;
                    break;
                }
            }
            int num7 = inserter.itemCount / inserter.stackCount;
            int num8 = (int)((float)inserter.itemInc / (float)inserter.itemCount * (float)num7 + 0.5f);
            byte remainInc = (byte)num8;
            int num9 = InsertInto(planet, inserter.id, inserter.insertTarget, inserter.insertOffset, inserter.itemId, (byte)num7, (byte)num8, out remainInc);
            if (num9 <= 0)
            {
                break;
            }
            if (remainInc == 0 && num9 == num7)
            {
                inserter.stackCount--;
            }
            inserter.itemCount -= (short)num9;
            inserter.itemInc -= (short)(num8 - remainInc);
            num3 = inserter.itemId;
            if (inserter.stackCount == 0)
            {
                inserter.itemId = 0;
                inserter.itemCount = 0;
                inserter.itemInc = 0;
                break;
            }
        }
        while (num-- > 0);
        if (flag || num3 > 0)
        {
            inserter.stage = EInserterStage.Sending;
        }
        else if (inserter.itemId > 0)
        {
            inserter.stage = EInserterStage.Inserting;
        }
        else
        {
            inserter.stage = ((inserter.stage == EInserterStage.Sending) ? EInserterStage.Returning : EInserterStage.Picking);
        }
        if (isActive)
        {
            ref AnimData reference = ref animPool[inserter.entityId];
            if (num3 > 0)
            {
                reference.prepare_length = 1f;
            }
            else
            {
                reference.prepare_length *= 0.7f;
            }
            reference.time += power * 0.125f * reference.prepare_length;
            if (reference.time > 0.5f)
            {
                reference.time -= 0.5f;
            }
            int num10 = ((inserter.itemId > 0) ? inserter.itemId : num3);
            if (num10 > 0 || reference.prepare_length < 0.5f)
            {
                reference.state = (uint)num10;
            }
        }
    }

    private int PickFrom(PlanetFactory planet, int inserterId, int entityId, int offset, int filter, int[] needs, out byte stack, out byte inc)
    {
        stack = 1;
        inc = 0;
        TypedObjectIndex typedObjectIndex = _inserterConnections[inserterId].PickFrom;
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
            AssemblerState assemblerState = _assemblerStates[objectIndex];
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveOutputFull)
            {
                _inserterStates[inserterId] = InserterState.InactivePickFrom;
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
                            _assemblerStates[objectIndex] = AssemblerState.Active;
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
                            _assemblerStates[objectIndex] = AssemblerState.Active;
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
                            _assemblerStates[objectIndex] = AssemblerState.Active;
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
                                    _assemblerStates[objectIndex] = AssemblerState.Active;
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
            lock (planet.entityMutexs[entityId])
            {
                int bulletId = planet.factorySystem.ejectorPool[objectIndex].bulletId;
                int bulletCount = planet.factorySystem.ejectorPool[objectIndex].bulletCount;
                if (bulletId > 0 && bulletCount > 5 && (filter == 0 || filter == bulletId) && (needs == null || needs[0] == bulletId || needs[1] == bulletId || needs[2] == bulletId || needs[3] == bulletId || needs[4] == bulletId || needs[5] == bulletId))
                {
                    planet.factorySystem.ejectorPool[objectIndex].TakeOneBulletUnsafe(out inc);
                    return bulletId;
                }
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Silo)
        {
            lock (planet.entityMutexs[entityId])
            {
                int bulletId2 = planet.factorySystem.siloPool[objectIndex].bulletId;
                int bulletCount2 = planet.factorySystem.siloPool[objectIndex].bulletCount;
                if (bulletId2 > 0 && bulletCount2 > 1 && (filter == 0 || filter == bulletId2) && (needs == null || needs[0] == bulletId2 || needs[1] == bulletId2 || needs[2] == bulletId2 || needs[3] == bulletId2 || needs[4] == bulletId2 || needs[5] == bulletId2))
                {
                    planet.factorySystem.siloPool[objectIndex].TakeOneBulletUnsafe(out inc);
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
                lock (planet.entityMutexs[entityId])
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
            int inc2;
            if (offset > 0 && planet.powerSystem.genPool[offset].id == offset)
            {
                lock (planet.entityMutexs[entityId])
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

    private int InsertInto(PlanetFactory planet, int inserterId, int entityId, int offset, int itemId, byte itemCount, byte itemInc, out byte remainInc)
    {
        remainInc = itemInc;
        TypedObjectIndex typedObjectIndex = _inserterConnections[inserterId].InsertInto;
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
            AssemblerState assemblerState = _assemblerStates[objectIndex];
            if (assemblerState != AssemblerState.Active &&
                assemblerState != AssemblerState.InactiveInputMissing)
            {
                _inserterStates[inserterId] = InserterState.InactiveInsertInto;
                return 0;
            }

            int[] array = planet.entityNeeds[entityId];
            if (array == null)
            {
                throw new InvalidOperationException($"Array from {nameof(planet.entityNeeds)} should only be null if assembler is inactive which the above if statement should have caught.");
            }
            ref AssemblerComponent reference = ref planet.factorySystem.assemblerPool[objectIndex];
            int[] requires = reference.requires;
            int num = requires.Length;
            if (0 < num && requires[0] == itemId)
            {
                Interlocked.Add(ref reference.served[0], itemCount);
                Interlocked.Add(ref reference.incServed[0], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (1 < num && requires[1] == itemId)
            {
                Interlocked.Add(ref reference.served[1], itemCount);
                Interlocked.Add(ref reference.incServed[1], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (2 < num && requires[2] == itemId)
            {
                Interlocked.Add(ref reference.served[2], itemCount);
                Interlocked.Add(ref reference.incServed[2], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (3 < num && requires[3] == itemId)
            {
                Interlocked.Add(ref reference.served[3], itemCount);
                Interlocked.Add(ref reference.incServed[3], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (4 < num && requires[4] == itemId)
            {
                Interlocked.Add(ref reference.served[4], itemCount);
                Interlocked.Add(ref reference.incServed[4], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            if (5 < num && requires[5] == itemId)
            {
                Interlocked.Add(ref reference.served[5], itemCount);
                Interlocked.Add(ref reference.incServed[5], itemInc);
                remainInc = 0;
                _assemblerStates[objectIndex] = AssemblerState.Active;
                return itemCount;
            }
            return 0;
        }
        else if (typedObjectIndex.EntityType == EntityType.Ejector)
        {
            int[] array = planet.entityNeeds[entityId];
            if (array == null)
            {
                return 0;
            }
            if (array[0] == itemId && planet.factorySystem.ejectorPool[objectIndex].bulletId == itemId)
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
            int[] array = planet.entityNeeds[entityId];
            if (array == null)
            {
                return 0;
            }
            if (array[0] == itemId && planet.factorySystem.siloPool[objectIndex].bulletId == itemId)
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
            int[] array = planet.entityNeeds[entityId];
            if (array == null)
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
            int[] array = planet.entityNeeds[entityId];
            if (array == null)
            {
                return 0;
            }
            StationComponent stationComponent = planet.transport.stationPool[objectIndex];
            if (itemId == 1210 && stationComponent.warperCount < stationComponent.warperMaxCount)
            {
                lock (planet.entityMutexs[entityId])
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
            for (int j = 0; j < array.Length && j < storage.Length; j++)
            {
                if (array[j] == itemId && storage[j].itemId == itemId)
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
            int[] array = planet.entityNeeds[entityId];
            PowerGeneratorComponent[] genPool = planet.powerSystem.genPool;
            lock (planet.entityMutexs[entityId])
            {
                if (itemId == genPool[objectIndex].fuelId)
                {
                    if (genPool[objectIndex].fuelCount < 10)
                    {
                        ref short fuelCount = ref genPool[objectIndex].fuelCount;
                        fuelCount += itemCount;
                        ref short fuelInc = ref genPool[objectIndex].fuelInc;
                        fuelInc += itemInc;
                        remainInc = 0;
                        return itemCount;
                    }
                    return 0;
                }
                if (genPool[objectIndex].fuelId == 0)
                {
                    array = ItemProto.fuelNeeds[genPool[objectIndex].fuelMask];
                    if (array == null || array.Length == 0)
                    {
                        return 0;
                    }
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (array[k] == itemId)
                        {
                            genPool[objectIndex].SetNewFuel(itemId, itemCount, itemInc);
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

    private bool IsObjectPickFromActive(int inserterId)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterId].PickFrom;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return _assemblerStates[objectIndex.Index] == AssemblerState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if pick from is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
    }

    private bool IsObjectInsertIntoActive(int inserterId)
    {
        TypedObjectIndex objectIndex = _inserterConnections[inserterId].InsertInto;
        if (objectIndex.EntityType == EntityType.Assembler)
        {
            return _assemblerStates[objectIndex.Index] == AssemblerState.Active;
        }
        else
        {
            throw new InvalidOperationException($"Check if insert into is active does currently not support entity type of type: {objectIndex.EntityType}");
        }
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
                        OptimizedInserters optimizedInserters = _planetToOptimizedInserters[planet];
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
                        OptimizedInserters optimizedInserters = _planetToOptimizedInserters[planet];
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
    private static TypedObjectIndex GetAsTypedObjectIndex(int index, EntityData[] entities)
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

    private readonly struct InserterConnections
    {
        public readonly TypedObjectIndex PickFrom;
        public readonly TypedObjectIndex InsertInto;

        public InserterConnections(TypedObjectIndex pickFrom, TypedObjectIndex insertInto)
        {
            PickFrom = pickFrom;
            InsertInto = insertInto;
        }
    }

    private record struct PickFromResult(int ItemId, InserterState InserterState);
    private record struct InsertIntoResult(int ItemCount, InserterState InserterState);
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