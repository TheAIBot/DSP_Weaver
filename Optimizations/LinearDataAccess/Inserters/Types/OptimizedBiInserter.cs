using System;
using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Inserters.Types;

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
                       PickFromProducingPlant[] pickFromProducingPlants,
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
                            num2 = OptimizedPlanet.PickFrom(planet,
                                                            optimizedPlanet,
                                                            ref inserterNetworkIdAndState,
                                                            in inserterConnections,
                                                            inserterIndex,
                                                            pickFromProducingPlants,
                                                            pickOffset,
                                                            filter,
                                                            array,
                                                            out stack,
                                                            out inc);
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
                    num2 = OptimizedPlanet.PickFrom(planet,
                                                    optimizedPlanet,
                                                    ref inserterNetworkIdAndState,
                                                    in inserterConnections,
                                                    inserterIndex,
                                                    pickFromProducingPlants,
                                                    pickOffset,
                                                    filter,
                                                    null,
                                                    out stack,
                                                    out inc);
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
                                int num44 = OptimizedPlanet.PickFrom(planet,
                                                                     optimizedPlanet,
                                                                     ref inserterNetworkIdAndState,
                                                                     in inserterConnections,
                                                                     inserterIndex,
                                                                     pickFromProducingPlants,
                                                                     pickOffset,
                                                                     itemId,
                                                                     array2,
                                                                     out stack,
                                                                     out inc);
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
                    else if (OptimizedPlanet.PickFrom(planet,
                                                      optimizedPlanet,
                                                      ref inserterNetworkIdAndState,
                                                      in inserterConnections,
                                                      inserterIndex,
                                                      pickFromProducingPlants,
                                                      pickOffset,
                                                      itemId,
                                                      null,
                                                      out stack,
                                                      out inc) > 0)
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
            TypedObjectIndex num4 = inserterGrade.StackOutput > 1 ? inserterConnections.InsertInto : default;
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
                stackCount = itemCount > 0 ? (itemCount - 1) / 4 + 1 : 0;
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

            int[] insertIntoNeeds = inserterConnectionNeeds;
            if (careNeeds)
            {

                if (insertIntoNeeds == null || insertIntoNeeds[0] == 0 && insertIntoNeeds[1] == 0 && insertIntoNeeds[2] == 0 && insertIntoNeeds[3] == 0 && insertIntoNeeds[4] == 0 && insertIntoNeeds[5] == 0)
                {
                    idleTick = 10;
                    break;
                }
            }
            int num7 = itemCount / stackCount;
            int num8 = (int)(itemInc / (float)itemCount * num7 + 0.5f);
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
            stage = stage == OptimizedInserterStage.Sending ? OptimizedInserterStage.Returning : OptimizedInserterStage.Picking;
        }
    }

    public long GetPowerConsumption(PowerConsumerType powerConsumerType)
    {
        return powerConsumerType.GetRequiredEnergy(stage == OptimizedInserterStage.Sending || stage == OptimizedInserterStage.Returning);
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