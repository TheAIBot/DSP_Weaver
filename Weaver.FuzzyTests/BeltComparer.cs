using System;
using System.Linq;
using System.Threading.Tasks;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.StaticData;

namespace Weaver.FuzzyTests;

internal sealed class BeltComparer
{
    private readonly GameCode.CargoContainer _cargoContainer;
    private readonly GameCode.CargoPath _original;
    // Can't be readonly because it is a mutable struct
    private OptimizedCargoPath _optimized;
    private long _time;

    public BeltComparer(BeltChunk[] beltChunks, long time)
    {
        int[] chunks = new int[beltChunks.Length * 3];
        int usedLength = 0;
        int maxSpeed = 0;
        for (int i = 0; i < beltChunks.Length; i++)
        {
            chunks[i * 3 + 0] = usedLength;
            chunks[i * 3 + 1] = beltChunks[i].Length;
            chunks[i * 3 + 2] = beltChunks[i].Speed;

            usedLength += beltChunks[i].Length;
            maxSpeed = Math.Max(maxSpeed, beltChunks[i].Speed);
        }

        _cargoContainer = new GameCode.CargoContainer();
        _original = new GameCode.CargoPath(_cargoContainer, chunks, beltChunks.Length, usedLength, true);
        _optimized = new OptimizedCargoPath(BeltBuffer.CreateFromExistingBuffer(new byte[usedLength], maxSpeed),
                                            new ReadonlyArray<int>(chunks),
                                            outputIndex: -1,
                                            closed: true,
                                            bufferLength: usedLength,
                                            chunkCount: beltChunks.Length,
                                            outputCargoPathIndex: BeltIndex.NoBelt,
                                            outputChunk: -1,
                                            lastUpdateFrameOdd: false);
        _time = time;
    }

    public bool TryInsertItem(int index, TestCargo insert)
    {
        bool oldInserted = _original.TryInsertItem(index, insert.Item, insert.Stack, insert.Inc);
        bool optimizedInserted = _optimized.TryInsertItem(index, insert.Item, insert.Stack, insert.Inc);
        return oldInserted == optimizedInserted;
    }

    public bool TryInsertItemWithStackIncreasement(int index, int maxStack, TestCargo insert)
    {
        int oldStack = insert.Stack;
        int oldInc = insert.Inc;
        _original.TryInsertItemWithStackIncreasement(index, insert.Item, maxStack, ref oldStack, ref oldInc);

        int optimizedStack = insert.Stack;
        int optimizedInc = insert.Inc;
        _optimized.TryInsertItemWithStackIncreasement(index, insert.Item, maxStack, ref optimizedStack, ref optimizedInc);

        return oldStack == optimizedStack &&
               oldInc == optimizedInc;
    }

    public bool TryGetItem(int index)
    {
        GameCode.Cargo? cargo = null;
        int originalCargoIndex = _original.TryPickCargo(index, 10);
        if (originalCargoIndex != -1)
        {
            cargo = _cargoContainer.cargoPool[originalCargoIndex];
        }

        _optimized.TryPickItem(index, 10, out OptimizedCargo optimizedCargo);
        return AreSame(cargo, optimizedCargo);
    }

    public bool TryInsertAtHeadFillBlank(TestCargo insert)
    {
        bool oldInserted = _original.TryInsertItemAtHeadAndFillBlank(insert.Item, insert.Stack, insert.Inc);
        bool optimizedInserted = _optimized.TryInsertItemAtHeadAndFillBlank(insert.Item, insert.Stack, insert.Inc);
        return oldInserted == optimizedInserted;
    }

    public bool TryInsertItemAtHead(TestCargo insert)
    {
        bool oldInserted = _original.TryInsertItemAtHead(insert.Item, insert.Stack, insert.Inc);
        bool optimizedInserted = _optimized.TryInsertItemAtHead(insert.Item, insert.Stack, insert.Inc);
        return oldInserted == optimizedInserted;
    }

    public bool TryUpdateItemAtHeadAndFillBlank(int maxStack, TestCargo insert)
    {
        bool oldInserted = _original.TryUpdateItemAtHeadAndFillBlank(insert.Item, maxStack, insert.Stack, insert.Inc);
        bool optimizedInserted = _optimized.TryUpdateItemAtHeadAndFillBlank(insert.Item, maxStack, insert.Stack, insert.Inc);
        return oldInserted == optimizedInserted;
    }

    public bool TryQueryItem(int index)
    {
        int oldItem = _original.QueryItemAtIndex(index, out byte oldStack, out byte oldInc);
        bool optimizedFound = _optimized.QueryItemAtIndex(index, out OptimizedCargo optimizedCargo, out _);

        if (oldItem == 0)
        {
            return !optimizedFound;
        }
        if (!optimizedFound)
        {
            return false;
        }

        return oldItem == optimizedCargo.Item &&
               oldStack == optimizedCargo.Stack &&
               oldInc == optimizedCargo.Inc;
    }

    public bool TryGetCargoAtIndex(int index)
    {
        bool oldFound = _original.GetCargoAtIndex(index, out GameCode.Cargo oldCargo, out _, out _);
        bool optimizedFound = _optimized.GetCargoAtIndex(index, out OptimizedCargo optimizedCargo, out _, out _, out _);

        if (oldFound != optimizedFound)
        {
            return false;
        }
        if (!oldFound)
        {
            return true;
        }

        return oldCargo.item == optimizedCargo.Item &&
               oldCargo.stack == optimizedCargo.Stack &&
               oldCargo.inc == optimizedCargo.Inc;
    }

    public bool TryRemoveCargoAtIndex(int index)
    {
        bool oldRemoved = _original.RemoveCargoAtIndex(index);
        bool optimizedRemoved = _optimized.RemoveCargoAtIndex(index);
        return oldRemoved == optimizedRemoved;
    }


    public void Update()
    {
        _original.Update(_time);
        _optimized.Update([], _time);

        _time++;
    }

    public async Task AssertEqualAsync()
    {
        await IsTruePrintBeltsIfFalseAsync(_optimized.buffer.IsAllCargoItemsValid);

        for (int i = 0; i < _original.buffer.Length; i++)
        {
            await IsTruePrintBeltsIfFalseAsync(() => TryQueryItem(i));
        }
    }

    public Task IsTruePrintBeltsIfFalseAsync(Func<bool> operation)
    {
        return IsTrueAsync(() => Task.FromResult(operation()));
    }

    public async Task IsTrueAsync(Func<Task<bool>> operation)
    {
        bool result = await operation();
        if (!result)
        {
            await TUnit.Assertions.Assert.That(result)
                                         .IsTrue()
                                         .Because($"""
                                                      Optimized: [{string.Join(", ", _optimized.buffer.GetBytesAsArray().Select(x => $"{x,3}"))}]
                                                      Old:       [{string.Join(", ", _original.buffer.Select(x => $"{x,3}"))}]
                                                      """);
        }
    }

    private static bool AreSame(GameCode.Cargo? cargo, OptimizedCargo optimizedCargo)
    {
        if (cargo == null &&
            optimizedCargo.Item == 0 &&
            optimizedCargo.Stack == 0 &&
            optimizedCargo.Inc == 0)
        {
            return true;
        }

        if (cargo != null &&
            cargo.Value.item == optimizedCargo.Item &&
            cargo.Value.stack == optimizedCargo.Stack &&
            cargo.Value.inc == optimizedCargo.Inc)
        {
            return true;
        }

        return false;
    }
}