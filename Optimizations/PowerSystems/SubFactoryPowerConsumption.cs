namespace Weaver.Optimizations.PowerSystems;

internal sealed class SubFactoryPowerConsumption
{
    public PowerConsumerType[] PowerConsumerTypes { get; }
    public int[] AssemblerPowerConsumerTypeIndexes { get; }
    public int[] InserterBiPowerConsumerTypeIndexes { get; }
    public int[] InserterPowerConsumerTypeIndexes { get; }
    public int[] ProducingLabPowerConsumerTypeIndexes { get; }
    public int[] ResearchingLabPowerConsumerTypeIndexes { get; }
    public int[] SpraycoaterPowerConsumerTypeIndexes { get; }
    public int[] FractionatorPowerConsumerTypeIndexes { get; }
    public int[] EjectorPowerConsumerTypeIndexes { get; }
    public int[] SiloPowerConsumerTypeIndexes { get; }
    public int[] PilerPowerConsumerTypeIndexes { get; }
    public int[] MonitorPowerConsumerTypeIndexes { get; }
    public int[] WaterMinerPowerConsumerTypeIndexes { get; }
    public int[] OilMinerPowerConsumerTypeIndexes { get; }
    public int[] BeltVeinMinerPowerConsumerTypeIndexes { get; }
    public int[] StationVeinMinerPowerConsumerTypeIndexes { get; }
    public long[] NetworksPowerConsumption { get; }

    public SubFactoryPowerConsumption(PowerConsumerType[] powerConsumerTypes,
                                      int[] assemblerPowerConsumerTypeIndexes,
                                      int[] inserterBiPowerConsumerTypeIndexes,
                                      int[] inserterPowerConsumerTypeIndexes,
                                      int[] producingLabPowerConsumerTypeIndexes,
                                      int[] researchingLabPowerConsumerTypeIndexes,
                                      int[] spraycoaterPowerConsumerTypeIndexes,
                                      int[] fractionatorPowerConsumerTypeIndexes,
                                      int[] ejectorPowerConsumerTypeIndexes,
                                      int[] siloPowerConsumerTypeIndexes,
                                      int[] pilerPowerConsumerTypeIndexes,
                                      int[] monitorPowerConsumerTypeIndexes,
                                      int[] waterMinerPowerConsumerTypeIndexes,
                                      int[] oilMinerPowerConsumerTypeIndexes,
                                      int[] beltVeinMinerPowerConsumerTypeIndexes,
                                      int[] stationVeinMinerPowerConsumerTypeIndexes,
                                      long[] networksPowerConsumption)
    {
        PowerConsumerTypes = powerConsumerTypes;
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
