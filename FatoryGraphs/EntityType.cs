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
    PowerExchanger = 11,
    Splitter = 12,
    Inserter = 13,
    Monitor = 14,
    SprayCoater = 15,
    Piler = 16,
    Miner = 17,
    Fractionator = 18,
    Dispenser = 19,
    Turret = 20,
    FieldGenerator = 21,
    BattleBase = 22,
    Marker = 23,
    VeinGroup = 24 // Ensure miners do not update vein group in parallel
}
