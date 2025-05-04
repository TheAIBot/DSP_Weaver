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
    Splitter = 11,
    Inserter = 12,
    Monitor = 13,
    SprayCoater = 14,
    Piler = 15,
    Miner = 16,
    Fractionator = 17,
    Dispenser = 18,
    Turret = 19,
    FieldGenerator = 20,
    BattleBase = 21,
    Marker = 22,
    VeinGroup = 23 // Ensure miners do not update vein group in parallel
}
