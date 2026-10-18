using RuangDisk.Core.Models;

namespace RuangDisk.Core.Contracts;

/// <summary>
/// Layanan untuk mengekspor data hierarki hasil scan ke format CSV.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Mengekspor seluruh pohon hierarki hasil scan ke stream CSV.
    /// </summary>
    Task ExportToCsvAsync(FileSystemNode rootNode, Stream outputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mengekspor seluruh pohon hierarki hasil scan ke file CSV.
    /// </summary>
    Task ExportToCsvFileAsync(FileSystemNode rootNode, string filePath, CancellationToken cancellationToken = default);
}
