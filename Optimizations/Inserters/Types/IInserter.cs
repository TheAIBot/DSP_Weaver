using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.NeedsSystem;

namespace Weaver.Optimizations.Inserters.Types;

internal interface IInserter<TInserter, TInserterGrade>
    where TInserter : struct, IInserter<TInserter, TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>
{
    short grade { get; }
    int pickOffset { get; }
    int insertOffset { get; }

    TInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade);

    void Update(PlanetFactory planet,
                InserterExecutor<TInserter, TInserterGrade> inserterExecutor,
                float power,
                int inserterIndex,
                ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                ref readonly TInserterGrade inserterGrade,
                ref OptimizedInserterStage stage,
                InserterConnections[] insertersConnections,
                ref readonly SubFactoryNeeds subFactoryNeeds);

    void Save(ref InserterComponent inserter, EInserterStage inserterStage);
}
