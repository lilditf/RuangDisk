using System.Runtime.Versioning;
using RuangDisk.Core.Models;
using RuangDisk.Infrastructure.Scanning;
using Xunit;

namespace RuangDisk.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public class FileSystemScannerTests : IDisposable
{
    private readonly string _testDirectory;

    public FileSystemScannerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "RuangDisk_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Abaikan kesalahan pembersihan direktori temp
        }
    }

    [Fact]
    public async Task ScanAsync_EmptyDirectory_ReturnsZeroFilesAndBytes()
    {
        var scanner = new FileSystemScanner();
        var result = await scanner.ScanAsync(_testDirectory, ScanOptions.Default, null, CancellationToken.None);

        Assert.Equal(ScanState.Completed, result.FinalState);
        Assert.NotNull(result.RootNode);
        Assert.Equal(0, result.TotalFiles);
        Assert.Equal(0, result.TotalLogicalBytes);
        Assert.Equal(0, result.TotalDirectories);
    }

    [Fact]
    public async Task ScanAsync_NestedStructure_AggregatesCorrectly()
    {
        // Setup:
        // root/
        //   sub1/
        //     file1.txt (100 bytes)
        //     file2.bin (250 bytes)
        //   file3.log (50 bytes)
        var sub1 = Path.Combine(_testDirectory, "sub1");
        Directory.CreateDirectory(sub1);

        File.WriteAllBytes(Path.Combine(sub1, "file1.txt"), new byte[100]);
        File.WriteAllBytes(Path.Combine(sub1, "file2.bin"), new byte[250]);
        File.WriteAllBytes(Path.Combine(_testDirectory, "file3.log"), new byte[50]);

        var scanner = new FileSystemScanner();
        var result = await scanner.ScanAsync(_testDirectory, ScanOptions.Default, null, CancellationToken.None);

        Assert.Equal(ScanState.Completed, result.FinalState);
        Assert.Equal(3, result.TotalFiles);
        Assert.Equal(1, result.TotalDirectories);
        Assert.Equal(400, result.TotalLogicalBytes);

        var root = result.RootNode;
        Assert.NotNull(root);
        Assert.Equal(400, root.LogicalSize);
        Assert.Equal(3, root.FileCount);
        Assert.Equal(1, root.DirectoryCount);
    }

    [Fact]
    public async Task ScanAsync_WithExclusions_IgnoresMatchingPatterns()
    {
        var sub = Path.Combine(_testDirectory, "node_modules");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "package.json"), new byte[100]);

        File.WriteAllBytes(Path.Combine(_testDirectory, "test.tmp"), new byte[500]);
        File.WriteAllBytes(Path.Combine(_testDirectory, "valid.txt"), new byte[200]);

        var options = new ScanOptions(
            IncludeHidden: true,
            IncludeSystem: true,
            FollowReparsePoints: false,
            ExclusionPatterns: ["node_modules", "*.tmp"]);

        var scanner = new FileSystemScanner();
        var result = await scanner.ScanAsync(_testDirectory, options, null, CancellationToken.None);

        Assert.Equal(ScanState.Completed, result.FinalState);
        Assert.Equal(1, result.TotalFiles);
        Assert.Equal(200, result.TotalLogicalBytes);
    }

    [Fact]
    public async Task ScanAsync_WhenCanceled_ReturnsCanceledStateGracefully()
    {
        // Buat folder besar agar proses scan membutuhkan waktu
        for (int i = 0; i < 50; i++)
        {
            var dir = Path.Combine(_testDirectory, $"dir_{i}");
            Directory.CreateDirectory(dir);
            for (int j = 0; j < 20; j++)
            {
                File.WriteAllBytes(Path.Combine(dir, $"file_{j}.dat"), new byte[50]);
            }
        }

        using var cts = new CancellationTokenSource();
        var scanner = new FileSystemScanner();

        var progress = new Progress<ScanProgress>(_ =>
        {
            cts.Cancel();
        });

        var result = await scanner.ScanAsync(_testDirectory, ScanOptions.Default, progress, cts.Token);

        Assert.Equal(ScanState.Canceled, result.FinalState);
    }

    [Fact]
    public async Task ScanAsync_CanPauseAndResume_Successfully()
    {
        for (int i = 0; i < 30; i++)
        {
            var dir = Path.Combine(_testDirectory, $"pause_dir_{i}");
            Directory.CreateDirectory(dir);
            for (int j = 0; j < 10; j++)
            {
                File.WriteAllBytes(Path.Combine(dir, $"pause_file_{j}.dat"), new byte[20]);
            }
        }

        var scanner = new FileSystemScanner();
        bool pausedTriggered = false;

        var progress = new Progress<ScanProgress>(_ =>
        {
            if (!pausedTriggered)
            {
                pausedTriggered = true;
                scanner.Pause();
                Assert.True(scanner.IsPaused);
                Task.Delay(50).ContinueWith(_ => scanner.Resume());
            }
        });

        var result = await scanner.ScanAsync(_testDirectory, ScanOptions.Default, progress, CancellationToken.None);

        Assert.Equal(ScanState.Completed, result.FinalState);
        Assert.False(scanner.IsPaused);
        Assert.True(result.TotalFiles >= 300);
    }
}
