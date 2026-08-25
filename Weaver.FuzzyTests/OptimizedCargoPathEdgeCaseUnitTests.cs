using System.Reflection;
using System.Threading.Tasks;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.StaticData;

namespace Weaver.Tests;

internal sealed class OptimizedCargoPathEdgeCaseUnitTests
{
    /*
     * Constructs a BeltBuffer in a precise internal state.
     *
     * The factory writes copyFrom[i] at physical (i + maxOffsetBeforeMove) while the buffer
     * is still in its initial all-stopped / offset==0 state. So the data array already places
     * the bytes exactly where SetBufferValue(beltIndex) would have placed them.
     *
     * Then the private _offset and _stoppedItemsActualIndex are overridden so we can place the
     * stopped-region boundary exactly where the test needs it (this is what the fuzzy tests
     * cannot do deterministically).
     */
    private static BeltBuffer CreateBufferWithState(byte[] data, int beltSpeed, int offset, int stoppedItemsActualIndex)
    {
        BeltBuffer buffer = BeltBuffer.CreateFromExistingBuffer(data, beltSpeed);
        object boxed = buffer;
        typeof(BeltBuffer).GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!
                          .SetValue(boxed, offset);
        typeof(BeltBuffer).GetField("_stoppedItemsActualIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
                          .SetValue(boxed, stoppedItemsActualIndex);
        return (BeltBuffer)boxed;
    }

    private static OptimizedCargoPath CreatePath(BeltBuffer buffer, int beltLength)
    {
        return new OptimizedCargoPath(buffer,
                                      new ReadonlyArray<int>(new[] { 0, beltLength, 1 }),
                                      outputIndex: -1,
                                      closed: true,
                                      bufferLength: beltLength,
                                      chunkCount: 1,
                                      outputCargoPathIndex: BeltIndex.NoBelt,
                                      outputChunk: -1,
                                      lastUpdateFrameOdd: false);
    }

    // ================================================================
    // Difference #1: TryFindIndexOfFirstPreviousZeroValue fast path
    // ================================================================

    /*
     * The buggy fast-path condition checked IsInStoppedRegion(index-4) == IsInStoppedRegion(index+1)
     * instead of the range actually being read ([index+1, index+5]). With offset>0 and the
     * stopped-region boundary inside that window, the fast path reads the wrong physical bytes.
     *
     * Setup here: beltLength 30, speed 1 -> maxOffset 10; offset 3; stoppedItemsActualIndex 22
     * -> stopped region starts at belt 12. index=10 -> reads num in [11,15], which straddles the
     * boundary (belt 11 moving, belts 12-15 stopped).
     *
     *   physical 18 (belt 11, correct) = 0          <- zero the fixed code finds
     *   physical 21 (buggy fast-path read for 11) = 5
     *   physical 22..25 (belts 12..15)            = 5
     *
     * Correct result: zero at belt 11 -> returns true, index==6, num==11.
     * Buggy fast path reads physical 21 (=5) for belt 11, then falls out of the loop -> returns false.
     */
    [Test]
    public async Task TryFindIndexOfFirstPreviousZeroValue_WithStraddlingStoppedRegion_ExpectFindsZeroOnCorrectSide()
    {
        byte[] data = new byte[30];
        data[8] = 0;   // physical 18 -> belt 11 correct (non-stopped) location
        data[11] = 5;  // physical 21 -> the (wrong) location the buggy fast path reads for belt 11
        data[12] = 5;  // physical 22 -> belt 12
        data[13] = 5;  // physical 23 -> belt 13
        data[14] = 5;  // physical 24 -> belt 14
        data[15] = 5;  // physical 25 -> belt 15

        var beltBuffer = CreateBufferWithState(data, 1, offset: 3, stoppedItemsActualIndex: 22);

        int index = 10;
        int num = 15;   // index + 5
        int num2 = 5;   // index - 5

        bool found = beltBuffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num2);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That(index).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(num).IsEqualTo(11);
    }

    /*
     * Same straddling state, but now belt 11's correct location (physical 18) is also non-zero,
     * so neither the slow path nor the (buggy) fast path can find a zero inside the window.
     * Expected: returns false and leaves index/num untouched.
     */
    [Test]
    public async Task TryFindIndexOfFirstPreviousZeroValue_WithStraddlingStoppedRegionAndNoZero_ExpectReturnsFalse()
    {
        byte[] data = new byte[30];
        data[8] = 5;   // physical 18 -> belt 11 correct location, non-zero
        data[11] = 5;  // physical 21 -> buggy fast-path read for belt 11
        data[12] = 5;
        data[13] = 5;
        data[14] = 5;
        data[15] = 5;

        var beltBuffer = CreateBufferWithState(data, 1, offset: 3, stoppedItemsActualIndex: 22);

        int index = 10;
        int num = 15;
        int num2 = 5;

        bool found = beltBuffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num2);

