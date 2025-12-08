using System;
using System.Threading;

namespace Weaver.FuzzyTests.GameCode;

public class CargoContainer
{
    private int poolCapacity = 1024;

    public int cursor;

    public Cargo[] cargoPool;

    private int[] recycleIds;

    private int recycleBegin;

    private int recycleEnd;

    public ItemPackage[] tmpCargos;

    public int tmpCargoCount;


    public SemaphoreSlim cargoContainer_sl = new SemaphoreSlim(1);

    public int cargoCount => cursor - (recycleEnd - recycleBegin);

    public CargoContainer()
    {
        cargoPool = new Cargo[poolCapacity];
        recycleIds = new int[poolCapacity];
        tmpCargos = new ItemPackage[128];
    }

    public int AddCargo(short item, byte stack, byte inc)
    {
        cargoContainer_sl.Wait();
        try
        {
            if (recycleEnd - recycleBegin > 0)
            {
                int num = recycleBegin & (poolCapacity - 1);
                int num2 = recycleIds[num];
                cargoPool[num2].item = item;
                cargoPool[num2].stack = stack;
                cargoPool[num2].inc = inc;
                recycleBegin++;
                if (recycleBegin == recycleEnd)
                {
                    recycleBegin = (recycleEnd = 0);
                }
                else if (recycleBegin == poolCapacity)
                {
                    recycleBegin -= poolCapacity;
                    recycleEnd -= poolCapacity;
                }
                return num2;
            }
            recycleBegin = (recycleEnd = 0);
            int num3 = cursor++;
            cargoPool[num3].item = item;
            cargoPool[num3].stack = stack;
            cargoPool[num3].inc = inc;
            if (cursor == poolCapacity)
            {
                Expand2x();
            }
            return num3;
        }
        finally
        {
            cargoContainer_sl.Release();
        }
    }

    public void AddItemStackToCargo(int cargoId, int itemId, int maxStack, ref int count, ref int inc)
    {
        cargoContainer_sl.Wait();
        try
        {
            if (cargoPool[cargoId].item == itemId && cargoPool[cargoId].stack < maxStack)
            {
                int num = maxStack - cargoPool[cargoId].stack;
                int num2 = inc;
                if (num < count)
                {
                    num2 = inc / count;
                    int num3 = inc - num2 * count;
                    count -= num;
                    num3 -= count;
                    num2 = ((num3 > 0) ? (num2 * num + num3) : (num2 * num));
                    inc -= num2;
                }
                else
                {
                    num = count;
                    count = 0;
                    inc = 0;
                }
                cargoPool[cargoId].stack += (byte)num;
                cargoPool[cargoId].inc += (byte)num2;
            }
        }
        finally
        {
            cargoContainer_sl.Release();
        }
    }

    public void RemoveCargo(int index)
    {
        cargoContainer_sl.Wait();
        try
        {
            if (cargoPool == null)
            {
                return;
            }
            if (cargoPool[index].item != 0)
            {
                cargoPool[index].stack = 0;
                cargoPool[index].inc = 0;
                cargoPool[index].item = 0;
                int num = recycleEnd & (poolCapacity - 1);
                recycleIds[num] = index;
                recycleEnd++;
            }
        }
        finally
        {
            cargoContainer_sl.Release();
        }
    }

    public void Expand2x()
    {
        int num = poolCapacity << 1;
        Cargo[] sourceArray = cargoPool;
        cargoPool = new Cargo[num];
        recycleIds = new int[num];
        Array.Copy(sourceArray, cargoPool, poolCapacity);
        poolCapacity = num;
    }
}