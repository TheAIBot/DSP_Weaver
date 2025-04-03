namespace Weaver.Optimizations.LinearDataAccess.Inserters.Types;

internal interface IInserter<T>
    where T : struct, IInserter<T>
{
    byte grade { get; }

    T Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade);

    void Update(PlanetFactory planet,
                InserterExecutor<T> inserterExecutor,
                float power,
                int inserterIndex,
                ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                InserterGrade inserterGrade,
                ref OptimizedInserterStage stage);
}
