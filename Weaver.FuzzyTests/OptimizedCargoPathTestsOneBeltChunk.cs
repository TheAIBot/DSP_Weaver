using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Weaver.FuzzyTests;

public sealed class OptimizedCargoPathTestsOneBeltChunk
{
    public static IEnumerable<int> AllSpeeds()
    {
        for (int speed = 1; speed <= 5; speed++)
        {
            yield return speed;
        }
    }

    public static IEnumerable<(int speed, int maxStack)> AllSpeedsAllMaxStacks()
    {
        for (int speed = 1; speed <= 5; speed++)
        {
            for (int maxStack = 1; maxStack <= 4; maxStack++)
            {
                yield return (speed, maxStack);
            }
        }
    }

    public static IEnumerable<(int index, BeltChunk beltChunk)> InsertIndexSingleSpeedBeltRandomPermutations()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));

            yield return (insertIndex, beltChunk);
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int getIndex)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            int minGetIndex = Math.Max(insertIndex - CargoPath.kCargoLength, 0);
            if (minGetIndex > 20)
            {
                continue;
            }
            int getIndex = random.Next(minGetIndex, 20);

            yield return (insertIndex, beltChunk, getIndex);
        }
    }

    public static IEnumerable<(int index, BeltChunk beltChunk, int maxStacks)> InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            int maxStack = random.Next(1, 5);

            yield return (insertIndex, beltChunk, maxStack);
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int getIndex, int maxStacks)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks()
    {
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            int minGetIndex = Math.Max(insertIndex - CargoPath.kCargoLength, 0);
            if (minGetIndex > 20)
            {
                continue;
            }
            int getIndex = random.Next(minGetIndex, 20);
            int maxStack = random.Next(1, 5);

            yield return (insertIndex, beltChunk, getIndex, maxStack);
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutations))]
    public async Task TryInsertItem_WithDifferentBeltIndex_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutations))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int getIndex)
    {
        var comparer = new BeltComparer([beltChunk], 0);

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
    public async Task TryInsertItemWithStackIncreasement_WithDifferentBeltIndex_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int getIndex, int maxStacks)
    {
        var comparer = new BeltComparer([beltChunk], 0);

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
    public async Task TryInsertAtHeadFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertAtHeadFillBlank(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(AllSpeeds))]
    public async Task TryInsertItemAtHead_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemAtHead(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }

    [Test]
    [MethodDataSource(nameof(AllSpeedsAllMaxStacks))]
    public async Task TryUpdateItemAtHeadAndFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed, int maxStack)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < 50; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryUpdateItemAtHeadAndFillBlank(maxStack, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }
    }
}