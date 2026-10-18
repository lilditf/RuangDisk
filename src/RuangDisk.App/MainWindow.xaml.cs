using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RuangDisk.App.Models;
using RuangDisk.App.ViewModels;
using RuangDisk.Core.Models;

namespace RuangDisk.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Target untuk Dipindai",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.TargetPath = dialog.FolderName;
        }
    }

    private void SnapshotMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var saveItem = new MenuItem { Header = "Simpan Snapshot Saat Ini (.ruangdisk)..." };
        saveItem.IsEnabled = ViewModel.HasResults;
        saveItem.Click += async (_, _) =>
        {
            var saveDlg = new SaveFileDialog
            {
                Title = "Simpan Snapshot RuangDisk",
                Filter = "Snapshot RuangDisk (*.ruangdisk)|*.ruangdisk",
                DefaultExt = ".ruangdisk",
                FileName = $"RuangDisk_Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.ruangdisk"
            };

            if (saveDlg.ShowDialog(this) == true)
            {
                await ViewModel.SaveSnapshotAsync(saveDlg.FileName);
            }
        };

        var loadItem = new MenuItem { Header = "Muat Snapshot Tersimpan (.ruangdisk)..." };
        loadItem.Click += async (_, _) =>
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Buka File Snapshot RuangDisk",
                Filter = "Snapshot RuangDisk (*.ruangdisk)|*.ruangdisk",
                DefaultExt = ".ruangdisk"
            };

            if (openDlg.ShowDialog(this) == true)
            {
                await ViewModel.LoadSnapshotAsync(openDlg.FileName);
            }
        };

        menu.Items.Add(saveItem);
        menu.Items.Add(loadItem);
        menu.IsOpen = true;
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var saveDlg = new SaveFileDialog
        {
            Title = "Ekspor Hasil Scan ke CSV",
            Filter = "File CSV (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"RuangDisk_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveDlg.ShowDialog(this) == true)
        {
            await ViewModel.ExportCsvAsync(saveDlg.FileName);
        }
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeItemViewModel item && item.Node != null)
        {
            ViewModel.SelectNode(item.Node);
        }
    }

    private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 50 && e.NewSize.Height > 50)
        {
            ViewModel.RefreshTreemap(e.NewSize.Width, e.NewSize.Height);
        }
    }

    private void RefreshTreemap_Click(object sender, RoutedEventArgs e)
    {
        if (TreemapItemsControl.ActualWidth > 0 && TreemapItemsControl.ActualHeight > 0)
        {
            ViewModel.RefreshTreemap(TreemapItemsControl.ActualWidth, TreemapItemsControl.ActualHeight);
        }
    }

    private void TreemapBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TreemapItem item)
        {
            if (e.ClickCount == 2)
            {
                // Double-click: Drill-down / Zoom-in
                ViewModel.DrillDownTreemap(item);
            }
            else
            {
                // Single-click: Select node
                if (item.Node != null)
                {
                    ViewModel.SelectNode(item.Node);
                }
            }
            e.Handled = true;
        }
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FileSystemNode node)
        {
            ViewModel.NavigateTreemapBreadcrumb(node);
        }
    }

    private void FilterPill_All_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilterPreset("All");
    private void FilterPill_1GB_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilterPreset("1GB");
    private void FilterPill_100MB_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilterPreset("100MB");
    private void FilterPill_Old_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilterPreset("Old");
    private void FilterPill_Media_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilterPreset("Media");

    private void LargestFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is LargestFileItemViewModel item)
        {
            ViewModel.SelectNode(item.Node);
        }
    }

    private void DriveCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DriveTargetInfo drive)
        {
            ViewModel.SelectDrive(drive);
        }
    }

    private void RecentTarget_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is string target)
        {
            ViewModel.SelectRecentTarget(target);
        }
    }

    private void ContextMenu_OpenFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenFile(ViewModel.SelectedNode?.FullPath);
    }

    private void ContextMenu_Reveal_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RevealInExplorer(ViewModel.SelectedNode?.FullPath);
    }

    private async void ContextMenu_CopyPath_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CopyPathAsync(ViewModel.SelectedNode?.FullPath);
    }

    private async void ContextMenu_Recycle_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveToRecycleBinAsync(ViewModel.SelectedNode?.FullPath);
    }
}