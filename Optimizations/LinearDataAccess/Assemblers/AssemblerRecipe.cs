using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Assemblers;

[StructLayout(LayoutKind.Auto)]
internal readonly struct AssemblerRecipe : IEqualityComparer<AssemblerRecipe>
{
    public readonly int RecipeId;
    public readonly ERecipeType RecipeType;
    public readonly int TimeSpend;
    public readonly int ExtraTimeSpend;
    public readonly bool Productive;
    public readonly int[] Requires;
    public readonly int[] RequireCounts;
    public readonly int[] Products;
    public readonly int[] ProductCounts;

    public AssemblerRecipe(int recipeId,
                           ERecipeType recipeType,
                           int timeSpend,
                           int extraTimeSpend,
                           bool productive,
                           int[] requires,
                           int[] requireCounts,
                           int[] products,
                           int[] productCounts)
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

    public bool Equals(AssemblerRecipe x, AssemblerRecipe y)
    {
        return x.RecipeId == y.RecipeId &&
               x.RecipeType == y.RecipeType &&
               x.TimeSpend == y.TimeSpend &&
               x.ExtraTimeSpend == y.ExtraTimeSpend &&
               x.Productive == y.Productive &&
               x.Requires.SequenceEqual(y.Requires) &&
               x.RequireCounts.SequenceEqual(y.RequireCounts) &&
               x.Products.SequenceEqual(y.Products) &&
               x.ProductCounts.SequenceEqual(y.ProductCounts);
    }

    public int GetHashCode(AssemblerRecipe obj)
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

    public override bool Equals(object obj)
    {
        if (obj is not AssemblerRecipe other)
        {
            return false;
        }

        return Equals(this, other);
    }

    public override int GetHashCode()
    {
        return GetHashCode(this);
    }

    public void Print()
    {
        WeaverFixes.Logger.LogInfo($"{nameof(RecipeId)}: {RecipeId}");
        WeaverFixes.Logger.LogInfo($"{nameof(RecipeType)}: {RecipeType}");
        WeaverFixes.Logger.LogInfo($"{nameof(TimeSpend)}: {TimeSpend}");
        WeaverFixes.Logger.LogInfo($"{nameof(ExtraTimeSpend)}: {ExtraTimeSpend}");
        WeaverFixes.Logger.LogInfo($"{nameof(Productive)}: {Productive}");
        WeaverFixes.Logger.LogInfo($"{nameof(Requires)}: [{(Requires != null ? string.Join(", ", Requires) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(RequireCounts)}: [{(RequireCounts != null ? string.Join(", ", RequireCounts) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(Products)}: [{(Products != null ? string.Join(", ", Products) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(ProductCounts)}: [{(ProductCounts != null ? string.Join(", ", ProductCounts) : null)}]");
        WeaverFixes.Logger.LogInfo($"{nameof(GetHashCode)}: {GetHashCode(this)}");
    }
}