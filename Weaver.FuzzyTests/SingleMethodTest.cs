using System.Threading.Tasks;

namespace Weaver.FuzzyTests;

public sealed class RegressionTests
{
    [Test]
    public async Task TryInsertItem_WithBeltSpecificExample_ExpectBeltsAreEqual()
    {
        int updateCount = 200;
        int insertionIndex = 13;
        var beltChunk1 = new BeltChunk(4, 22);
        var beltChunk2 = new BeltChunk(2, 34);
        int addRate = 7;
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < updateCount; i++)
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
    public async Task TryInsertItem_WithBeltSpecificExample2_ExpectBeltsAreEqual()
    {
        int insertionIndex = 37;
        var beltChunk1 = new BeltChunk(4, 22);
        var beltChunk2 = new BeltChunk(2, 34);
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        await TUnit.Assertions.Assert.That(comparer.TryInsertItem(insertionIndex, new TestCargo(3, 2, 1))).IsTrue();

        await comparer.AssertEqualAsync();
        comparer.Update();
        await comparer.AssertEqualAsync();

        await comparer.AssertEqualAsync();
    }

    [Test]
    public async Task TryInsertAtHeadFillBlank_WithThreeDifferentBelts_ExpectBeltsAreEqual()
    {
        int updateCount = 200;
        var comparer = new BeltComparer([new BeltChunk(1, 30), new BeltChunk(2, 30), new BeltChunk(1, 30)], 0);

        for (int i = 0; i < updateCount; i++)
        {
            await TUnit.Assertions.Assert.That(comparer.TryInsertAtHeadFillBlank(new TestCargo(3, 2, 1))).IsTrue();
            await comparer.AssertEqualAsync();
            comparer.Update();
            await comparer.AssertEqualAsync();
        }

        await comparer.AssertEqualAsync();
    }

    [Test]
    public async Task TryInsertItem_WithTwoDifferentBelts_ExpectBeltsAreEqual()
    {
        int updateCount = 200;
        int insertionIndex = 19;
        var beltChunk1 = new BeltChunk(2, 41);
        var beltChunk2 = new BeltChunk(4, 25);
        int addRate = 9;
        var comparer = new BeltComparer([beltChunk1, beltChunk2], 0);

        for (int i = 0; i < updateCount; i++)
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
    public async Task TryInsertItem_WithBeltComparerPermutationOverTimeAndTakeItem_ExpectBeltsAreEqual()
    {
        int updateCount = 200;
        int insertionIndex = 11;
        var beltChunk1 = new BeltChunk(1, 20);
        var beltChunk2 = new BeltChunk(4, 49);
        var beltChunk3 = new BeltChunk(5, 27);
        int getIndex = 4;
        int addRate = 18;
        int removeRate = 17;
        var comparer = new BeltComparer([beltChunk1, beltChunk2, beltChunk3], 0);

        for (int i = 0; i < updateCount; i++)
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
}
