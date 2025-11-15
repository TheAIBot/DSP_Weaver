using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.Statistics;

internal sealed class PlanetWideProductionRegisterBuilder
{
    private readonly PlanetFactory _planet;
    private readonly Dictionary<int, int> _productIndexToOptimizedProductIndex = [];
    private readonly Dictionary<int, int> _consumeIndexToOptimizedConsumeIndex = [];
    private readonly Dictionary<int, ItemIdWithOptimizedRegisterIndex> _productIndexToItemIdWithOptimizedRegisterIndex = [];
    private readonly Dictionary<int, ItemIdWithOptimizedRegisterIndex> _consumeIndexToItemIdWithOptimizedRegisterIndex = [];
    private readonly List<OptimizedProductionStatistics> _optimizedProductionStatistics = [];

    public PlanetWideProductionRegisterBuilder(PlanetFactory planet)
    {
        _planet = planet;
    }

    public SubFactoryProductionRegisterBuilder GetSubFactoryBuilder()
    {
        return new SubFactoryProductionRegisterBuilder(this);
    }

    public int AddProduct(int productIndex)
    {
        if (!_productIndexToOptimizedProductIndex.TryGetValue(productIndex, out int optimizedProductIndex))
        {
            optimizedProductIndex = _productIndexToOptimizedProductIndex.Count;
            _productIndexToOptimizedProductIndex.Add(productIndex, optimizedProductIndex);
        }

        return optimizedProductIndex;
    }

    public int AddConsume(int consumeIndex)
    {
        if (!_consumeIndexToOptimizedConsumeIndex.TryGetValue(consumeIndex, out int optimizedConsumeIndex))
        {
            optimizedConsumeIndex = _consumeIndexToOptimizedConsumeIndex.Count;
            _consumeIndexToOptimizedConsumeIndex.Add(consumeIndex, optimizedConsumeIndex);
        }

        return optimizedConsumeIndex;
    }

    public void AddOptimizedProductionStatistics(ref readonly OptimizedProductionStatistics optimizedProductionStatistics)
    {
        _optimizedProductionStatistics.Add(optimizedProductionStatistics);
    }

    public void AdditionalProductItemsIdToWatch(int itemId)
    {
        if (_productIndexToItemIdWithOptimizedRegisterIndex.TryGetValue(itemId, out _))
        {
            return;
        }

        int optimizedProductRegisterIndex = AddProduct(itemId);
        _productIndexToItemIdWithOptimizedRegisterIndex.Add(itemId, new ItemIdWithOptimizedRegisterIndex(itemId, optimizedProductRegisterIndex));
    }

    public void AdditionalConsumeItemsIdToWatch(int itemId)
    {
        if (_consumeIndexToItemIdWithOptimizedRegisterIndex.TryGetValue(itemId, out _))
        {
            return;
        }

        int optimizedConsumeRegisterIndex = AddConsume(itemId);
        _consumeIndexToItemIdWithOptimizedRegisterIndex.Add(itemId, new ItemIdWithOptimizedRegisterIndex(itemId, optimizedConsumeRegisterIndex));
    }

    public OptimizedPlanetWideProductionStatistics Build(UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        if (CanPickupItemsFromEnemy(_planet))
        {
            int[] enemyDropItemIds = ItemProto.enemyDropRangeTable;
            for (int i = 0; i < enemyDropItemIds.Length; i++)
            {
                AdditionalProductItemsIdToWatch(enemyDropItemIds[i]);
            }
        }

        if (HasStationConsumingWarpers(_planet))
        {
            const int warperItemId = 1210;
            AdditionalConsumeItemsIdToWatch(warperItemId);
        }

        OptimizedItemId[] productIndexes = _productIndexToOptimizedProductIndex.Select(x => new OptimizedItemId(x.Key, x.Value))
                                                                               .OrderBy(x => x.ItemIndex)
                                                                               .ToArray();
        OptimizedItemId[] consumeIndexes = _consumeIndexToOptimizedConsumeIndex.Select(x => new OptimizedItemId(x.Key, x.Value))
                                                                               .OrderBy(x => x.ItemIndex)
                                                                               .ToArray();

        ItemIdWithOptimizedRegisterIndex[] additionalProductsToWatch = _productIndexToItemIdWithOptimizedRegisterIndex.Values
                                                                                                                      .OrderBy(x => x.ItemIndex)
                                                                                                                      .ToArray();
        ItemIdWithOptimizedRegisterIndex[] additionalConsumesToWatch = _consumeIndexToItemIdWithOptimizedRegisterIndex.Values
                                                                                                                      .OrderBy(x => x.ItemIndex)
                                                                                                                      .ToArray();

        return new OptimizedPlanetWideProductionStatistics(universeStaticDataBuilder.DeduplicateArray(productIndexes),
                                                           universeStaticDataBuilder.DeduplicateArray(consumeIndexes),
                                                           universeStaticDataBuilder.DeduplicateArray(additionalProductsToWatch),
                                                           universeStaticDataBuilder.DeduplicateArray(additionalConsumesToWatch),
                                                           _optimizedProductionStatistics.ToArray(),
                                                           GameMain.statistics.production.factoryStatPool[_planet.index]);
    }

    private static bool CanPickupItemsFromEnemy(PlanetFactory planet)
    {
        if (planet.defenseSystem.battleBases.count == 0)
        {
            return false;
        }

        bool hasBattleBaseThatCanPickUpItems = false;
        for (int i = 1; i < planet.defenseSystem.battleBases.cursor; i++)
        {
            ref readonly BattleBaseComponent component = ref planet.defenseSystem.battleBases.buffer[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            if (component.autoPickEnabled)
            {
                hasBattleBaseThatCanPickUpItems = true;
            }
        }

        if (!hasBattleBaseThatCanPickUpItems)
        {
            return false;
        }

        // Can't check if there is any enemies because they might
        // show up at a later time.
        return true;
    }

    private static bool HasStationConsumingWarpers(PlanetFactory planet)
    {
        for (int i = 1; i < planet.transport.stationCursor; i++)
        {
            StationComponent component = planet.transport.stationPool[i];
            if (component == null || component.id != i)
            {
                continue;
            }

            // Not possible to check any further because a stations configuration
            // can be changed at any time from anywhere in the star cluster.
            return true;
        }

        return false;
    }
}

internal readonly struct ItemIdWithOptimizedRegisterIndex : IEquatable<ItemIdWithOptimizedRegisterIndex>, IMemorySize
{
    public readonly short ItemIndex;
    public readonly short OptimizedRegisterIndex;

    public ItemIdWithOptimizedRegisterIndex(int itemIndex, int optimizedRegisterIndex)
    {
        if (itemIndex > short.MaxValue || itemIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(itemIndex)} first in a short is not correct.");
        }
        if (optimizedRegisterIndex > short.MaxValue || optimizedRegisterIndex < short.MinValue)
        {
            throw new InvalidOperationException($"Assumption that {nameof(optimizedRegisterIndex)} first in a short is not correct.");
        }

        ItemIndex = (short)itemIndex;
        OptimizedRegisterIndex = (short)optimizedRegisterIndex;
    }

    public int GetSize() => Marshal.SizeOf<ItemIdWithOptimizedRegisterIndex>();

    public readonly bool Equals(ItemIdWithOptimizedRegisterIndex other)
    {
        return ItemIndex == other.ItemIndex &&
               OptimizedRegisterIndex == other.OptimizedRegisterIndex;
    }

    public override readonly bool Equals(object obj)
    {
        return obj is ItemIdWithOptimizedRegisterIndex other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(ItemIndex, OptimizedRegisterIndex);
    }
}