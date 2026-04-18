using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Weaver.FuzzyTests;

public sealed class OptimizedCargoPathTestsOneBeltChunk
{
    private const int _updateCount = 200;

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

    public static IEnumerable<(int index, BeltChunk beltChunk, int addRate)> InsertIndexSingleSpeedBeltRandomPermutations()
    {
        HashSet<(int index, BeltChunk beltChunk, int addRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            int addRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, addRate)))
            {
                yield return (insertIndex, beltChunk, addRate);
            }
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int getIndex, int addRate, int removeRate)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex()
    {
        HashSet<(int insertIndex, BeltChunk beltChunk, int getIndex, int addRate, int removeRate)> uniqueTestCases = [];
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
            int addRate = random.Next(1, 20);
            int removeRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, getIndex, addRate, removeRate)))
            {
                yield return (insertIndex, beltChunk, getIndex, addRate, removeRate);
            }
        }
    }

    public static IEnumerable<(int index, BeltChunk beltChunk, int maxStacks, int addRate)> InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks()
    {
        HashSet<(int index, BeltChunk beltChunk, int maxStacks, int addRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(0, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(20, 50));
            int maxStack = random.Next(1, 5);
            int addRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, maxStack, addRate)))
            {
                yield return (insertIndex, beltChunk, maxStack, addRate);
            }
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int getIndex, int maxStacks, int addRate, int removeRate)> InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks()
    {
        HashSet<(int insertIndex, BeltChunk beltChunk, int getIndex, int maxStacks, int addRate, int removeRate)> uniqueTestCases = [];
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
            int addRate = random.Next(1, 20);
            int removeRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, getIndex, maxStack, addRate, removeRate)))
            {
                yield return (insertIndex, beltChunk, getIndex, maxStack, addRate, removeRate);
            }
        }
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutations))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int addRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();
            }
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndex))]
    public async Task TryInsertItem_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int getIndex, int addRate, int removeRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();
            }
            if (i % removeRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryGetItem(getIndex)).IsTrue();
            }
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int maxStacks, int addRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();
            }
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertIndexSingleSpeedBeltRandomPermutationsWithGetIndexMaxStacks))]
    public async Task TryInsertItemWithStackIncreasement_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int getIndex, int maxStacks, int addRate, int removeRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStacks, new TestCargo(3, 2, 1))).IsTrue();
            }
            if (i % removeRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryGetItem(getIndex)).IsTrue();
            }
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(AllSpeeds))]
    public async Task TryInsertAtHeadFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertAtHeadFillBlank(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(AllSpeeds))]
    public async Task TryInsertItemAtHead_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertItemAtHead(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(AllSpeedsAllMaxStacks))]
    public async Task TryUpdateItemAtHeadAndFillBlank_WithBeltComparerPermutationOverTime_ExpectBeltsAreEqual(int speed, int maxStack)
    {
        var comparer = new BeltComparer([new BeltChunk(speed, 100)], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryUpdateItemAtHeadAndFillBlank(maxStack, new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }
}