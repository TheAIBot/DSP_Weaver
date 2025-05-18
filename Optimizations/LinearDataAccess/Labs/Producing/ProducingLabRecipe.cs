using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Labs.Producing;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ProducingLabRecipe : IEqualityComparer<ProducingLabRecipe>
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

    public bool Equals(ProducingLabRecipe x, ProducingLabRecipe y)
    {
        return x.RecipeId == y.RecipeId &&
               x.TimeSpend == y.TimeSpend &&
               x.ExtraTimeSpend == y.ExtraTimeSpend &&
               x.Speed == y.Speed &&
               x.Productive == y.Productive &&
               x.Requires.SequenceEqual(y.Requires) &&
               x.RequireCounts.SequenceEqual(y.RequireCounts) &&
               x.Products.SequenceEqual(y.Products) &&
               x.ProductCounts.SequenceEqual(y.ProductCounts);
    }

    public int GetHashCode(ProducingLabRecipe obj)
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

    public override bool Equals(object obj)
    {
        if (obj is not ProducingLabRecipe other)
        {
            return false;
        }

        return Equals(this, other);
    }

    public override int GetHashCode()
    {
        return GetHashCode(this);
    }
}
