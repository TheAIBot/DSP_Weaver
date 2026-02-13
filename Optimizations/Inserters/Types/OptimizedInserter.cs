using System;
using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.Inserters.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct InserterGrade : IInserterGrade<InserterGrade>, IMemorySize
{
    public readonly int Delay;
    public readonly int Stt;
    public readonly short Filter;
    public readonly byte StackInput;
    public readonly byte StackOutput;
    public readonly bool CareNeeds;

    public InserterGrade(int delay, byte stackInput, byte stackOutput, bool careNeeds, int filter, int stt)
    {
        if (filter < 0 || filter > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), $"{nameof(filter)} was not within the bounds of a short. Value: {filter}");
        }

        Delay = delay;
        StackInput = stackInput;
        StackOutput = stackOutput;
        CareNeeds = careNeeds;
        Filter = (short)filter;
        Stt = stt;
    }

    public readonly InserterGrade Create(ref InserterComponent inserter)
    {
        byte b = (byte)GameMain.history.inserterStackCountObsolete;
        byte b2 = (byte)GameMain.history.inserterStackInput;
        byte stackOutput = (byte)GameMain.history.inserterStackOutput;
        int delay = b > 1 ? 110000 : 0;
        int delay2 = b2 > 1 ? 40000 : 0;

        if (inserter.grade == 3)
        {
            return new InserterGrade(delay, b, 1, inserter.careNeeds, inserter.filter, inserter.stt);
        }
        else if (inserter.grade == 4)
        {
            return new InserterGrade(delay2, b2, stackOutput, inserter.careNeeds, inserter.filter, inserter.stt);
        }
        else
        {
            return new InserterGrade(0, 1, 1, inserter.careNeeds, inserter.filter, inserter.stt);
        }
    }

    public unsafe int GetSize() => Marshal.SizeOf<InserterGrade>();

    public readonly bool Equals(InserterGrade other)
    {
        return Delay == other.Delay &&
               StackInput == other.StackInput &&
               StackOutput == other.StackOutput &&
               CareNeeds == other.CareNeeds &&
               Filter == other.Filter &&
               Stt == other.Stt;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is InserterGrade inserterGrade && Equals(inserterGrade);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Delay, StackInput, StackOutput, CareNeeds, Filter, Stt);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedInserter : IInserter<OptimizedInserter, InserterGrade>
{
    public short grade { get; }
    public short pickOffset { get; }
    public short insertOffset { get; }
    private int time;
    private short itemId;
    private short itemCount;
    private short itemInc;
    private int stackCount;
    private int idleTick;

    public OptimizedInserter(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        if (grade < 0 || grade > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(grade), $"{nameof(grade)} was not within the bounds of a short. Value: {grade}");
        }

        if (inserter.itemId < 0 || inserter.itemId > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(inserter.itemId), $"{nameof(inserter.itemId)} was not within the bounds of a short. Value: {inserter.itemId}");
        }

        if (pickFromOffset < short.MinValue || pickFromOffset > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pickFromOffset), $"{nameof(pickFromOffset)} was not within the bounds of a short. Value: {pickFromOffset}");
        }

        if (insertIntoOffset < short.MinValue || insertIntoOffset > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(insertIntoOffset), $"{nameof(insertIntoOffset)} was not within the bounds of a short. Value: {insertIntoOffset}");
        }

        this.grade = (short)grade;
        pickOffset = (short)pickFromOffset;
        insertOffset = (short)insertIntoOffset;
        time = inserter.time;
        itemId = (short)inserter.itemId;
        itemCount = inserter.itemCount;
        itemInc = inserter.itemInc;
        stackCount = inserter.stackCount;
        idleTick = inserter.idleTick;
    }

    public readonly OptimizedInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return new OptimizedInserter(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public readonly ReadonlyArray<InserterGrade> GetInserterGrades(UniverseStaticData universeStaticData)
    {
        return universeStaticData.InserterGrades;
    }

    public void Update(PlanetFactory planet,
                       InserterExecutor<OptimizedInserter, InserterGrade> inserterExecutor,
                       float power,
                       int inserterIndex,
                       ref InserterState inserterState,
                       ref readonly InserterGrade inserterGrade,
                       ref OptimizedInserterStage stage,
                       ReadonlyArray<InserterConnections> insertersConnections,
                       SubFactoryNeeds subFactoryNeeds,
                       OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (power < 0.1f)
        {
            return;
        }
        switch (stage)
        {
            case OptimizedInserterStage.Picking:
                {
                    int filter;
                    InserterConnections inserterConnections = insertersConnections[inserterIndex];
                    GroupNeeds groupNeeds = default;
                    if (itemId == 0)
                    {
                        if (inserterGrade.CareNeeds)
                        {
                            if (idleTick-- > 0)
                            {
                                goto doneThing;
                            }

                            if (!OptimizedBiInserter.IsNeedsNotEmpty(insertersConnections,
                                                                     subFactoryNeeds,
                                                                     inserterIndex,
                                                                     inserterConnections,
                                                                     out groupNeeds))
                            {
                                idleTick = 9;
                                goto doneThing;
                            }
                        }
                        else if (inserterConnections.InsertInto.EntityType == EntityType.FuelPowerGenerator)
                        {
                            if (idleTick-- > 0)
                            {
                                goto doneThing;
                            }

                            short num77 = inserterExecutor.PickFuelForPowerGenFrom(planet,
                                                                                   ref inserterState,
                                                                                   pickOffset,
                                                                                   insertOffset,
                                                                                   inserterGrade.Filter,
                                                                                   inserterConnections,
                                                                                   out byte stack11,
                                                                                   out byte inc11,
                                                                                   out bool fuelFull,
                                                                                   optimizedCargoPaths);
                            if (num77 > 0)
                            {
                                itemId = num77;
                                itemCount += stack11;
                                itemInc += inc11;
                                stackCount++;
                                time = 0;
                            }
                            else if (fuelFull)
                            {
                                idleTick = 9;
                            }

                            goto doneThing;
                        }

                        filter = inserterGrade.Filter;
                    }
                    else if (stackCount < inserterGrade.StackInput)
                    {
                        if (inserterGrade.CareNeeds)
                        {
                            if (idleTick-- > 0)
                            {
                                goto doneThing;
                            }

                            if (!OptimizedBiInserter.IsNeedsNotEmpty(insertersConnections,
                                                                     subFactoryNeeds,
                                                                     inserterIndex,
                                                                     inserterConnections,
                                                                     out groupNeeds))
                            {
                                idleTick = 10;
                                goto doneThing;
                            }
                        }

                        filter = itemId;
                    }
                    else
                    {
                        goto doneThing;
                    }

                    short num = inserterExecutor.PickFrom(planet,
                                                          ref inserterState,
                                                          inserterIndex,
                                                          pickOffset,
                                                          filter,
                                                          inserterConnections,
                                                          groupNeeds,
                                                          out byte stack,
                                                          out byte inc,
                                                          optimizedCargoPaths);
                    if (num > 0)
                    {
                        itemId = num;
                        itemCount += stack;
                        itemInc += inc;
                        stackCount++;
                        time = 0;
                    }

                doneThing:

                    if (itemId > 0)
                    {
                        time += InserterComponent.speed;
                        if (stackCount == inserterGrade.StackInput || time >= inserterGrade.Delay)
                        {
                            time = (int)(power * InserterComponent.speed);
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
                time += (int)(power * InserterComponent.speed);
                if (time >= inserterGrade.Stt)
                {
                    stage = OptimizedInserterStage.Inserting;
                    time -= inserterGrade.Stt;
                }
                if (itemId == 0)
                {
                    stage = OptimizedInserterStage.Returning;
                    time = inserterGrade.Stt - time;
                }
                break;
            case OptimizedInserterStage.Inserting:
                if (itemId == 0 || stackCount == 0)
                {
                    itemId = 0;
                    stackCount = 0;
                    itemCount = 0;
                    itemInc = 0;
                    time += (int)(power * InserterComponent.speed);
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
                        optimizedCargoPaths[num2.Index].TryInsertItemWithStackIncreasement(insertOffset, itemId, inserterGrade.StackOutput, ref num3, ref num4);
                        itemCount = (short)num3;
                        itemInc = (short)num4;
                        stackCount = itemCount > 0 ? (itemCount - 1) / 4 + 1 : 0;
                        if (stackCount == 0)
                        {
                            itemId = 0;
                            time += (int)(power * InserterComponent.speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                        break;
                    }

                    GroupNeeds groupNeeds = default;
                    if (inserterGrade.CareNeeds)
                    {
                        if (OptimizedBiInserter.IsNeedsEmpty(subFactoryNeeds,
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
                                                           ref inserterState,
                                                           inserterIndex,
                                                           inserterConnections,
                                                           groupNeeds,
                                                           insertOffset,
                                                           itemId,
                                                           (byte)num5,
                                                           (byte)num6,
                                                           out remainInc,
                                                           optimizedCargoPaths);
                    if (inserterConnections.InsertInto.EntityType == EntityType.FuelPowerGenerator &&
                        num7 == 0)
                    {
                        idleTick = 10;
                    }
                    else if (num7 > 0)
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
                            time += (int)(power * InserterComponent.speed);
                            stage = OptimizedInserterStage.Returning;
                            itemCount = 0;
                            itemInc = 0;
                        }
                    }
                }
                break;
            case OptimizedInserterStage.Returning:
                time += (int)(power * InserterComponent.speed);
                if (time >= inserterGrade.Stt)
                {
                    stage = OptimizedInserterStage.Picking;
                    time = 0;
                }
                break;
        }
    }

    public readonly void Save(ref InserterComponent inserter, EInserterStage inserterStage)
    {
        inserter.time = time;
        inserter.itemId = itemId;
        inserter.itemCount = itemCount;
        inserter.itemInc = itemInc;
        inserter.stackCount = stackCount;
        inserter.idleTick = idleTick;
        inserter.stage = inserterStage;
    }
}