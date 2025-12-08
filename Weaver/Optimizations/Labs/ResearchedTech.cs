namespace Weaver.Optimizations.Labs;

internal record struct ResearchedTech(int ResearchTechId, int PreviousTechLevel, TechState State, TechProto Proto);
