namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal record struct WorkPlan(WorkType WorkType, int WorkTrackerIndex, int WorkIndex, int WorkParallelism);
