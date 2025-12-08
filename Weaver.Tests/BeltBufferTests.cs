using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;
using Weaver.Optimizations.Belts;

namespace Weaver.Tests;

internal sealed class BeltBufferTests
{
    [Test]
    public async Task CreateFromExistingBuffer_WithItemsOnBelt_ExpectDataIsAvailableInOriginalIndexes()
    {
        byte[] existingData = [1, 0, 2];

        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(2);
    }

    [Test]
    public async Task CreateFromExistingBuffer_WithFullBelt_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [1, 7, 2];

        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(7);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(2);
    }

    [Test]
    public async Task Update_WithItemsOnMovingBeltThatWillNotBecomeStopped_ExpectDataIsAvailableInMovedIndexes()
    {
        byte[] existingData = [3, 4, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WithUpdatedTwiceItemsOnMovingBeltThatWillNotBecomeStopped_ExpectDataIsAvailableInMovedIndexes()
    {
        byte[] existingData = [3, 4, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WithSpeed2ItemsOnMovingBeltThatWillNotBecomeStopped_ExpectDataIsAvailableInMovedIndexes()
    {
        byte[] existingData = [3, 4, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(0);
    }

    [Test]
    public async Task CreateFromExistingBuffer_WithStoppedItems_ExpectDataIsAvailableInOriginalIndexes()
    {
        byte[] existingData = [0, 3, 4];

        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithUpdateStoppedItems_ExpectDataIsAvailableInOriginalIndexes()
    {
        byte[] existingData = [0, 3, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithUpdateMakesStoppedItems_ExpectDataIsAvailableAtStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithSpeedHigherThanDistanceToEndOfBeltUpdateMakesStoppedItems_ExpectDataIsAvailableAtStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithStoppedItemsUpdateMovesItems_ExpectDataIsAvailableInOriginalIndexes()
    {
        byte[] existingData = [0, 3, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithUpdateMakesStoppedItemsAndUpdateMovesItemsToStopped_ExpectDataIsAvailableAtStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithUpdateMakesStoppedItemsAndUpdateMovesItems_ExpectDataIsAvailableAtMovingIndexes()
    {
        byte[] existingData = [0, 3, 4, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(5)).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithClearCausingOtherStoppedItemsToBeMovingItems_ExpectDataIsAvailableInCorrectIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.Clear(3, 2);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithClearCausingOtherStoppedItemsToBeMovingItemsThenUpdatesMakesItemsStoppedAgain_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.Clear(3, 2);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_ClearAfterTwoUpdatesAndThenUpdateEnoughToMakeItemsStopped_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2, 8, 1, 9];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 10);
        beltBuffer.Update();
        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 3, 4, 7, 2, 8, 1, 9 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 3, 4, 7, 2, 8, 1, 9 }, CollectionOrdering.Matching);

        beltBuffer.Clear(6, 2);
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 3, 4, 7, 2, 8, 0, 0 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 3, 4, 7, 2, 8, 0 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 3, 4, 7, 2, 8 }, CollectionOrdering.Matching);

        beltBuffer.Clear(6, 2);
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 3, 4, 7, 0, 0 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 3, 4, 7, 0 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);

        beltBuffer.Update();
        actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 3, 4, 7 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithSetBufferValueCausingOtherStoppedItemsToBeMovingItems_ExpectDataIsAvailableInCorrectIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.SetBufferValue(3, 0);
        beltBuffer.SetBufferValue(4, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithSetBufferValueCausingOtherStoppedItemsToBeMovingItemsThenUpdatesMakesItemsStoppedAgain_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.SetBufferValue(3, 0);
        beltBuffer.SetBufferValue(4, 0);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(4);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithSetBufferValueCausingTwoHolesCausingOtherStoppedItemsToBeMovingItems_ExpectDataIsAvailableInCorrectIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.SetBufferValue(2, 0);
        beltBuffer.SetBufferValue(4, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(7);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithSetBufferValueCausingTwoHolesCausingOtherStoppedItemsToBeMovingItemsThenUpdatesMakesItemsStoppedAgain_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.SetBufferValue(2, 0);
        beltBuffer.SetBufferValue(4, 0);

        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(7);
    }

    [Test]
    public async Task Update_WithItemsRemovedFromStoppedItemsWithSetBufferValueCausingTwoHolesCausingOtherStoppedItemsToBeMovingItemsSpeed2_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);
        beltBuffer.SetBufferValue(2, 0);
        beltBuffer.SetBufferValue(4, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(7);
    }

    [Test]
    public async Task Update_WithItemsRemovedAndAddedFromStoppedPart_ExpectDataIsAvailableInStoppedIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.SetBufferValue(3, 0);
        beltBuffer.SetBufferValue(4, 0);
        beltBuffer.SetBufferValue(3, 6);
        beltBuffer.SetBufferValue(4, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(4);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(6);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(1);
    }

    [Test]
    public async Task Update_WithItemsRemovedAndAddedFromStoppedPart_ExpectDataIsAvailableInCorrectIndexes()
    {
        byte[] existingData = [0, 3, 4, 7, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 1);
        beltBuffer.SetBufferValue(2, 0);
        beltBuffer.SetBufferValue(4, 0);
        beltBuffer.SetBufferValue(2, 6);
        beltBuffer.SetBufferValue(4, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(3);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(6);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(7);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(1);
    }

    [Test]
    [Arguments(new byte[] { 1, 0, 0, 4, 0, 3 }, new byte[] { 0, 0, 0, 1, 4, 3 })]
    [Arguments(new byte[] { 1, 0, 0, 4, 0, 0 }, new byte[] { 0, 0, 0, 0, 1, 4 })]
    [Arguments(new byte[] { 1, 0, 3, 0, 2, 0 }, new byte[] { 0, 0, 0, 1, 3, 2 })]
    [Arguments(new byte[] { 1, 0, 0, 0, 3, 0, 2, 0 }, new byte[] { 0, 0, 0, 0, 1, 0, 3, 2 })]
    [Arguments(new byte[] { 0, 1, 2, 3, 0, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 0, 1, 2, 3 })]
    [Arguments(new byte[] { 5, 0, 0, 2, 3, 0, 3, 2, 0 }, new byte[] { 0, 0, 0, 0, 5, 2, 3, 3, 2 })]
    [Arguments(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1, 0 }, new byte[] { 0, 8, 7, 6, 5, 4, 3, 2, 1 })]
    [Arguments(new byte[] { 0, 7, 6, 5, 4, 3, 2, 1, 0 }, new byte[] { 0, 0, 7, 6, 5, 4, 3, 2, 1 })]
    [Arguments(new byte[] { 7, 6, 5, 4, 3, 2, 1, 0, 0 }, new byte[] { 0, 0, 7, 6, 5, 4, 3, 2, 1 })]
    [Arguments(new byte[] { 0, 6, 5, 4, 3, 2, 1, 0, 0 }, new byte[] { 0, 0, 0, 6, 5, 4, 3, 2, 1 })]
    [Arguments(new byte[] { 0, 6, 5, 4, 3, 2, 1, 0, 0, 6, 5, 4, 3, 2, 1, 0, 0 }, new byte[] { 0, 0, 0, 0, 0, 6, 5, 4, 3, 2, 1, 6, 5, 4, 3, 2, 1 })]
    public async Task Update_WithSpacedItemsAndSpeed4_ExpectDataIsAvailableInExpectedIndexes(byte[] existingData, byte[] expectedData)
    {
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 4);

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(new byte[] { 1, 0, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 1 })]
    [Arguments(new byte[] { 0, 1, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 1 })]
    [Arguments(new byte[] { 0, 0, 1, 0, 0 }, new byte[] { 0, 0, 0, 0, 1 })]
    [Arguments(new byte[] { 0, 0, 0, 1, 0 }, new byte[] { 0, 0, 0, 0, 1 })]
    [Arguments(new byte[] { 1, 0, 1, 0, 0 }, new byte[] { 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 1, 0, 1, 0 }, new byte[] { 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 0, 1, 0, 1 }, new byte[] { 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 0, 0, 1, 1 }, new byte[] { 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 1, 0, 0, 1, 0, 0 }, new byte[] { 0, 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 1, 0, 0, 1, 0 }, new byte[] { 0, 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 0, 1, 0, 0, 1 }, new byte[] { 0, 0, 0, 0, 1, 1 })]
    [Arguments(new byte[] { 0, 0, 0, 1, 0, 1 }, new byte[] { 0, 0, 0, 0, 1, 1 })]
    public async Task Update_WithSpacedItemsAndFourUpdatesAnd5UpdateBeforeMove_ExpectDataIsAvailableInExpectedIndexes(byte[] existingData, byte[] expectedData)
    {
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 5);

        beltBuffer.Update();
        beltBuffer.Update();
        beltBuffer.Update();
        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(1, 0, 22)]
    [Arguments(2, 0, 22)]
    [Arguments(3, 0, 22)]
    [Arguments(4, 0, 22)]
    [Arguments(1, 1, 22)]
    [Arguments(2, 1, 22)]
    [Arguments(3, 1, 22)]
    [Arguments(4, 1, 22)]
    public async Task Update_WithItemAddedAtNotFirstUpdate_ExpectItemMovedCorrectly(int updatesBeforeInsertCount, int index, int value)
    {
        byte[] existingData = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 5);
        for (int i = 0; i < updatesBeforeInsertCount; i++)
        {
            beltBuffer.Update();
        }
        beltBuffer.SetBufferValue(index, (byte)value);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(index + 1)).IsEqualTo(value);
    }

    [Test]
    [Arguments(1, 0, 1, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [Arguments(2, 0, 1, new byte[] { 0, 2, 1, 0, 0, 0, 0, 0, 0, 0 })]
    [Arguments(3, 0, 1, new byte[] { 0, 3, 2, 1, 0, 0, 0, 0, 0, 0 })]
    [Arguments(4, 0, 1, new byte[] { 0, 4, 3, 2, 1, 0, 0, 0, 0, 0 })]
    [Arguments(5, 0, 1, new byte[] { 0, 5, 4, 3, 2, 1, 0, 0, 0, 0 })]
    [Arguments(6, 0, 1, new byte[] { 0, 6, 5, 4, 3, 2, 1, 0, 0, 0 })]
    [Arguments(7, 0, 1, new byte[] { 0, 7, 6, 5, 4, 3, 2, 1, 0, 0 })]
    //
    [Arguments(1, 1, 1, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 })]
    [Arguments(2, 1, 1, new byte[] { 0, 0, 2, 1, 0, 0, 0, 0, 0, 0 })]
    [Arguments(3, 1, 1, new byte[] { 0, 0, 3, 2, 1, 0, 0, 0, 0, 0 })]
    [Arguments(4, 1, 1, new byte[] { 0, 0, 4, 3, 2, 1, 0, 0, 0, 0 })]
    [Arguments(5, 1, 1, new byte[] { 0, 0, 5, 4, 3, 2, 1, 0, 0, 0 })]
    [Arguments(6, 1, 1, new byte[] { 0, 0, 6, 5, 4, 3, 2, 1, 0, 0 })]
    [Arguments(7, 1, 1, new byte[] { 0, 0, 7, 6, 5, 4, 3, 2, 1, 0 })]
    //
    [Arguments(1, 2, 1, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 })]
    [Arguments(2, 2, 1, new byte[] { 0, 0, 0, 2, 1, 0, 0, 0, 0, 0 })]
    [Arguments(3, 2, 1, new byte[] { 0, 0, 0, 3, 2, 1, 0, 0, 0, 0 })]
    [Arguments(4, 2, 1, new byte[] { 0, 0, 0, 4, 3, 2, 1, 0, 0, 0 })]
    [Arguments(5, 2, 1, new byte[] { 0, 0, 0, 5, 4, 3, 2, 1, 0, 0 })]
    [Arguments(6, 2, 1, new byte[] { 0, 0, 0, 6, 5, 4, 3, 2, 1, 0 })]
    [Arguments(7, 2, 1, new byte[] { 0, 0, 0, 7, 6, 5, 4, 3, 2, 1 })]
    public async Task Update_WithItemAddedOnceAfterEachUpdate_ExpectItemMovedCorrectly(int updatesCount, int index, int value, byte[] expectedData)
    {
        byte[] existingData = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 5);
        for (int i = 0; i < updatesCount; i++)
        {
            beltBuffer.Update();
            beltBuffer.SetBufferValue(index, (byte)(value + i));
        }

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Copy_WithCopyDataInMovingSection_ExpectDataDuplicated()
    {
        byte[] existingData = [0, 0, 1, 2, 3, 0, 0, 0, 0, 0];
        byte[] expectedData = [0, 0, 1, 2, 3, 0, 1, 2, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Copy(2, 6, 2);

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithCopyDataInMovingSection_ExpectDataDuplicatedAndMoved()
    {
        byte[] existingData = [0, 0, 1, 2, 3, 0, 0, 0, 0, 0];
        byte[] expectedData = [0, 0, 0, 0, 1, 2, 3, 0, 1, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);
        beltBuffer.Copy(2, 6, 2);

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Clear_WithClearDataInMovingSection_ExpectDataCleared()
    {
        byte[] existingData = [0, 0, 1, 2, 3, 4, 0, 0, 0, 0];
        byte[] expectedData = [0, 0, 1, 0, 0, 4, 0, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Clear(3, 2);

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Clear_WithClearDataInStoppedSection_ExpectDataCleared()
    {
        byte[] existingData = [0, 0, 0, 0, 0, 0, 1, 2, 3, 4];
        byte[] expectedData = [0, 0, 0, 0, 0, 0, 1, 0, 0, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);

        beltBuffer.Clear(7, 2);

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithClearDataInMovingSection_ExpectDataClearedAndMoved()
    {
        byte[] existingData = [0, 0, 1, 2, 3, 4, 0, 0, 0, 0];
        byte[] expectedData = [0, 0, 0, 0, 1, 0, 0, 4, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);
        beltBuffer.Clear(3, 2);

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithClearDataInStoppedSectionSpeed1_ExpectDataClearedAndMoved()
    {
        byte[] existingData = [0, 0, 0, 0, 0, 0, 1, 2, 3, 4];
        byte[] expectedData = [0, 0, 0, 0, 0, 0, 0, 1, 0, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Clear(7, 2);

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithClearDataInStoppedSectionSpeed2_ExpectDataClearedAndMoved()
    {
        byte[] existingData = [0, 0, 0, 0, 0, 0, 1, 2, 3, 4];
        byte[] expectedData = [0, 0, 0, 0, 0, 0, 0, 0, 1, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 2);
        beltBuffer.Clear(7, 2);

        beltBuffer.Update();

        byte[] actualData = Enumerable.Range(0, existingData.Length).Select(beltBuffer.GetBufferValue).ToArray();
        await Assert.That(actualData).IsEquivalentTo(expectedData, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Update_WithHoleCreatedInsideStoppedSection_ExpectMovingItemsToStoppedSection()
    {
        byte[] existingData = [1, 2, 3];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Update();
        beltBuffer.SetBufferValue(1, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
    }

    [Test]
    public async Task Update_WithFullBeltAndClearItemInMiddle_ExpectMovingItemsToFillGap()
    {
        byte[] existingData = [1, 2, 3];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Update();
        beltBuffer.SetBufferValue(1, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(3);
    }

    [Test]
    public async Task Update_WithFullBeltAndClearLastItem_ExpectMovingItemsToStoppedSection()
    {
        byte[] existingData = [1, 2, 3];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Update();
        beltBuffer.SetBufferValue(2, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(2);
    }

    [Test]
    public async Task Update_WithClearAtExactBoundaryOfStoppedAndMoving_ExpectCorrectTransition()
    {
        byte[] existingData = [1, 0, 2, 3];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Update();
        beltBuffer.SetBufferValue(2, 0);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(3);
    }

    [Test]
    public async Task Update_WithTriggersMove_ExpectItemsMovedToEnd()
    {
        byte[] existingData = [1, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 2);

        beltBuffer.Update();
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);

        beltBuffer.Update();
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(1);
    }

    [Test]
    public async Task Update_WithTriggersMoveAndhItemsInMovingSection_ExpectItemsMovedToEnd()
    {
        byte[] existingData = [1, 0, 2, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 2);

        beltBuffer.Update();
        beltBuffer.Update();
        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(2);
    }

    [Test]
    public async Task Update_WithSpeedHigherThanGapSize_ExpectItemToFillGapAndStop()
    {
        byte[] existingData = [1, 0, 2];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 5);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(2);
    }

    [Test]
    public async Task Update_SpeedExceedsBeltLength_ExpectItemStopsAtEnd()
    {
        byte[] existingData = [1, 0, 0, 0, 0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 10);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(4)).IsEqualTo(1);
    }

    [Test]
    public async Task Update_WithClearAndAddInStoppedSection_ExpectCorrectGapFilling()
    {
        byte[] existingData = [1, 2, 3, 4];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1);
        beltBuffer.Update();
        beltBuffer.SetBufferValue(2, 0);
        beltBuffer.SetBufferValue(3, 5);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(0);
        await Assert.That((int)beltBuffer.GetBufferValue(1)).IsEqualTo(1);
        await Assert.That((int)beltBuffer.GetBufferValue(2)).IsEqualTo(2);
        await Assert.That((int)beltBuffer.GetBufferValue(3)).IsEqualTo(5);
    }

    [Test]
    public async Task Update_WithHighestOffsetAndItemAtStartOfBuffer_ExpectItemToMoveWithoutReset()
    {
        byte[] existingData = [0];
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(existingData, 1, 10);
        for (int i = 0; i < 10; i++)
        {
            beltBuffer.Update();
        }
        beltBuffer.SetBufferValue(0, 1);

        beltBuffer.Update();

        await Assert.That((int)beltBuffer.GetBufferValue(0)).IsEqualTo(1);
    }
}
