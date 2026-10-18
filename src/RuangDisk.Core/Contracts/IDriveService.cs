using RuangDisk.Core.Models;

namespace RuangDisk.Core.Contracts;

/// <summary>
/// Layanan untuk mendapatkan daftar drive lokal sistem tanpa mengikat UI secara langsung ke IO native.
/// </summary>
public interface IDriveService
{
    /// <summary>
    /// Mengambil seluruh drive lokal yang siap digunakan.
    /// </summary>
    IReadOnlyList<DriveTargetInfo> GetAvailableDrives();

    /// <summary>
    /// Memvalidasi apakah suatu path direktori atau drive masih valid dan dapat diakses.
    /// </summary>
    bool ValidatePathExists(string path);
}
