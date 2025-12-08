namespace Weaver.Optimizations.Miners;

internal interface IMiner
{
    int ProductCount { get; set; }
    float SpeedDamper { get; set; }
}
