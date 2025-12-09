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
}
