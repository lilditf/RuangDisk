namespace RuangDisk.Core.Models;

/// <summary>
/// Informasi detail partisi atau drive penyimpanan lokal.
/// </summary>
public sealed record DriveTargetInfo(
    string Name,
    string RootPath,
    string DriveType,
    string FileSystem,
    long TotalSizeBytes,
    long FreeSpaceBytes,
    long UsedSpaceBytes)
{
    public double UsedPercentage => TotalSizeBytes > 0
        ? (double)UsedSpaceBytes / TotalSizeBytes * 100.0
        : 0.0;
}
