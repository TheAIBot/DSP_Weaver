using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Storages;

internal sealed class OptimizedStorage
{
    public static bool TakeTailItems(StorageComponent storageComponent,
                                     ref int itemId,
                                     ref int count,
                                     ComponentNeeds componentNeeds,
                                     short[] needsPatterns,
                                     int needsSize,
                                     out int inc,
                                     bool useBan = false)
    {
        inc = 0;
        if (count == 0)
        {
            itemId = 0;
            count = 0;
            return false;
        }
        bool result = false;
        int num = count;
        count = 0;
        for (int num2 = (useBan ? (storageComponent.size - storageComponent.bans - 1) : (storageComponent.size - 1)); num2 >= 0; num2--)
        {
            if (storageComponent.grids[num2].itemId != 0 && storageComponent.grids[num2].count != 0 && (itemId == 0 || storageComponent.grids[num2].itemId == itemId))
            {
                result = true;
                int gridItemIndex = storageComponent.grids[num2].itemId;
                if (IsInNeed(gridItemIndex, componentNeeds, needsPatterns, needsSize))
                {
                    itemId = storageComponent.grids[num2].itemId;
                    if (storageComponent.grids[num2].count > num)
                    {
                        inc += storageComponent.split_inc(ref storageComponent.grids[num2].count, ref storageComponent.grids[num2].inc, num);
                        count += num;
                        break;
                    }
                    inc += storageComponent.grids[num2].inc;
                    count += storageComponent.grids[num2].count;
                    num -= storageComponent.grids[num2].count;
                    storageComponent.grids[num2].itemId = storageComponent.grids[num2].filter;
                    storageComponent.grids[num2].count = 0;
                    storageComponent.grids[num2].inc = 0;
                    if (storageComponent.grids[num2].filter == 0)
                    {
                        storageComponent.grids[num2].stackSize = 0;
                    }
                }
            }
        }
        if (count == 0)
        {
            itemId = 0;
            count = 0;
        }
        else
        {
            storageComponent.lastFullItem = -1;
            storageComponent.NotifyStorageChange();
        }
        return result;
    }

    public static bool TakeTailFuel(StorageComponent storageComponent,
                                    ref int itemId,
                                    ref int count,
                                    OptimizedItemId[]? fuelMask,
                                    out int inc,
                                    bool useBan = false)
    {
        inc = 0;
        if (count == 0)
        {
            itemId = 0;
            count = 0;
            return false;
        }
        bool result = false;
        int num = count;
        count = 0;
        int num2 = (useBan ? (storageComponent.size - storageComponent.bans - 1) : (storageComponent.size - 1));
        for (int num3 = num2; num3 >= 0; num3--)
        {
            if (storageComponent.grids[num3].itemId != 0 && storageComponent.grids[num3].count != 0 && (itemId == 0 || storageComponent.grids[num3].itemId == itemId))
            {
                result = true;
                for (int i = 0; i < fuelMask.Length; i++)
                {
                    if (fuelMask[i].ItemIndex == storageComponent.grids[num3].itemId)
                    {
                        itemId = storageComponent.grids[num3].itemId;
                        if (storageComponent.grids[num3].count > num)
                        {
                            inc += storageComponent.split_inc(ref storageComponent.grids[num3].count, ref storageComponent.grids[num3].inc, num);
                            count += num;
                            num = 0;
                            break;
                        }
                        inc += storageComponent.grids[num3].inc;
                        count += storageComponent.grids[num3].count;
                        num -= storageComponent.grids[num3].count;
                        storageComponent.grids[num3].itemId = storageComponent.grids[num3].filter;
                        storageComponent.grids[num3].count = 0;
                        storageComponent.grids[num3].inc = 0;
                        if (storageComponent.grids[num3].filter == 0)
                        {
                            storageComponent.grids[num3].stackSize = 0;
                        }
                    }
                }
            }
        }
        if (count == 0)
        {
            itemId = 0;
            count = 0;
        }
        else
        {
            storageComponent.lastFullItem = -1;
            storageComponent.NotifyStorageChange();
        }
        return result;
    }

    private static bool IsInNeed(int productItemIndex,
                                 ComponentNeeds componentNeeds,
                                 short[] needsPatterns,
                                 int needsSize)
    {
        for (int i = 0; i < needsSize; i++)
        {
            if (componentNeeds.GetNeeds(i) && needsPatterns[componentNeeds.PatternIndex + i] == productItemIndex)
            {
                return true;
            }
        }

        return false;
    }
}
