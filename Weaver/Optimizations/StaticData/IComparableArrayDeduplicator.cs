namespace Weaver.Optimizations.StaticData;

internal interface IComparableArrayDeduplicator
{
    int TotalBytes { get; }
    int BytesDeduplicated { get; }

    void Clear();
}
