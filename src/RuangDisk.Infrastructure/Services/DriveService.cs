using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;

namespace RuangDisk.Infrastructure.Services;

/// <summary>
/// Layanan untuk mendapatkan informasi drive penyimpanan lokal.
/// </summary>
public sealed class DriveService : IDriveService
{
    public IReadOnlyList<DriveTargetInfo> GetAvailableDrives()
    {
        var result = new List<DriveTargetInfo>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady) continue;

                long totalSize = 0;
                long freeSpace = 0;
                string volumeLabel = drive.Name;
                string format = "Unknown";

                try
                {
                    totalSize = drive.TotalSize;
                    freeSpace = drive.AvailableFreeSpace;
                    if (!string.IsNullOrWhiteSpace(drive.VolumeLabel))
                    {
                        volumeLabel = $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                    }
                    format = drive.DriveFormat;
                }
                catch
                {
                    // Drive mungkin sedang sibuk atau diakses secara bersamaan
                }

                long usedSpace = Math.Max(0, totalSize - freeSpace);

                result.Add(new DriveTargetInfo(
                    Name: volumeLabel,
                    RootPath: drive.RootDirectory.FullName,
                    DriveType: drive.DriveType.ToString(),
                    FileSystem: format,
                    TotalSizeBytes: totalSize,
                    FreeSpaceBytes: freeSpace,
                    UsedSpaceBytes: usedSpace));
            }
        }
        catch
        {
            // Fallback jika tidak dapat mengambil drive
        }

        return result.AsReadOnly();
    }

    public bool ValidatePathExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return Directory.Exists(path) || File.Exists(path);
    }
}
