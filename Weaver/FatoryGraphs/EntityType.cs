namespace Weaver.FatoryGraphs;

internal enum EntityType : byte
{
    None = 0,
    Belt = 1,
    Assembler = 2,
    Ejector = 3,
    Silo = 4,
    ProducingLab = 5,
    ResearchingLab = 6,
    Storage = 7,
    Tank = 8,
    Station = 9,
    PowerGenerator = 10,
    FuelPowerGenerator = 11,
    PowerExchanger = 12,
    Splitter = 13,
    Inserter = 14,
    Monitor = 15,
    SprayCoater = 16,
    Piler = 17,
    Miner = 18,
    Fractionator = 19,
    Dispenser = 20,
    Turret = 21,
    FieldGenerator = 22,
    BattleBase = 23,
    Marker = 24,
    VeinGroup = 25 // Ensure miners do not update vein group in parallel
}
