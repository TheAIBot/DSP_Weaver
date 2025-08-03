using System;
using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.NeedsSystem;

namespace Weaver.Optimizations.LinearDataAccess.Inserters.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedInserter : IInserter<OptimizedInserter>
{
    public byte grade { get; }
    private readonly bool careNeeds;
    private readonly int pickOffset;
    private readonly int insertOffset;
    private readonly int filter;
    private readonly int speed; // Perhaps a constant at 10.000? Need to validate
    private readonly int stt; // Probably not a constant but can probably be moved to inserterGrade. Need to validate
    private int time;
    private int itemId;
    private short itemCount;
    private short itemInc;
    private int stackCount;
    private int idleTick;

    public OptimizedInserter(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        if (grade < 0 || grade > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(grade), $"{nameof(grade)} was not within the bounds of a byte. Value: {grade}");
        }

        this.grade = (byte)grade;
        careNeeds = inserter.careNeeds;
        pickOffset = pickFromOffset;
        insertOffset = insertIntoOffset;
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

    public readonly OptimizedInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return new OptimizedInserter(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public void Update(PlanetFactory planet,
                       InserterExecutor<OptimizedInserter> inserterExecutor,
                       float power,
                       int inserterIndex,
                       ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                       InserterGrade inserterGrade,
                       ref OptimizedInserterStage stage,
                       InserterConnections[] insertersConnections,
                       ref readonly SubFactoryNeeds subFactoryNeeds)
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
                        if (careNeeds)
                        {
                            if (idleTick-- < 1)
                            {
                                if (OptimizedBiInserter.IsNeedsNotEmpty(insertersConnections,
                                                                        in subFactoryNeeds,
                                                                        inserterIndex,
                                                                        out InserterConnections inserterConnections,
                                                                        out GroupNeeds groupNeeds))
                                {
                                    int num = inserterExecutor.PickFrom(planet,
                                                                        ref inserterNetworkIdAndState,
                                                                        inserterIndex,
                                                                        pickOffset,
                                                                        filter,
                                                                        inserterConnections,
                                                                        groupNeeds,
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
                            InserterConnections inserterConnections = insertersConnections[inserterIndex];
                            int num = inserterExecutor.PickFrom(planet,
                                                                ref inserterNetworkIdAndState,
                                                                inserterIndex,
                                                                pickOffset,
                                                                filter,
                                                                inserterConnections,
                                                                default,
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
                                if (OptimizedBiInserter.IsNeedsNotEmpty(insertersConnections,
                                                                        in subFactoryNeeds,
                                                                        inserterIndex,
                                                                        out InserterConnections inserterConnections,
                                                                        out GroupNeeds groupNeeds))
                                {
                                    if (inserterExecutor.PickFrom(planet,
                                                                  ref inserterNetworkIdAndState,
                                                                  inserterIndex,
                                                                  pickOffset,
                                                                  itemId,
                                                                  inserterConnections,
                                                                  groupNeeds,
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
                        else
                        {
                            InserterConnections inserterConnections = insertersConnections[inserterIndex];
                            if (inserterExecutor.PickFrom(planet,
                                                           ref inserterNetworkIdAndState,
                                                           inserterIndex,
                                                           pickOffset,
                                                           itemId,
                                                           inserterConnections,
                                                           default,
                                                           out stack,
                                                           out inc) > 0)
                            {
                                itemCount += stack;
                                itemInc += inc;
                                stackCount++;
                                time = 0;
                            }
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

                    InserterConnections inserterConnections = insertersConnections[inserterIndex];
                    TypedObjectIndex num2 = inserterGrade.StackOutput > 1 ? inserterConnections.InsertInto : default;
                    if (num2.EntityType == EntityType.Belt)
                    {
                        int num3 = itemCount;
                        int num4 = itemInc;
                        ConnectionBelts connectionBelts = inserterExecutor._connectionBelts[inserterIndex];
                        if (connectionBelts.InsertInto == null)
                        {
                            throw new InvalidOperationException($"{nameof(connectionBelts.InsertInto)} was null.");
                        }

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

                    GroupNeeds groupNeeds = default;
                    if (careNeeds)
                    {
                        if (OptimizedBiInserter.IsNeedsEmpty(in subFactoryNeeds,
                                                             inserterConnections,
                                                             out groupNeeds))
                        {
                            idleTick = 10;
                            break;
                        }
                    }
                    int num5 = itemCount / stackCount;
                    int num6 = (int)(itemInc / (float)itemCount * num5 + 0.5f);
                    byte remainInc;
                    int num7 = inserterExecutor.InsertInto(planet,
                                                           ref inserterNetworkIdAndState,
                                                           inserterIndex,
                                                           inserterConnections,
                                                           groupNeeds,
                                                           insertOffset,
                                                           itemId,
                                                           (byte)num5,
                                                           (byte)num6,
                                                           out remainInc);
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

    public readonly void Save(ref InserterComponent inserter, EInserterStage inserterStage)
    {
        inserter.speed = speed;
        inserter.time = time;
        inserter.stt = stt;
        inserter.itemId = itemId;
        inserter.itemCount = itemCount;
        inserter.itemInc = itemInc;
        inserter.stackCount = stackCount;
        inserter.idleTick = idleTick;
        inserter.stage = inserterStage;
    }
}