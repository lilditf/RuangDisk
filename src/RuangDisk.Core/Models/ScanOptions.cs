namespace RuangDisk.Core.Models;

/// <summary>
/// Opsi konfigurasi untuk proses pemindaian filesystem.
/// </summary>
public sealed record ScanOptions(
    bool IncludeHidden = false,
    bool IncludeSystem = false,
    bool FollowReparsePoints = false,
    IReadOnlyList<string>? ExclusionPatterns = null)
{
    public IReadOnlyList<string> ExclusionPatterns { get; init; } = ExclusionPatterns ?? Array.Empty<string>();

    public static ScanOptions Default => new();
}
