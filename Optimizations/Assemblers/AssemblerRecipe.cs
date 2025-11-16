using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Labs.Producing;
using Weaver.Optimizations.StaticData;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Assemblers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct AssemblerRecipe : IEquatable<AssemblerRecipe>, IMemorySize
{
    public readonly int RecipeId;
    public readonly ERecipeType RecipeType;
    public readonly int TimeSpend;
    public readonly int ExtraTimeSpend;
    public readonly bool Productive;
    public readonly OptimizedItemId[] Requires;
    public readonly ReadonlyArray<int> RequireCounts;
    public readonly OptimizedItemId[] Products;
    public readonly ReadonlyArray<int> ProductCounts;

    public AssemblerRecipe(int recipeId,
                           ERecipeType recipeType,
                           int timeSpend,
                           int extraTimeSpend,
                           bool productive,
                           OptimizedItemId[] requires,
                           ReadonlyArray<int> requireCounts,
                           OptimizedItemId[] products,
                           ReadonlyArray<int> productCounts)
    {
        RecipeId = recipeId;
        RecipeType = recipeType;
        TimeSpend = timeSpend;
        ExtraTimeSpend = extraTimeSpend;
        Productive = productive;
        Requires = requires;
        RequireCounts = requireCounts;
        Products = products;
        ProductCounts = productCounts;
    }

    public int GetSize()
    {
        int size = Marshal.SizeOf<AssemblerRecipe>();
        size += Marshal.SizeOf<OptimizedItemId>() * Requires.Length;
        size += Marshal.SizeOf<int>() * RequireCounts.Length;
        size += Marshal.SizeOf<OptimizedItemId>() * Products.Length;
        size += Marshal.SizeOf<int>() * ProductCounts.Length;

        return size;
    }

    public readonly bool Equals(AssemblerRecipe other)
    {
        return RecipeId == other.RecipeId &&
               RecipeType == other.RecipeType &&
               TimeSpend == other.TimeSpend &&
               ExtraTimeSpend == other.ExtraTimeSpend &&
               Productive == other.Productive &&
               Requires.SequenceEqual(other.Requires) &&
               RequireCounts.SequenceEqual(other.RequireCounts) &&
               Products.SequenceEqual(other.Products) &&
               ProductCounts.SequenceEqual(other.ProductCounts);
    }

    public override readonly bool Equals(object obj)
    {
        return obj is AssemblerRecipe other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(RecipeId);
        hashCode.Add(RecipeType);
        hashCode.Add(TimeSpend);
        hashCode.Add(ExtraTimeSpend);
        hashCode.Add(Productive);
        for (int i = 0; i < Requires.Length; i++)
        {
            hashCode.Add(Requires[i]);
        }
        for (int i = 0; i < RequireCounts.Length; i++)
        {
            hashCode.Add(RequireCounts[i]);
        }
        for (int i = 0; i < Products.Length; i++)
        {
            hashCode.Add(Products[i]);
        }
        for (int i = 0; i < ProductCounts.Length; i++)
        {
            hashCode.Add(ProductCounts[i]);
        }

        return hashCode.ToHashCode();
    }

    public void Print()
    {
        WeaverFixes.Logger.LogInfo($"{nameof(RecipeId)}: {RecipeId}");
        WeaverFixes.Logger.LogInfo($"{nameof(RecipeType)}: {RecipeType}");
        WeaverFixes.Logger.LogInfo($"{nameof(TimeSpend)}: {TimeSpend}");
        WeaverFixes.Logger.LogInfo($"{nameof(ExtraTimeSpend)}: {ExtraTimeSpend}");
        WeaverFixes.Logger.LogInfo($"{nameof(Productive)}: {Productive}");
        WeaverFixes.Logger.LogInfo($"{nameof(Requires)}: [{(Requires != null ? string.Join(", ", Requires) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(RequireCounts)}: [{string.Join(", ", RequireCounts)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(Products)}: [{(Products != null ? string.Join(", ", Products) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(ProductCounts)}: [{string.Join(", ", ProductCounts)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(GetHashCode)}: {GetHashCode()}");
    }
}