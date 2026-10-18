using RuangDisk.Core.Models;

namespace RuangDisk.Core.Contracts;

/// <summary>
/// Kontrak untuk mesin pemindaian filesystem asinkron, dapat dibatalkan, dan dapat dijeda.
/// </summary>
public interface IFileSystemScanner
{
    /// <summary>
    /// Memulai proses pemindaian pada path target.
    /// </summary>
    Task<ScanResult> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Menjeda proses pemindaian yang sedang berjalan.
    /// </summary>
    void Pause();

    /// <summary>
    /// Melanjutkan kembali proses pemindaian yang sedang dijeda.
    /// </summary>
    void Resume();

    /// <summary>
    /// Menandakan apakah pemindaian saat ini sedang dijeda.
    /// </summary>
    bool IsPaused { get; }
}
