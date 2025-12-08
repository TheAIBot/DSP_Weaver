using Weaver.Optimizations.NeedsSystem;

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
