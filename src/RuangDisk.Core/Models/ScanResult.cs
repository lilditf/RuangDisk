namespace RuangDisk.Core.Models;

/// <summary>
/// Hasil akhir dari operasi pemindaian filesystem atau pemuatan snapshot.
/// </summary>
public sealed record ScanResult(
    string TargetPath,
    FileSystemNode? RootNode,
    ScanState FinalState,
    TimeSpan Duration,
    long TotalFiles,
    long TotalDirectories,
    long TotalLogicalBytes,
    IReadOnlyList<ScanError> Errors,
    ScanOptions Options,
    DateTimeOffset ScannedAt,
    bool IsSnapshot = false);
