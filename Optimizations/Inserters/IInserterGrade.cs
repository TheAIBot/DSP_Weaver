using System;

namespace Weaver.Optimizations.Inserters;

internal interface IInserterGrade<T> : IEquatable<T>
    where T : IInserterGrade<T>
{
     T Create(ref InserterComponent inserter);
}