using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Weaver.FuzzyTests;

public sealed class OptimizedCargoPathTestsTwoBeltChunks
{
    public static IEnumerable<(int speed1, int speed2)> AllSpeeds()
    {
        for (int speed1 = 1; speed1 <= 5; speed1++)
        {
            for (int speed2 = 1; speed2 <= 5; speed2++)
            {
                if (speed1 == speed2)
                {
                    continue;
                }

                yield return (speed1, speed2);
            }
        }
    }

    public static IEnumerable<(int speed1, int speed2, int maxStack)> AllSpeedsAllMaxStacks()
    {
        for (int speed1 = 1; speed1 <= 5; speed1++)
        {
            for (int speed2 = 1; speed2 <= 5; speed2++)
            {
                if (speed1 == speed2)
                {
                    continue;
                }

                for (int maxStack = 1; maxStack <= 4; maxStack++)
                {
                    yield return (speed1, speed2, maxStack);
                }
            }

        }
    }

    public static IEnumerable<(int index, BeltChunk beltChunk1, BeltChunk beltChunk2)> InsertIndexSingleSpeedBeltRandomPermutations()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk1 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            var beltChunk2 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            if (beltChunk1 == beltChunk2)
            {
                continue;
            }

            yield return (insertIndex, beltChunk1, beltChunk2);
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int getIndex)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk1 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            var beltChunk2 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            if (beltChunk1 == beltChunk2)
            {
                continue;
            }
            int minGetIndex = Math.Max(insertIndex - CargoPath.kCargoLength, 0);
            if (minGetIndex > 20)
            {
                continue;
            }
            int getIndex = random.Next(minGetIndex, 20);

            yield return (insertIndex, beltChunk1, beltChunk2, getIndex);
        }
    }

    public static IEnumerable<(int index, BeltChunk beltChunk1, BeltChunk beltChunk2, int maxStacks)> InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk1 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            var beltChunk2 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            if (beltChunk1 == beltChunk2)
            {
                continue;
            }
            int maxStack = random.Next(1, 5);

            yield return (insertIndex, beltChunk1, beltChunk2, maxStack);
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int getIndex, int maxStacks)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk1 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            var beltChunk2 = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            if (beltChunk1 == beltChunk2)
            {
                continue;
            }
            int minGetIndex = Math.Max(insertIndex - CargoPath.kCargoLength, 0);
            if (minGetIndex > 20)
            {
                continue;
            }
            int getIndex = random.Next(minGetIndex, 20);
            int maxStack = random.Next(1, 5);

            yield return (insertIndex, beltChunk1, beltChunk2, getIndex, maxStack);
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutations))]
    public async Task TryInsertItem_WithDifferentBeltIndex_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutations))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int getIndex)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();
            await TUnit.Assertions.Assert.That(comparer.TryGetItem(getIndex)).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithDifferentBeltIndex_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk1, BeltChunk beltChunk2, int getIndex, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();
            await TUnit.Assertions.Assert.That(comparer.TryGetItem(getIndex)).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(AllSpeeds))]
    public async Task TryInsertAtHeadFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed1, int speed2)
    {
        var comparer = new BeltComparer([new BeltChunk(speed1, 50), new BeltChunk(speed2, 50)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertAtHeadFillBlank(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(AllSpeeds))]
    public async Task TryInsertItemAtHead_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed1, int speed2)
    {
        var comparer = new BeltComparer([new BeltChunk(speed1, 50), new BeltChunk(speed2, 50)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemAtHead(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(AllSpeedsAllMaxStacks))]
    public async Task TryUpdateItemAtHeadAndFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed1, int speed2, int maxStack)
    {
        var comparer = new BeltComparer([new BeltChunk(speed1, 50), new BeltChunk(speed2, 50)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryUpdateItemAtHeadAndFillBlank(maxStack, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }
}
