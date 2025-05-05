using System;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

internal sealed class OptimizedCargoContainer
{
    private int poolCapacity = 1024;
    public int cursor;
    public OptimizedCargo[] cargoPool;
    private int[] recycleIds;
    private int recycleBegin;
    private int recycleEnd;

    public int cargoCount => cursor - (recycleEnd - recycleBegin);

    public OptimizedCargoContainer()
    {
        cargoPool = new OptimizedCargo[poolCapacity];
        recycleIds = new int[poolCapacity];
    }

    public int AddCargo(short item, byte stack, byte inc)
    {
        int result;
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
                result = (recycleBegin = (recycleEnd = 0));
            }
            else if (recycleBegin == poolCapacity)
            {
                recycleBegin -= poolCapacity;
                recycleEnd -= poolCapacity;
            }
            result = num2;
        }
        else
        {
            recycleBegin = (recycleEnd = 0);
            int num3 = cursor++;
            cargoPool[num3].item = item;
            cargoPool[num3].stack = stack;
            cargoPool[num3].inc = inc;
            if (cursor == poolCapacity)
            {
                Expand2x();
            }
            result = num3;
        }
        return result;
    }

    public void AddItemStackToCargo(int cargoId, int itemId, int maxStack, ref int count, ref int inc)
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

    public void RemoveCargo(int index)
    {
        if (cargoPool != null && cargoPool[index].item != 0)
        {
            cargoPool[index].stack = 0;
            cargoPool[index].inc = 0;
            cargoPool[index].item = 0;
            int num = recycleEnd & (poolCapacity - 1);
            recycleIds[num] = index;
            recycleEnd++;
        }
    }

    public void Expand2x()
    {
        int num = poolCapacity << 1;
        OptimizedCargo[] sourceArray = cargoPool;
        cargoPool = new OptimizedCargo[num];
        recycleIds = new int[num];
        Array.Copy(sourceArray, cargoPool, poolCapacity);
        poolCapacity = num;
    }
}
