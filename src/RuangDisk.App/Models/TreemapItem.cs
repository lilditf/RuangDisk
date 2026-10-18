using RuangDisk.Core.Models;

namespace RuangDisk.App.Models;

/// <summary>
/// Model elemen kotak treemap untuk dirender pada UI WPF.
/// </summary>
public sealed class TreemapItem
{
    public FileSystemNode? Node { get; init; }
    public Guid NodeId => Node?.Id ?? Guid.Empty;
    public required string DisplayName { get; init; }
    public required string FullPath { get; init; }
    public required long SizeBytes { get; init; }
    public required string FormattedSize { get; init; }
    public required double Percentage { get; init; }
    public required string ColorHex { get; init; }
    public bool IsOther { get; init; }

    // Posisi dan dimensi layout
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
