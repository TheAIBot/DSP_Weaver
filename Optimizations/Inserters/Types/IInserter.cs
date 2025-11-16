using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Inserters;
using Weaver.Optimizations.NeedsSystem;

namespace Weaver.Optimizations.Inserters.Types;

internal interface IInserter<TInserter, TInserterGrade>
    where TInserter : struct, IInserter<TInserter, TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>, IMemorySize
{
    short grade { get; }
    int pickOffset { get; }
    int insertOffset { get; }

    TInserter Create(ref readonly InserterComponent inserter, int pickFromOffset, int insertIntoOffset, int grade);

    TInserterGrade[] GetInserterGrades(UniverseStaticData universeStaticData);

    void Update(PlanetFactory planet,
                InserterExecutor<TInserter, TInserterGrade> inserterExecutor,
                float power,
                int inserterIndex,
                ref InserterState inserterState,
                ref readonly TInserterGrade inserterGrade,
                ref OptimizedInserterStage stage,
                InserterConnections[] insertersConnections,
                SubFactoryNeeds subFactoryNeeds,
                OptimizedCargoPath[] optimizedCargoPaths);

    void Save(ref InserterComponent inserter, EInserterStage inserterStage);
}
