using System.Diagnostics.CodeAnalysis;
using Weaver.Optimizations.Inserters;

namespace Weaver.Optimizations.StaticData;

internal sealed class UniverseInserterStaticDataBuilder<TInserterGrade>
    where TInserterGrade : struct, IInserterGrade<TInserterGrade>, IMemorySize
{
    private readonly DataDeduplicator<TInserterGrade> _inserterGrades = new DataDeduplicator<TInserterGrade>();

    public int TotalBytes => _inserterGrades.TotalBytes;
    public int BytesDeduplicated => _inserterGrades.BytesDeduplicated;

    public int AddInserterGrade(ref readonly TInserterGrade inserterGrade)
    {
        return _inserterGrades.GetDeduplicatedValueIndex(in inserterGrade);
    }

    public bool TryGetUpdatedData([NotNullWhen(true)]  out ReadonlyArray<TInserterGrade>? updatedData)
    {
        return _inserterGrades.TryGetUpdatedData(out updatedData);
    }

    public void Clear()
    {
        _inserterGrades.Clear();
    }
}
