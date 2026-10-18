using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RuangDisk.App.Models;
using RuangDisk.App.Services;
using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;
using RuangDisk.Core.Utilities;

namespace RuangDisk.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFileSystemScanner _scanner;
    private readonly IDriveService _driveService;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IFileActionService _fileActionService;
    private readonly IExportService _exportService;
    private readonly ISettingsService _settingsService;
    private readonly TreemapLayoutService _treemapService;

    private CancellationTokenSource? _scanCts;
    private double _lastTreemapWidth = 600;
    private double _lastTreemapHeight = 400;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScanning))]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    private ScanState _state = ScanState.Idle;

    public bool IsScanning => State == ScanState.Scanning || State == ScanState.Preparing || State == ScanState.Paused;
    public bool CanScan => !IsScanning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private ScanResult? _currentResult;

    public bool HasResults => CurrentResult?.RootNode != null;

    [ObservableProperty]
    private ScanProgress? _progress;

    [ObservableProperty]
    private string _scanSpeedFormatted = string.Empty;

    [ObservableProperty]
    private FileSystemNode? _selectedNode;

    [ObservableProperty]
    private FileSystemNode? _treemapCurrentRoot;

    [ObservableProperty]
    private bool _isSnapshotLoaded;

    [ObservableProperty]
    private bool _isScanStale;

    [ObservableProperty]
    private string _statusMessage = "Pilih drive atau folder untuk memulai pemindaian.";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _activeFilterPreset = "All"; // All, 1GB, 100MB, Old, Media

    [ObservableProperty]
    private bool _copyFeedbackVisible;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0: Tree, 1: Treemap, 2: Largest Files, 3: File Types

    public ObservableCollection<DriveTargetInfo> Drives { get; } = [];
    public ObservableCollection<string> RecentTargets { get; } = [];
    public ObservableCollection<TreeItemViewModel> RootTreeItems { get; } = [];
    public ObservableCollection<TreemapItem> TreemapItems { get; } = [];
    public ObservableCollection<FileSystemNode> TreemapBreadcrumbs { get; } = [];
    public ObservableCollection<LargestFileItemViewModel> LargestFiles { get; } = [];
    public ObservableCollection<FileTypeItemViewModel> FileTypes { get; } = [];
    public ObservableCollection<ScanError> Errors { get; } = [];

    public AppSettings Settings => _settingsService.Settings;

    public MainViewModel(
        IFileSystemScanner scanner,
        IDriveService driveService,
        ISnapshotStore snapshotStore,
        IFileActionService fileActionService,
        IExportService exportService,
        ISettingsService settingsService,
        TreemapLayoutService treemapService)
    {
        _scanner = scanner;
        _driveService = driveService;
        _snapshotStore = snapshotStore;
        _fileActionService = fileActionService;
        _exportService = exportService;
        _settingsService = settingsService;
        _treemapService = treemapService;

        RefreshDrives();
        LoadRecentTargets();
    }

    [RelayCommand]
    public void RefreshDrives()
    {
        Drives.Clear();
        var drives = _driveService.GetAvailableDrives();
        foreach (var d in drives)
        {
            Drives.Add(d);
        }

        if (string.IsNullOrEmpty(TargetPath) && Drives.Count > 0)
        {
            TargetPath = Drives[0].RootPath;
        }
    }

    private void LoadRecentTargets()
    {
        RecentTargets.Clear();
        foreach (var target in _settingsService.Settings.RecentTargets)
        {
            RecentTargets.Add(target);
        }
    }

    [RelayCommand]
    public void SelectDrive(DriveTargetInfo? drive)
    {
        if (drive != null && !IsScanning)
        {
            TargetPath = drive.RootPath;
        }
    }

    [RelayCommand]
    public void SelectRecentTarget(string? target)
    {
        if (!string.IsNullOrWhiteSpace(target) && !IsScanning)
        {
            TargetPath = target;
        }
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            StatusMessage = "Harap tentukan folder atau drive target terlebih dahulu.";
            return;
        }

        if (!_driveService.ValidatePathExists(TargetPath))
        {
            StatusMessage = "Target direktori tidak valid atau tidak dapat ditemukan.";
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        State = ScanState.Preparing;
        IsPaused = false;
        StatusMessage = "Menyiapkan pemindaian berkecepatan tinggi...";
        ScanSpeedFormatted = string.Empty;
        IsSnapshotLoaded = false;
        IsScanStale = false;
        Errors.Clear();

        _settingsService.AddRecentTarget(TargetPath);
        LoadRecentTargets();

        var options = new ScanOptions(
            IncludeHidden: _settingsService.Settings.IncludeHidden,
            IncludeSystem: _settingsService.Settings.IncludeSystem,
            FollowReparsePoints: _settingsService.Settings.FollowReparsePoints,
            ExclusionPatterns: _settingsService.Settings.ExclusionPatterns);

        var progressHandler = new Progress<ScanProgress>(p =>
        {
            Progress = p;
            State = p.State;
            IsPaused = p.State == ScanState.Paused;

            string speedText = p.ItemsPerSecond > 0
                ? $"{p.ItemsPerSecond:N0} item/dtk • {SizeFormatter.FormatBytes((long)p.BytesPerSecond, _settingsService.Settings.SizeFormat)}/dtk"
                : string.Empty;

            ScanSpeedFormatted = speedText;

            StatusMessage = p.State == ScanState.Paused
                ? "Pemindaian dijeda. Klik 'Lanjutkan' untuk meneruskan proses."
                : $"Memindai... {p.FilesScanned:N0} file, {p.DirectoriesScanned:N0} folder ({SizeFormatter.FormatBytes(p.LogicalBytesFound, _settingsService.Settings.SizeFormat)})";
        });

        try
        {
            var result = await _scanner.ScanAsync(TargetPath, options, progressHandler, _scanCts.Token);
            ApplyScanResult(result);
        }
        catch (OperationCanceledException)
        {
            State = ScanState.Canceled;
            IsPaused = false;
            StatusMessage = "Pemindaian dibatalkan.";
        }
        catch (Exception ex)
        {
            State = ScanState.Failed;
            IsPaused = false;
            StatusMessage = $"Pemindaian gagal: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    public void TogglePauseScan()
    {
        if (!IsScanning) return;

        if (_scanner.IsPaused)
        {
            _scanner.Resume();
            IsPaused = false;
            State = ScanState.Scanning;
            StatusMessage = "Melanjutkan pemindaian...";
        }
        else
        {
            _scanner.Pause();
            IsPaused = true;
            State = ScanState.Paused;
            StatusMessage = "Pemindaian dijeda.";
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        if (IsScanning)
        {
            StatusMessage = "Membatalkan pemindaian...";
            _scanner.Resume(); // Pastikan unpaused agar token cancellation langsung bereaksi
            _scanCts?.Cancel();
        }
    }

    private void ApplyScanResult(ScanResult result)
    {
        CurrentResult = result;
        State = result.FinalState;
        IsPaused = false;
        IsSnapshotLoaded = result.IsSnapshot;

        Errors.Clear();
        foreach (var err in result.Errors)
        {
            Errors.Add(err);
        }

        RootTreeItems.Clear();
        TreemapItems.Clear();
        TreemapBreadcrumbs.Clear();
        LargestFiles.Clear();
        FileTypes.Clear();

        if (result.RootNode != null)
        {
            SelectedNode = result.RootNode;
            TreemapCurrentRoot = result.RootNode;
            TreemapBreadcrumbs.Add(result.RootNode);

            // 1. Hierarchical Tree Item dengan On-Demand Lazy Loading
            var rootItem = new TreeItemViewModel(result.RootNode, result.RootNode.LogicalSize, _settingsService.Settings.SizeFormat)
            {
                IsExpanded = true
            };
            RootTreeItems.Add(rootItem);

            // 2. Compute Largest Files
            PopulateLargestFiles(result.RootNode, _settingsService.Settings.LargestFilesLimit, ActiveFilterPreset);

            // 3. Compute File Types Breakdown
            PopulateFileTypes(result.RootNode);

            // 4. Compute Treemap
            RefreshTreemap(_lastTreemapWidth, _lastTreemapHeight);
        }

        string durationStr = result.Duration.TotalSeconds < 1
            ? $"{result.Duration.TotalMilliseconds:F0} ms"
            : $"{result.Duration.TotalSeconds:F1} detik";

        StatusMessage = result.FinalState == ScanState.Canceled
            ? $"Pemindaian dibatalkan sebagian. Ditemukan {result.TotalFiles:N0} file ({SizeFormatter.FormatBytes(result.TotalLogicalBytes, _settingsService.Settings.SizeFormat)}) dalam {durationStr}."
            : $"Selesai: {result.TotalFiles:N0} file, {result.TotalDirectories:N0} folder ({SizeFormatter.FormatBytes(result.TotalLogicalBytes, _settingsService.Settings.SizeFormat)}) dalam {durationStr}.";
    }

    [RelayCommand]
    public void SetFilterPreset(string preset)
    {
        ActiveFilterPreset = preset;
        if (CurrentResult?.RootNode != null)
        {
            PopulateLargestFiles(CurrentResult.RootNode, _settingsService.Settings.LargestFilesLimit, preset);
        }
    }

    private void PopulateLargestFiles(FileSystemNode rootNode, int limit, string preset)
    {
        var allFiles = new List<FileSystemNode>();

        void CollectFiles(FileSystemNode node)
        {
            if (!node.IsDirectory)
            {
                // Terapkan filter preset jika ada
                if (PassesPreset(node, preset))
                {
                    allFiles.Add(node);
                }
                return;
            }

            foreach (var child in node.Children)
            {
                CollectFiles(child);
            }
        }

        CollectFiles(rootNode);

        var topFiles = allFiles
            .OrderByDescending(f => f.LogicalSize)
            .Take(limit)
            .ToList();

        LargestFiles.Clear();
        int rank = 1;
        foreach (var file in topFiles)
        {
            LargestFiles.Add(new LargestFileItemViewModel
            {
                Rank = rank++,
                Node = file,
                Name = file.Name,
                FullPath = file.FullPath,
                LogicalSize = file.LogicalSize,
                FormattedSize = SizeFormatter.FormatBytes(file.LogicalSize, _settingsService.Settings.SizeFormat),
                Extension = string.IsNullOrEmpty(file.Extension) ? "(tanpa ekstensi)" : file.Extension,
                LastModifiedFormatted = file.LastModified.HasValue
                    ? file.LastModified.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-"
            });
        }
    }

    private static bool PassesPreset(FileSystemNode node, string preset)
    {
        return preset switch
        {
            "1GB" => node.LogicalSize >= 1073741824L,
            "100MB" => node.LogicalSize >= 104857600L,
            "Old" => node.LastModified.HasValue && node.LastModified.Value < DateTimeOffset.UtcNow.AddYears(-1),
            "Media" => IsMediaOrArchive(node.Extension),
            _ => true
        };
    }

    private static bool IsMediaOrArchive(string ext)
    {
        string[] mediaExtensions = [".mp4", ".mkv", ".avi", ".mov", ".zip", ".rar", ".7z", ".iso", ".tar", ".gz", ".mp3", ".wav", ".flac"];
        return mediaExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private void PopulateFileTypes(FileSystemNode rootNode)
    {
        var typeMap = new Dictionary<string, (long count, long bytes)>(StringComparer.OrdinalIgnoreCase);

        void CollectExtensions(FileSystemNode node)
        {
            if (!node.IsDirectory)
            {
                string ext = string.IsNullOrEmpty(node.Extension) ? "(tanpa ekstensi)" : node.Extension;
                if (!typeMap.TryGetValue(ext, out var data))
                {
                    data = (0, 0);
                }
                typeMap[ext] = (data.count + 1, data.bytes + node.LogicalSize);
                return;
            }

            foreach (var child in node.Children)
            {
                CollectExtensions(child);
            }
        }

        CollectExtensions(rootNode);

        long totalBytes = rootNode.LogicalSize;
        var sortedTypes = typeMap
            .OrderByDescending(kv => kv.Value.bytes)
            .ToList();

        FileTypes.Clear();
        string[] colors = ["#3B82F6", "#10B981", "#8B5CF6", "#F59E0B", "#EC4899", "#06B6D4", "#EF4444", "#6366F1"];
        int colorIdx = 0;

        foreach (var kv in sortedTypes)
        {
            double pct = totalBytes > 0 ? (double)kv.Value.bytes / totalBytes * 100.0 : 0.0;
            FileTypes.Add(new FileTypeItemViewModel
            {
                Extension = kv.Key,
                FileCount = kv.Value.count,
                TotalBytes = kv.Value.bytes,
                FormattedSize = SizeFormatter.FormatBytes(kv.Value.bytes, _settingsService.Settings.SizeFormat),
                Percentage = pct,
                FormattedPercentage = SizeFormatter.FormatPercentage(kv.Value.bytes, totalBytes),
                ColorHex = colors[colorIdx % colors.Length]
            });
            colorIdx++;
        }
    }

    public void RefreshTreemap(double width, double height)
    {
        if (width > 0) _lastTreemapWidth = width;
        if (height > 0) _lastTreemapHeight = height;

        var targetNode = TreemapCurrentRoot ?? SelectedNode ?? CurrentResult?.RootNode;
        if (targetNode == null) return;

        var nodeForTreemap = targetNode.IsDirectory ? targetNode : CurrentResult?.RootNode;
        if (nodeForTreemap == null) return;

        var items = _treemapService.ComputeLayout(
            nodeForTreemap,
            _lastTreemapWidth,
            _lastTreemapHeight,
            _settingsService.Settings.SizeFormat);

        TreemapItems.Clear();
        foreach (var item in items)
        {
            TreemapItems.Add(item);
        }
    }

    [RelayCommand]
    public void DrillDownTreemap(TreemapItem? item)
    {
        if (item?.Node == null || !item.Node.IsDirectory) return;

        TreemapCurrentRoot = item.Node;
        TreemapBreadcrumbs.Add(item.Node);
        SelectedNode = item.Node;
        RefreshTreemap(_lastTreemapWidth, _lastTreemapHeight);
    }

    [RelayCommand]
    public void NavigateTreemapBreadcrumb(FileSystemNode? node)
    {
        if (node == null) return;

        int index = TreemapBreadcrumbs.IndexOf(node);
        if (index >= 0)
        {
            while (TreemapBreadcrumbs.Count > index + 1)
            {
                TreemapBreadcrumbs.RemoveAt(TreemapBreadcrumbs.Count - 1);
            }
            TreemapCurrentRoot = node;
            SelectedNode = node;
            RefreshTreemap(_lastTreemapWidth, _lastTreemapHeight);
        }
    }

    public void SelectNode(FileSystemNode? node)
    {
        if (node == null) return;
        SelectedNode = node;
        RefreshTreemap(_lastTreemapWidth, _lastTreemapHeight);
    }

    [RelayCommand]
    public void SelectNodeById(Guid id)
    {
        if (CurrentResult?.RootNode == null) return;

        FileSystemNode? FindRecursive(FileSystemNode current)
        {
            if (current.Id == id) return current;
            foreach (var child in current.Children)
            {
                var found = FindRecursive(child);
                if (found != null) return found;
            }
            return null;
        }

        var match = FindRecursive(CurrentResult.RootNode);
        if (match != null)
        {
            SelectNode(match);
        }
    }

    [RelayCommand]
    public void OpenFile(string? path)
    {
        string? target = path ?? SelectedNode?.FullPath;
        if (!string.IsNullOrEmpty(target))
        {
            _fileActionService.OpenFile(target);
        }
    }

    [RelayCommand]
    public void RevealInExplorer(string? path)
    {
        string? target = path ?? SelectedNode?.FullPath;
        if (!string.IsNullOrEmpty(target))
        {
            _fileActionService.RevealInExplorer(target);
        }
    }

    [RelayCommand]
    public async Task CopyPathAsync(string? path)
    {
        string? target = path ?? SelectedNode?.FullPath;
        if (!string.IsNullOrEmpty(target))
        {
            _fileActionService.CopyPath(target);
            StatusMessage = "Path berhasil disalin ke clipboard.";
            CopyFeedbackVisible = true;
            await Task.Delay(2000);
            CopyFeedbackVisible = false;
        }
    }

    [RelayCommand]
    public async Task MoveToRecycleBinAsync(string? path)
    {
        string? target = path ?? SelectedNode?.FullPath;
        if (string.IsNullOrEmpty(target)) return;

        if (!_fileActionService.CanDelete(target, CurrentResult?.TargetPath, out string? reason))
        {
            MessageBox.Show(
                reason ?? "Item terlindungi dan tidak dapat dihapus.",
                "Tindakan Ditolak",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_settingsService.Settings.ConfirmBeforeRecycle)
        {
            var confirmResult = MessageBox.Show(
                $"Apakah Anda yakin ingin memindahkan item berikut ke Recycle Bin?\n\nTarget:\n{target}",
                "Konfirmasi Hapus ke Recycle Bin",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;
        }

        var actionResult = await _fileActionService.MoveToRecycleBinAsync([target], CurrentResult?.TargetPath);
        if (actionResult.Success)
        {
            IsScanStale = true;
            StatusMessage = "Item berhasil dipindahkan ke Recycle Bin. Hasil scan saat ini sudah usang (stale).";
            MessageBox.Show(
                "Item berhasil dipindahkan ke Recycle Bin.\n\nDisarankan untuk melakukan pemindaian ulang (Rescan) agar pohon hierarki diperbarui.",
                "Berhasil Dihapus",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            string errors = string.Join("\n", actionResult.ErrorMessages);
            MessageBox.Show($"Gagal menghapus item:\n{errors}", "Kesalahan", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task SaveSnapshotAsync(string? filePath)
    {
        if (CurrentResult == null || CurrentResult.RootNode == null)
        {
            StatusMessage = "Tidak ada hasil scan untuk disimpan.";
            return;
        }

        try
        {
            string savePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"RuangDisk_Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.ruangdisk");

            await _snapshotStore.SaveToFileAsync(CurrentResult, savePath);
            StatusMessage = $"Snapshot berhasil disimpan ke {Path.GetFileName(savePath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal menyimpan snapshot: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadSnapshotAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            StatusMessage = "Memuat snapshot...";
            var result = await _snapshotStore.LoadFromFileAsync(filePath);
            TargetPath = result.TargetPath;
            ApplyScanResult(result);
            StatusMessage = $"Snapshot berhasil dimuat: {Path.GetFileName(filePath)} (Mode Baca Saja / Snapshot).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal memuat snapshot: {ex.Message}";
            MessageBox.Show($"File snapshot tidak valid atau rusak: {ex.Message}", "Error Snapshot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync(string? filePath)
    {
        if (CurrentResult == null || CurrentResult.RootNode == null)
        {
            StatusMessage = "Tidak ada hasil scan untuk diekspor.";
            return;
        }

        try
        {
            string csvPath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"RuangDisk_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            await _exportService.ExportToCsvFileAsync(CurrentResult.RootNode, csvPath);
            StatusMessage = $"Hasil berhasil diekspor ke {Path.GetFileName(csvPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal mengekspor CSV: {ex.Message}";
        }
    }
}
