using Weaver.Optimizations.StaticData;

namespace Weaver.Optimizations.PowerSystems;

internal sealed class SubFactoryPowerConsumption
{
    public ReadonlyArray<short> AssemblerPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> InserterBiPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> InserterPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> ProducingLabPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> ResearchingLabPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> SpraycoaterPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> FractionatorPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> EjectorPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> SiloPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> PilerPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> MonitorPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> WaterMinerPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> OilMinerPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> BeltVeinMinerPowerConsumerTypeIndexes { get; }
    public ReadonlyArray<short> StationVeinMinerPowerConsumerTypeIndexes { get; }
    public long[] NetworksPowerConsumption { get; }

    public SubFactoryPowerConsumption(ReadonlyArray<short> assemblerPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> inserterBiPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> inserterPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> producingLabPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> researchingLabPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> spraycoaterPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> fractionatorPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> ejectorPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> siloPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> pilerPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> monitorPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> waterMinerPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> oilMinerPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> beltVeinMinerPowerConsumerTypeIndexes,
                                      ReadonlyArray<short> stationVeinMinerPowerConsumerTypeIndexes,
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
