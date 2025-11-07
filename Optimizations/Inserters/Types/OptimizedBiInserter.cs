using System;
using System.Runtime.InteropServices;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.NeedsSystem;

namespace Weaver.Optimizations.Inserters.Types;

[StructLayout(LayoutKind.Auto)]
internal readonly struct BiInserterGrade : IInserterGrade<BiInserterGrade>
{
    public readonly byte StackInput;
    public readonly byte StackOutput;
    public readonly bool CareNeeds;
    public readonly short Filter;

    public BiInserterGrade(byte stackInput, byte stackOutput, bool careNeeds, int filter)
    {
        if (filter < 0 || filter > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), $"{nameof(filter)} was not within the bounds of a short. Value: {filter}");
        }

        StackInput = stackInput;
        StackOutput = stackOutput;
        CareNeeds = careNeeds;
        Filter = (short)filter;
    }

    public readonly BiInserterGrade Create(ref InserterComponent inserter)
    {
        byte b = (byte)GameMain.history.inserterStackCountObsolete;
        byte b2 = (byte)GameMain.history.inserterStackInput;
        byte stackOutput = (byte)GameMain.history.inserterStackOutput;

        if (inserter.grade == 3)
        {
            return new BiInserterGrade(b, 1, inserter.careNeeds, inserter.filter);
        }
        else if (inserter.grade == 4)
        {
            return new BiInserterGrade(b2, stackOutput, inserter.careNeeds, inserter.filter);
        }
        else
        {
            return new BiInserterGrade(1, 1, inserter.careNeeds, inserter.filter);
        }
    }

    public readonly bool Equals(BiInserterGrade other)
    {
        return StackInput == other.StackInput &&
               StackOutput == other.StackOutput &&
               CareNeeds == other.CareNeeds &&
               Filter == other.Filter;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is BiInserterGrade inserterGrade && Equals(inserterGrade);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(StackInput, StackOutput, CareNeeds, Filter);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedBiInserter : IInserter<OptimizedBiInserter, BiInserterGrade>
{
    public short grade { get; }
    public int pickOffset { get; }
    public int insertOffset { get; }
    private short itemId;
    private short itemCount;
    private short itemInc;
    private int stackCount;
    private int idleTick;

    public OptimizedBiInserter(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        if (grade < 0 || grade > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(grade), $"{nameof(grade)} was not within the bounds of a short. Value: {grade}");
        }

        if (inserter.itemId < 0 || inserter.itemId > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(inserter.itemId), $"{nameof(inserter.itemId)} was not within the bounds of a short. Value: {inserter.itemId}");
        }

        this.grade = (short)grade;
        pickOffset = pickFromOffset;
        insertOffset = insertIntoOffset;
        itemId = (short)inserter.itemId;
        itemCount = inserter.itemCount;
        itemInc = inserter.itemInc;
        stackCount = inserter.stackCount;
        idleTick = inserter.idleTick;
    }

    public readonly OptimizedBiInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade)
    {
        return new OptimizedBiInserter(in inserter, pickFromOffset, insertIntoOffset, grade);
    }

    public readonly BiInserterGrade[] GetInserterGrades(UniverseStaticData universeStaticData)
    {
        return universeStaticData.BiInserterGrades;
    }

    internal static bool IsNeedsNotEmpty(InserterConnections[] insertersConnections,
                                         ref readonly SubFactoryNeeds subFactoryNeeds,
                                         int inserterIndex,
                                         out InserterConnections inserterConnections,
                                         out GroupNeeds groupNeeds)
    {
        inserterConnections = insertersConnections[inserterIndex];
        groupNeeds = subFactoryNeeds.GetGroupNeeds(inserterConnections.InsertInto.EntityType);

        // Object index for fuel power generators  does not represent the fuel generators index
        if (groupNeeds.GroupNeedsSize == 0 || inserterConnections.InsertInto.EntityType == EntityType.FuelPowerGenerator)
        {
            return true;
        }

        short[] needs = subFactoryNeeds.Needs;
        int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            if (needs[needsOffset + i] != 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsNeedsEmpty(ref readonly SubFactoryNeeds subFactoryNeeds,
                                      InserterConnections inserterConnections,
                                      out GroupNeeds groupNeeds)
    {
        groupNeeds = subFactoryNeeds.GetGroupNeeds(inserterConnections.InsertInto.EntityType);

        // Object index for fuel power generators  does not represent the fuel generators index
        if (groupNeeds.GroupNeedsSize == 0 || inserterConnections.InsertInto.EntityType == EntityType.FuelPowerGenerator)
        {
            return true;
        }

        short[] needs = subFactoryNeeds.Needs;
        int needsOffset = groupNeeds.GetObjectNeedsIndex(inserterConnections.InsertInto.Index);
        for (int i = 0; i < groupNeeds.GroupNeedsSize; i++)
        {
            if (needs[needsOffset + i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    public void Update(PlanetFactory planet,
                       InserterExecutor<OptimizedBiInserter, BiInserterGrade> inserterExecutor,
                       float power,
                       int inserterIndex,
                       ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                       ref readonly BiInserterGrade inserterGrade,
                       ref OptimizedInserterStage stage,
                       InserterConnections[] insertersConnections,
                       ref readonly SubFactoryNeeds subFactoryNeeds,
                       OptimizedCargoPath[] optimizedCargoPaths)
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
                if (inserterGrade.CareNeeds)
                {
                    if (idleTick-- < 1)
                    {
                        if (IsNeedsNotEmpty(insertersConnections,
                                            in subFactoryNeeds,
                                            inserterIndex,
                                            out InserterConnections inserterConnections,
                                            out GroupNeeds groupNeeds))
                        {
                            short num2 = inserterExecutor.PickFrom(planet,
                                                                   ref inserterNetworkIdAndState,
                                                                   inserterIndex,
                                                                   pickOffset,
                                                                   inserterGrade.Filter,
                                                                   inserterConnections,
                                                                   groupNeeds,
                                                                   out stack,
                                                                   out inc,
                                                                   optimizedCargoPaths);
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
                    InserterConnections inserterConnections = insertersConnections[inserterIndex];
                    short num2 = inserterExecutor.PickFrom(planet,
                                                           ref inserterNetworkIdAndState,
                                                           inserterIndex,
                                                           pickOffset,
                                                           inserterGrade.Filter,
                                                           inserterConnections,
                                                           default,
                                                           out stack,
                                                           out inc,
                                                           optimizedCargoPaths);
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
                if (inserterGrade.Filter == 0 || inserterGrade.Filter == itemId)
                {
                    if (inserterGrade.CareNeeds)
                    {
                        if (idleTick-- < 1)
                        {
                            if (IsNeedsNotEmpty(insertersConnections,
                                                in subFactoryNeeds,
                                                inserterIndex,
                                                out InserterConnections inserterConnections,
                                                out GroupNeeds groupNeeds))
                            {
                                int num44 = inserterExecutor.PickFrom(planet,
                                                                      ref inserterNetworkIdAndState,
                                                                      inserterIndex,
                                                                      pickOffset,
                                                                      itemId,
                                                                      inserterConnections,
                                                                      groupNeeds,
                                                                      out stack,
                                                                      out inc,
                                                                      optimizedCargoPaths);
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
                                                       out inc,
                                                       optimizedCargoPaths) > 0)
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

            InserterConnections inserterConnections = insertersConnections[inserterIndex];
            TypedObjectIndex num4 = inserterGrade.StackOutput > 1 ? inserterConnections.InsertInto : default;
            if (num4.EntityType == EntityType.Belt)
            {
                int num5 = itemCount;
                int num6 = itemInc;
                optimizedCargoPaths[num4.Index].TryInsertItemWithStackIncreasement(insertOffset, itemId, inserterGrade.StackOutput, ref num5, ref num6);
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

            GroupNeeds groupNeeds = default;
            if (inserterGrade.CareNeeds)
            {

                if (IsNeedsEmpty(in subFactoryNeeds,
                                 inserterConnections,
                                 out groupNeeds))
                {
                    idleTick = 10;
                    break;
                }
            }
            int num7 = itemCount / stackCount;
            int num8 = (int)(itemInc / (float)itemCount * num7 + 0.5f);
            byte remainInc;
            int num9 = inserterExecutor.InsertInto(planet,
                                                   ref inserterNetworkIdAndState,
                                                   inserterIndex,
                                                   inserterConnections,
                                                   groupNeeds,
                                                   insertOffset,
                                                   itemId,
                                                   (byte)num7,
                                                   (byte)num8,
                                                   out remainInc,
                                                   optimizedCargoPaths);
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

    public readonly void Save(ref InserterComponent inserter, EInserterStage inserterStage)
    {
        inserter.itemId = itemId;
        inserter.itemCount = itemCount;
        inserter.itemInc = itemInc;
        inserter.stackCount = stackCount;
        inserter.idleTick = idleTick;
        inserter.stage = inserterStage;
    }

    public override readonly string ToString()
    {
        return $"""
            Bi Inserter
            \t{nameof(grade)}: {grade:N0}
            \t{nameof(pickOffset)}: {pickOffset:N0}
            \t{nameof(insertOffset)}: {insertOffset:N0}
            \t{nameof(itemId)}: {itemId:N0}
            \t{nameof(itemCount)}: {itemCount:N0}
            \t{nameof(itemInc)}: {itemInc:N0}
            \t{nameof(stackCount)}: {stackCount:N0}
            \t{nameof(idleTick)}: {idleTick:N0}
            """;
    }
}