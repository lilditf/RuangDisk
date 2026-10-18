using RuangDisk.Core.Models;

namespace RuangDisk.Core.Contracts;

/// <summary>
/// Kontrak penyimpanan dan pemuatan snapshot .ruangdisk (JSON terkompresi GZip dengan schemaVersion).
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Versi skema serialisasi snapshot saat ini.
    /// </summary>
    int CurrentSchemaVersion { get; }

    /// <summary>
    /// Menyimpan hasil pemindaian ke dalam stream terkompresi.
    /// </summary>
    Task SaveAsync(ScanResult result, Stream destinationStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Menyimpan hasil pemindaian ke file snapshot (.ruangdisk).
    /// </summary>
    Task SaveToFileAsync(ScanResult result, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Memuat dan memvalidasi snapshot dari stream terkompresi.
    /// </summary>
    Task<ScanResult> LoadAsync(Stream sourceStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Memuat dan memvalidasi snapshot dari file snapshot (.ruangdisk).
    /// </summary>
    Task<ScanResult> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);
}
