namespace Weaver.Optimizations.Inserters;

internal interface IInserterGrade<T>
    where T : IInserterGrade<T>
{
     T Create(ref InserterComponent inserter);
}