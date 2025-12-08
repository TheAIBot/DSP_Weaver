using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal record struct PlanetWorkPlan(PlanetWorkManager PlanetWorkManager, IWorkChunk WorkChunk);
