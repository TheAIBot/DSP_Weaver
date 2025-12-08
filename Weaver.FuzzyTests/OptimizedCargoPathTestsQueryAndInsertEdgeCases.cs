using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Weaver.FuzzyTests;

public sealed class OptimizedCargoPathTestsQueryAndInsertEdgeCases
{
    private const int _updateCount = 300;

    // Insert at indices just in front of the rear / stopped region so the
    // TryFindIndexOfFirstPreviousZeroValue fast-path region check is exercised
    // right at the stopped-region boundary (the bug I found in that method).
    public static IEnumerable<(int index, BeltChunk beltChunk, int addRate)> InsertNearRearRandomPermutations()
    {
        HashSet<(int index, BeltChunk beltChunk, int addRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(40, 100));
            int insertIndex = random.Next(beltChunk.Length - 30, beltChunk.Length - 6);
            if (insertIndex < 4 || insertIndex + 5 >= beltChunk.Length)
            {
                continue;
            }
            int addRate = random.Next(1, 6);

            if (uniqueTestCases.Add((insertIndex, beltChunk, addRate)))
            {
                yield return (insertIndex, beltChunk, addRate);
            }
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int queryIndex, int addRate)> InsertWithQueryRandomPermutations()
    {
        HashSet<(int insertIndex, BeltChunk beltChunk, int queryIndex, int addRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(4, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(30, 80));
            // Bias toward the rear so the [index, num] scan in QueryItemAtIndex
            // is clamped to the end of the buffer (off-by-one in the optimized version).
            int queryIndex = random.Next(0, 100) < 70
                ? random.Next(Math.Max(0, beltChunk.Length - 15), beltChunk.Length)
                : random.Next(0, beltChunk.Length);
            int addRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, queryIndex, addRate)))
            {
                yield return (insertIndex, beltChunk, queryIndex, addRate);
            }
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int queryIndex, int maxStack, int addRate)> InsertWithStackIncreasementWithQueryRandomPermutations()
    {
        HashSet<(int insertIndex, BeltChunk beltChunk, int queryIndex, int maxStack, int addRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(4, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(30, 80));
            int queryIndex = random.Next(0, 100) < 70
                ? random.Next(Math.Max(0, beltChunk.Length - 15), beltChunk.Length)
                : random.Next(0, beltChunk.Length);
            int maxStack = random.Next(1, 5);
            int addRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, queryIndex, maxStack, addRate)))
            {
                yield return (insertIndex, beltChunk, queryIndex, maxStack, addRate);
            }
        }
    }

    public static IEnumerable<(int insertIndex, BeltChunk beltChunk, int queryIndex, int removeIndex, int addRate, int removeRate)> InsertRemoveWithQueryRandomPermutations()
    {
        HashSet<(int insertIndex, BeltChunk beltChunk, int queryIndex, int removeIndex, int addRate, int removeRate)> uniqueTestCases = [];
        var random = new Random(1);
        int testCount = 10_000;
        for (int i = 0; i < testCount; i++)
        {
            int insertIndex = random.Next(4, 30);
            var beltChunk = new BeltChunk(random.Next(1, 6), random.Next(30, 80));
            int queryIndex = random.Next(0, beltChunk.Length);
            int removeIndex = random.Next(0, beltChunk.Length);
            int addRate = random.Next(1, 20);
            int removeRate = random.Next(1, 20);

            if (uniqueTestCases.Add((insertIndex, beltChunk, queryIndex, removeIndex, addRate, removeRate)))
            {
                yield return (insertIndex, beltChunk, queryIndex, removeIndex, addRate, removeRate);
            }
        }
    }

    // Regression for the TryFindIndexOfFirstPreviousZeroValue fast-path bug (#1).
    [Test]
    [MethodDataSource(nameof(InsertNearRearRandomPermutations))]
    public async Task TryInsertItem_NearRearOfBelt_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int addRate)
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

    // Regression for the TryFindIndexOfFirstPreviousZeroValue fast-path bug (#1).
    [Test]
    [MethodDataSource(nameof(InsertNearRearRandomPermutations))]
    public async Task TryInsertItemWithStackIncreasement_NearRearOfBelt_ExpectBeltsAreEqual(int insertionIndex, BeltChunk beltChunk, int addRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, 3, new TestCargo(3, 2, 1))).IsTrue();
            }
            await comparer.AssertEqualAsync();
            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    // Covers the QueryItemAtIndex off-by-one (#2) and GetCargoAtIndex boundary read (#3).
    [Test]
    [MethodDataSource(nameof(InsertWithQueryRandomPermutations))]
    public async Task TryInsertItem_WithQueryItemAtIndex_ExpectQueriesAgree(int insertionIndex, BeltChunk beltChunk, int queryIndex, int addRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await comparer.IsTruePrintBeltsIfFalseAsync(() => comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1)));
            }

            await TUnit.Assertions.Assert.That(comparer.TryQueryItem(queryIndex))
                                         .IsTrue()
                                         .Because($"QueryItemAtIndex diverged at index {queryIndex}, frame {i}");
            await TUnit.Assertions.Assert.That(comparer.TryGetCargoAtIndex(queryIndex))
                                         .IsTrue()
                                         .Because($"GetCargoAtIndex diverged at index {queryIndex}, frame {i}");

            await comparer.AssertEqualAsync();
            comparer.Update();
            await comparer.AssertEqualAsync();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    [MethodDataSource(nameof(InsertWithStackIncreasementWithQueryRandomPermutations))]
    public async Task TryInsertItemWithStackIncreasement_WithQueryItemAtIndex_ExpectQueriesAgree(int insertionIndex, BeltChunk beltChunk, int queryIndex, int maxStack, int addRate)
    {
        var comparer = new BeltComparer([beltChunk], 0);

        for (int i = 0; i < _updateCount; i++)
        {
            if (i % addRate == 0)
            {
                await TUnit.Assertions.Assert.That(comparer.TryInsertItemWithStackIncreasement(insertionIndex, maxStack, new TestCargo(3, 2, 1))).IsTrue();
            }

            await TUnit.Assertions.Assert.That(comparer.TryQueryItem(queryIndex))
                                         .IsTrue()
                                         .Because($"QueryItemAtIndex diverged at index {queryIndex}, frame {i}");
            await TUnit.Assertions.Assert.That(comparer.TryGetCargoAtIndex(queryIndex))
                                         .IsTrue()
                                         .Because($"GetCargoAtIndex diverged at index {queryIndex}, frame {i}");

            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }

    // Exercises RemoveCargoAtIndex (which uses TryGetCargoWithinRange) alongside queries.
    [Test]
    [MethodDataSource(nameof(InsertRemoveWithQueryRandomPermutations))]
    public async Task TryInsertItem_WithRemoveAndQuery_ExpectQueriesAgree(int insertionIndex, BeltChunk beltChunk, int queryIndex, int removeIndex, int addRate, int removeRate)
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
                await TUnit.Assertions.Assert.That(comparer.TryRemoveCargoAtIndex(removeIndex))
                                             .IsTrue()
                                             .Because($"RemoveCargoAtIndex diverged at index {removeIndex}, frame {i}");
            }

            await TUnit.Assertions.Assert.That(comparer.TryQueryItem(queryIndex))
                                         .IsTrue()
                                         .Because($"QueryItemAtIndex diverged at index {queryIndex}, frame {i}");
            await TUnit.Assertions.Assert.That(comparer.TryGetCargoAtIndex(queryIndex))
                                         .IsTrue()
                                         .Because($"GetCargoAtIndex diverged at index {queryIndex}, frame {i}");

            comparer.Update();
        }

        await comparer.AssertEqualAsync();
    }
}
