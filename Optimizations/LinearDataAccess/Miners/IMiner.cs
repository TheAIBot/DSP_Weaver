namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal interface IMiner
{
    int ProductCount { get; set; }
    float SpeedDamper { get; set; }
}
