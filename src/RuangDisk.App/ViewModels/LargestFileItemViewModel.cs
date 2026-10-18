using CommunityToolkit.Mvvm.ComponentModel;
using RuangDisk.Core.Models;

namespace RuangDisk.App.ViewModels;

public sealed class LargestFileItemViewModel : ObservableObject
{
    public required int Rank { get; init; }
    public required FileSystemNode Node { get; init; }
    public Guid NodeId => Node.Id;
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required long LogicalSize { get; init; }
    public required string FormattedSize { get; init; }
    public required string Extension { get; init; }
    public required string LastModifiedFormatted { get; init; }
}
