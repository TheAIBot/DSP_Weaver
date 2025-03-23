using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess;

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