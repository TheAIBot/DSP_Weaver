namespace Weaver.Optimizations;

internal struct ServedData
{
    public short Served { get; set; }
    public short IncServed { get; set; }

    public ServedData(short served, short incServed)
    {
        Served = served;
        IncServed = incServed;
    }
}