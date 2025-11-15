namespace Weaver.Optimizations.PowerSystems;

internal sealed class SubFactoryPowerConsumption
{
    public short[] AssemblerPowerConsumerTypeIndexes { get; }
    public short[] InserterBiPowerConsumerTypeIndexes { get; }
    public short[] InserterPowerConsumerTypeIndexes { get; }
    public short[] ProducingLabPowerConsumerTypeIndexes { get; }
    public short[] ResearchingLabPowerConsumerTypeIndexes { get; }
    public short[] SpraycoaterPowerConsumerTypeIndexes { get; }
    public short[] FractionatorPowerConsumerTypeIndexes { get; }
    public short[] EjectorPowerConsumerTypeIndexes { get; }
    public short[] SiloPowerConsumerTypeIndexes { get; }
    public short[] PilerPowerConsumerTypeIndexes { get; }
    public short[] MonitorPowerConsumerTypeIndexes { get; }
    public short[] WaterMinerPowerConsumerTypeIndexes { get; }
    public short[] OilMinerPowerConsumerTypeIndexes { get; }
    public short[] BeltVeinMinerPowerConsumerTypeIndexes { get; }
    public short[] StationVeinMinerPowerConsumerTypeIndexes { get; }
    public long[] NetworksPowerConsumption { get; }

    public SubFactoryPowerConsumption(short[] assemblerPowerConsumerTypeIndexes,
                                      short[] inserterBiPowerConsumerTypeIndexes,
                                      short[] inserterPowerConsumerTypeIndexes,
                                      short[] producingLabPowerConsumerTypeIndexes,
                                      short[] researchingLabPowerConsumerTypeIndexes,
                                      short[] spraycoaterPowerConsumerTypeIndexes,
                                      short[] fractionatorPowerConsumerTypeIndexes,
                                      short[] ejectorPowerConsumerTypeIndexes,
                                      short[] siloPowerConsumerTypeIndexes,
                                      short[] pilerPowerConsumerTypeIndexes,
                                      short[] monitorPowerConsumerTypeIndexes,
                                      short[] waterMinerPowerConsumerTypeIndexes,
                                      short[] oilMinerPowerConsumerTypeIndexes,
                                      short[] beltVeinMinerPowerConsumerTypeIndexes,
                                      short[] stationVeinMinerPowerConsumerTypeIndexes,
                                      long[] networksPowerConsumption)
    {
        AssemblerPowerConsumerTypeIndexes = assemblerPowerConsumerTypeIndexes;
        InserterBiPowerConsumerTypeIndexes = inserterBiPowerConsumerTypeIndexes;
        InserterPowerConsumerTypeIndexes = inserterPowerConsumerTypeIndexes;
        ProducingLabPowerConsumerTypeIndexes = producingLabPowerConsumerTypeIndexes;
        ResearchingLabPowerConsumerTypeIndexes = researchingLabPowerConsumerTypeIndexes;
        SpraycoaterPowerConsumerTypeIndexes = spraycoaterPowerConsumerTypeIndexes;
        FractionatorPowerConsumerTypeIndexes = fractionatorPowerConsumerTypeIndexes;
        EjectorPowerConsumerTypeIndexes = ejectorPowerConsumerTypeIndexes;
        SiloPowerConsumerTypeIndexes = siloPowerConsumerTypeIndexes;
        PilerPowerConsumerTypeIndexes = pilerPowerConsumerTypeIndexes;
        MonitorPowerConsumerTypeIndexes = monitorPowerConsumerTypeIndexes;
        WaterMinerPowerConsumerTypeIndexes = waterMinerPowerConsumerTypeIndexes;
        OilMinerPowerConsumerTypeIndexes = oilMinerPowerConsumerTypeIndexes;
        BeltVeinMinerPowerConsumerTypeIndexes = beltVeinMinerPowerConsumerTypeIndexes;
        StationVeinMinerPowerConsumerTypeIndexes = stationVeinMinerPowerConsumerTypeIndexes;
        NetworksPowerConsumption = networksPowerConsumption;
    }
}