        await TUnit.Assertions.Assert.That(found).IsFalse();
        await TUnit.Assertions.Assert.That(index).IsEqualTo(10);
        await TUnit.Assertions.Assert.That(num).IsEqualTo(15);
    }

    /*
     * Baseline: the entire scanned window is in the moving (non-stopped) region, so both the
     * buggy and fixed conditions take the fast path and both are correct.
     * offset 3; stoppedItemsActualIndex 100 -> stopped region starts at belt 90.
     * Zero at belt 13 -> index==8, num==13.
     */
    [Test]
    public async Task TryFindIndexOfFirstPreviousZeroValue_WithEntirelyMovingRegion_ExpectFindsZero()
    {
        byte[] data = new byte[30];
        data[8] = 5;   // belt 11 -> physical 18
        data[9] = 5;   // belt 12 -> physical 19
        data[10] = 0;  // belt 13 -> physical 20 (zero)
        data[11] = 5;  // belt 14 -> physical 21
        data[12] = 5;  // belt 15 -> physical 22

        var beltBuffer = CreateBufferWithState(data, 1, offset: 3, stoppedItemsActualIndex: 100);

        int index = 10;
        int num = 15;
        int num2 = 5;

        bool found = beltBuffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num2);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That(index).IsEqualTo(8);
        await TUnit.Assertions.Assert.That(num).IsEqualTo(13);
    }

    /*
     * Baseline: the entire scanned window is in the stopped region.
     * offset 3; stoppedItemsActualIndex 16 -> stopped region starts at belt 6.
     * Zero at belt 13 -> index==8, num==13.
     */
    [Test]
    public async Task TryFindIndexOfFirstPreviousZeroValue_WithEntirelyStoppedRegion_ExpectFindsZero()
    {
        byte[] data = new byte[30];
        data[11] = 5;  // belt 11 -> physical 21
        data[12] = 5;  // belt 12 -> physical 22
        data[13] = 0;  // belt 13 -> physical 23 (zero)
        data[14] = 5;  // belt 14 -> physical 24
        data[15] = 5;  // belt 15 -> physical 25

        var beltBuffer = CreateBufferWithState(data, 1, offset: 3, stoppedItemsActualIndex: 16);

        int index = 10;
        int num = 15;
        int num2 = 5;

        bool found = beltBuffer.TryFindIndexOfFirstPreviousZeroValue(ref index, ref num, num2);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That(index).IsEqualTo(8);
        await TUnit.Assertions.Assert.That(num).IsEqualTo(13);
    }

    // ================================================================
    // Difference #3: GetCargoAtIndex / TryGetCargo region-boundary read
    // ================================================================

    /*
     * A cargo whose 250 padding byte is at belt 10 (moving region) but whose data bytes are at
     * belts 11-14 (stopped region).
     *
     *   belt 10 -> physical 17 = 250
     *   belt 11 -> physical 21 =  8   (item low byte)
     *   belt 12 -> physical 22 =  1   (item high byte)
     *   belt 13 -> physical 23 =  3   (stack + 1)
     *   belt 14 -> physical 24 =  2   (inc + 1)
     *
     * GetCargoAtIndex(10) must return cargo (item 7, stack 2, inc 1).
     * The current code reads via TryGetCargo(10) -> GetActualIndex(10)+1 = physical 18, which is
     * wrong by _offset; the fix (read GetActualIndex(11)) lands on physical 21 and is correct.
     */
    [Test]
    public async Task GetCargoAtIndex_WithCargoStraddlingStoppedRegion_ExpectReturnsCargo()
    {
        byte[] data = new byte[30];
        data[7] = 250;  // physical 17 -> belt 10, padding 250
        data[11] = 8;   // physical 21 -> belt 11, item low  (7 & 0x7F) + 1
        data[12] = 1;   // physical 22 -> belt 12, item high (7 >> 7) + 1
        data[13] = 3;   // physical 23 -> belt 13, stack + 1  (2 + 1)
        data[14] = 2;   // physical 24 -> belt 14, inc + 1    (1 + 1)
        data[15] = 255; // physical 25 -> belt 15, end marker

        var beltBuffer = CreateBufferWithState(data, 1, offset: 3, stoppedItemsActualIndex: 21);
        var path = CreatePath(beltBuffer, 30);

        bool found = path.GetCargoAtIndex(10, out OptimizedCargo cargo, out _, out _, out _);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That((int)cargo.Item).IsEqualTo(7);
        await TUnit.Assertions.Assert.That((int)cargo.Stack).IsEqualTo(2);
        await TUnit.Assertions.Assert.That((int)cargo.Inc).IsEqualTo(1);
    }

    // ================================================================
    // Difference #2: QueryItemAtIndex off-by-one (regression / documentation)
    // ================================================================

    /*
     * A well-formed cargo at the head of the belt. Both the current code (off by one) and the
     * fix agree here because the cargo is found immediately. This documents the expected
     * boundary behavior; the off-by-one itself is masked by the cargo layout invariants, so
     * this test passes on both versions.
     */
    [Test]
    public async Task QueryItemAtIndex_WithCargoAtHead_ExpectReturnsCargo()
    {
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(new byte[20], 1);
        beltBuffer.SetCargoWithPadding(0, new OptimizedCargo(7, 2, 1));
        var path = CreatePath(beltBuffer, 20);

        bool found = path.QueryItemAtIndex(0, out OptimizedCargo cargo, out _);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That((int)cargo.Item).IsEqualTo(7);
        await TUnit.Assertions.Assert.That((int)cargo.Stack).IsEqualTo(2);
        await TUnit.Assertions.Assert.That((int)cargo.Inc).IsEqualTo(1);
    }

    // Same as above, but querying from inside the cargo's data region (index 5).
    [Test]
    public async Task QueryItemAtIndex_WithQueryInsideCargoData_ExpectReturnsCargo()
    {
        var beltBuffer = BeltBuffer.CreateFromExistingBuffer(new byte[20], 1);
        beltBuffer.SetCargoWithPadding(0, new OptimizedCargo(7, 2, 1));
        var path = CreatePath(beltBuffer, 20);

        bool found = path.QueryItemAtIndex(5, out OptimizedCargo cargo, out _);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That((int)cargo.Item).IsEqualTo(7);
        await TUnit.Assertions.Assert.That((int)cargo.Stack).IsEqualTo(2);
        await TUnit.Assertions.Assert.That((int)cargo.Inc).IsEqualTo(1);
    }
}
