using System.Diagnostics.CodeAnalysis;

namespace Weaver.Optimizations.Belts;

internal readonly struct OptimizedIndexedCargoPath
{
    private readonly OptimizedCargoPath[]? _optimizedCargoPaths;
    private readonly BeltIndex _index;

    [MemberNotNullWhen(true, nameof(_optimizedCargoPaths))] // Doesn't do anything but helps make it clear to me what i intended
    public bool HasBelt => _optimizedCargoPaths != null;

    public ref OptimizedCargoPath Belt => ref _index.GetBelt(_optimizedCargoPaths!);

    public OptimizedIndexedCargoPath(OptimizedCargoPath[]? optimizedCargoPaths, BeltIndex index)
    {
        _optimizedCargoPaths = optimizedCargoPaths;
        _index = index;
    }

    public static readonly OptimizedIndexedCargoPath NoBelt = new OptimizedIndexedCargoPath(null, BeltIndex.NoBelt);
}