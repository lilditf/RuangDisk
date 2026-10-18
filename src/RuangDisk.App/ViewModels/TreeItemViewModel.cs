using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RuangDisk.Core.Models;
using RuangDisk.Core.Utilities;

namespace RuangDisk.App.ViewModels;

/// <summary>
/// ViewModel untuk item pada pohon hierarki dengan dukungan lazy loading on-demand untuk menghemat memori.
/// </summary>
public sealed partial class TreeItemViewModel : ObservableObject
{
    private static readonly TreeItemViewModel DummyPlaceholder = new();

    private readonly SizeUnitFormat _format;
    private readonly long _rootTotalBytes;
    private bool _childrenLoaded;

    public FileSystemNode? Node { get; }
    public Guid Id => Node?.Id ?? Guid.Empty;
    public string Name => Node?.Name ?? string.Empty;
    public string FullPath => Node?.FullPath ?? string.Empty;
    public bool IsDirectory => Node?.IsDirectory ?? false;
    public long LogicalSize => Node?.LogicalSize ?? 0;
    public long FileCount => Node?.FileCount ?? 0;
    public long DirectoryCount => Node?.DirectoryCount ?? 0;
    public string Extension => Node?.Extension ?? string.Empty;

    public string FormattedSize => Node != null ? SizeFormatter.FormatBytes(Node.LogicalSize, _format) : string.Empty;
    public string FormattedPercentage => Node != null ? SizeFormatter.FormatPercentage(Node.LogicalSize, _rootTotalBytes) : string.Empty;
    public double Percentage => Node != null ? SizeFormatter.CalculatePercentage(Node.LogicalSize, _rootTotalBytes) : 0;

    public string LastModifiedFormatted => Node?.LastModified.HasValue == true
        ? Node.LastModified.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        : "-";

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<TreeItemViewModel> Children { get; } = [];

    // Konstruktor khusus untuk dummy placeholder
    private TreeItemViewModel()
    {
        _childrenLoaded = true;
    }

    public TreeItemViewModel(FileSystemNode node, long rootTotalBytes, SizeUnitFormat format)
    {
        Node = node;
        _rootTotalBytes = rootTotalBytes > 0 ? rootTotalBytes : node.LogicalSize;
        _format = format;

        // Jika direktori memiliki anak, pasang placeholder agar tanda panah expand (▶) muncul
        if (node.IsDirectory && node.Children.Count > 0)
        {
            Children.Add(DummyPlaceholder);
        }
        else
        {
            _childrenLoaded = true;
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded && Node != null)
        {
            LoadChildren();
        }
    }

    public void LoadChildren()
    {
        if (_childrenLoaded || Node == null) return;

        Children.Clear();
        foreach (var child in Node.Children.OrderByDescending(c => c.LogicalSize))
        {
            Children.Add(new TreeItemViewModel(child, _rootTotalBytes, _format));
        }
        _childrenLoaded = true;
    }
}
