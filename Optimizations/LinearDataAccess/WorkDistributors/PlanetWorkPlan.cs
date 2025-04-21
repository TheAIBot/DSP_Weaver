using Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal record struct PlanetWorkPlan(PlanetWorkManager PlanetWorkManager, IWorkChunk WorkChunk);
