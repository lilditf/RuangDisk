namespace RuangDisk.Core.Models;

/// <summary>
/// Hasil eksekusi aksi file, termasuk penghapusan ke Recycle Bin.
/// </summary>
public sealed record FileActionResult(
    bool Success,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<string> FailedPaths,
    IReadOnlyList<string> ErrorMessages);
