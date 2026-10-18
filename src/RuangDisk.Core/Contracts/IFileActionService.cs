using RuangDisk.Core.Models;

namespace RuangDisk.Core.Contracts;

/// <summary>
/// Layanan aksi file yang aman, termasuk integrasi Explorer dan penghapusan ke Recycle Bin.
/// </summary>
public interface IFileActionService
{
    /// <summary>
    /// Membuka file dengan aplikasi default sistem operasi.
    /// </summary>
    bool OpenFile(string path);

    /// <summary>
    /// Membuka folder dan memilih file yang ditentukan dalam Windows Explorer.
    /// </summary>
    bool RevealInExplorer(string path);

    /// <summary>
    /// Menyalin path lengkap ke clipboard.
    /// </summary>
    bool CopyPath(string path);

    /// <summary>
    /// Memeriksa apakah path diizinkan untuk dihapus atau merupakan lokasi yang dilindungi.
    /// </summary>
    /// <param name="path">Path yang akan diperiksa.</param>
    /// <param name="activeScanRoot">Root target scan aktif (opsional).</param>
    /// <param name="reason">Alasan jika tidak diizinkan dihapus.</param>
    /// <returns>True jika aman dihapus, False jika dilindungi.</returns>
    bool CanDelete(string path, string? activeScanRoot, out string? reason);

    /// <summary>
    /// Memindahkan satu atau lebih path ke Recycle Bin secara aman dengan pelaporan parsial.
    /// </summary>
    Task<FileActionResult> MoveToRecycleBinAsync(
        IReadOnlyList<string> paths,
        string? activeScanRoot = null,
        CancellationToken cancellationToken = default);
}
