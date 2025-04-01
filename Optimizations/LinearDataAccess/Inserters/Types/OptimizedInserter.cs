using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Inserters.Types;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedInserter : IInserter<OptimizedInserter>
{
    public byte grade { get; }
    public readonly bool careNeeds;
    public readonly short pickOffset;
    public readonly short insertOffset;
    public readonly int filter;
    public int speed; // Perhaps a constant at 10.000? Need to validate
    public int time;
    public int stt; // Probably not a constant but can probably be moved to inserterGrade. Need to validate
    public int itemId;
    public short itemCount;
    public short itemInc;
    public int stackCount;
    public int idleTick;

    public OptimizedInserter(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        grade = (byte)grade;
        careNeeds = inserter.careNeeds;
        pickOffset = (short)pickFromOffset;
        insertOffset = (short)insertIntoOffset;
        filter = inserter.filter;
        speed = inserter.speed;
        time = inserter.time;
        stt = inserter.stt;
        itemId = inserter.itemId;
        itemCount = inserter.itemCount;
        itemInc = inserter.itemInc;
        stackCount = inserter.stackCount;
        idleTick = inserter.idleTick;
    }

    public OptimizedInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return new OptimizedInserter(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public void Update(PlanetFactory planet,
                       OptimizedPlanet optimizedPlanet,
                       float power,
                       int inserterIndex,
                       ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                       ref readonly InserterConnections inserterConnections,
                       ref readonly int[] inserterConnectionNeeds,
                       PickFromProducingPlant[] pickFromProducingPlants,
                       InserterGrade inserterGrade,
                       ref OptimizedInserterStage stage,
                       ref readonly ConnectionBelts connectionBelts)
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
                                    num = OptimizedPlanet.PickFrom(planet,
                                                                   optimizedPlanet,
                                                                   ref inserterNetworkIdAndState,
                                                                   in inserterConnections,
                                                                   in connectionBelts,
                                                                   inserterIndex,
                                                                   pickFromProducingPlants,
                                                                   pickOffset,
                                                                   filter,
                                                                   array,
                                                                   out stack,
                                                                   out inc);
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
                            num = OptimizedPlanet.PickFrom(planet,
                                                           optimizedPlanet,
                                                           ref inserterNetworkIdAndState,
                                                           in inserterConnections,
                                                           in connectionBelts,
                                                           inserterIndex,
                                                           pickFromProducingPlants,
                                                           pickOffset,
                                                           filter,
                                                           null,
                                                           out stack,
                                                           out inc);
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
                                    if (OptimizedPlanet.PickFrom(planet,
                                                                 optimizedPlanet,
                                                                 ref inserterNetworkIdAndState,
                                                                 in inserterConnections,
                                                                 in connectionBelts,
                                                                 inserterIndex,
                                                                 pickFromProducingPlants,
                                                                 pickOffset,
                                                                 itemId,
                                                                 array2,
                                                                 out stack,
                                                                 out inc) > 0)
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
                        else if (OptimizedPlanet.PickFrom(planet,
                                                          optimizedPlanet,
                                                          ref inserterNetworkIdAndState,
                                                          in inserterConnections,
                                                          in connectionBelts,
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
                            time = 0;
                        }
                    }
                    if (itemId > 0)
                    {
                        time += speed;
                        if (stackCount == inserterGrade.StackInput || time >= inserterGrade.Delay)
                        {
                            time = (int)(power * speed);
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
                time += (int)(power * speed);
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
                    time += (int)(power * speed);
                    stage = OptimizedInserterStage.Returning;
                }
                else
                {
                    if (idleTick-- >= 1)
                    {
                        break;
                    }
                    TypedObjectIndex num2 = inserterGrade.StackOutput > 1 ? inserterConnections.InsertInto : default;
                    if (num2.EntityType == EntityType.Belt && num2.Index > 0)
                    {
                        int num3 = itemCount;
                        int num4 = itemInc;
                        connectionBelts.InsertInto.TryInsertItemWithStackIncreasement(insertOffset, itemId, inserterGrade.StackOutput, ref num3, ref num4);
                        itemCount = (short)num3;
                        itemInc = (short)num4;
                        stackCount = itemCount > 0 ? (itemCount - 1) / 4 + 1 : 0;
                        if (stackCount == 0)
                        {
                            itemId = 0;
                            time += (int)(power * speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                        break;
                    }
                    int[] array3 = inserterConnectionNeeds;
                    if (careNeeds)
                    {
                        if (array3 == null || array3[0] == 0 && array3[1] == 0 && array3[2] == 0 && array3[3] == 0 && array3[4] == 0 && array3[5] == 0)
                        {
                            idleTick = 10;
                            break;
                        }
                    }
                    int num5 = itemCount / stackCount;
                    int num6 = (int)(itemInc / (float)itemCount * num5 + 0.5f);
                    byte remainInc = (byte)num6;
                    int num7 = OptimizedPlanet.InsertInto(planet, optimizedPlanet, ref inserterNetworkIdAndState, in inserterConnections, in connectionBelts, array3, insertOffset, itemId, (byte)num5, (byte)num6, out remainInc);
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
                            time += (int)(power * speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                    }
                }
                break;
            case OptimizedInserterStage.Returning:
                time += (int)(power * speed);
                if (time >= stt)
                {
                    stage = OptimizedInserterStage.Picking;
                    time = 0;
                }
                break;
        }
    }
}