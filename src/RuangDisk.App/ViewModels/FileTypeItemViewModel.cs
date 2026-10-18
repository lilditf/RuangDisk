using CommunityToolkit.Mvvm.ComponentModel;

namespace RuangDisk.App.ViewModels;

public sealed class FileTypeItemViewModel : ObservableObject
{
    public required string Extension { get; init; }
    public required long FileCount { get; init; }
    public required long TotalBytes { get; init; }
    public required string FormattedSize { get; init; }
    public required double Percentage { get; init; }
    public required string FormattedPercentage { get; init; }
    public required string ColorHex { get; init; }
}
