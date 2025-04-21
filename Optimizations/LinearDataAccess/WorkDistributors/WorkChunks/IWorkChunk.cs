namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors.WorkChunks;

internal interface IWorkChunk
{
    void Execute(WorkerTimings workerTimings, long time);
    void TieToWorkStep(WorkStep workStep);
    bool Complete();

    void CompleteStep();
}
