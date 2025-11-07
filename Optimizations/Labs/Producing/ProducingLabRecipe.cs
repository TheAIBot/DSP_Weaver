using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Labs.Producing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct ProducingLabRecipe : IEquatable<ProducingLabRecipe>
{
    public readonly int RecipeId;
    public readonly int TimeSpend;
    public readonly int ExtraTimeSpend;
    public readonly int Speed;
    public readonly bool Productive;
    public readonly OptimizedItemId[] Requires;
    public readonly int[] RequireCounts;
    public readonly OptimizedItemId[] Products;
    public readonly int[] ProductCounts;

    public ProducingLabRecipe(ref readonly LabComponent lab, SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder)
    {
        RecipeId = lab.recipeId;
        TimeSpend = lab.timeSpend;
        ExtraTimeSpend = lab.extraTimeSpend;
        Speed = lab.speed;
        Productive = lab.productive;
        Requires = subFactoryProductionRegisterBuilder.AddConsume(lab.requires);
        RequireCounts = lab.requireCounts;
        Products = subFactoryProductionRegisterBuilder.AddProduct(lab.products);
        ProductCounts = lab.productCounts;
    }

    public readonly bool Equals(ProducingLabRecipe other)
    {
        return RecipeId == other.RecipeId &&
               TimeSpend == other.TimeSpend &&
               ExtraTimeSpend == other.ExtraTimeSpend &&
               Speed == other.Speed &&
               Productive == other.Productive &&
               Requires.SequenceEqual(other.Requires) &&
               RequireCounts.SequenceEqual(other.RequireCounts) &&
               Products.SequenceEqual(other.Products) &&
               ProductCounts.SequenceEqual(other.ProductCounts);
    }

    public override readonly bool Equals(object obj)
    {
        return obj is ProducingLabRecipe other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(RecipeId);
        hashCode.Add(TimeSpend);
        hashCode.Add(ExtraTimeSpend);
        hashCode.Add(Speed);
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
}
