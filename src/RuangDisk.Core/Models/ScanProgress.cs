namespace RuangDisk.Core.Models;

/// <summary>
/// Status progres pemindaian berkala untuk dikirim ke UI melalui IProgress.
/// </summary>
public sealed record ScanProgress(
    ScanState State,
    string CurrentPath,
    long FilesScanned,
    long DirectoriesScanned,
    long LogicalBytesFound,
    int ErrorCount,
    TimeSpan Elapsed,
    double ItemsPerSecond = 0,
    double BytesPerSecond = 0);
