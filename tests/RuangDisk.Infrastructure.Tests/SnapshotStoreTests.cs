using RuangDisk.Core.Models;
using RuangDisk.Infrastructure.Persistence;
using Xunit;

namespace RuangDisk.Infrastructure.Tests;

public class SnapshotStoreTests
{
    [Fact]
    public async Task SaveAndLoad_Roundtrip_RestoresDataAccurately()
    {
        var rootNode = new FileSystemNode
        {
            Name = "Root",
            FullPath = "C:\\Root",
            IsDirectory = true,
            LogicalSize = 1000,
            FileCount = 2,
            DirectoryCount = 1
        };

        var childFile = new FileSystemNode
        {
            Name = "sample.txt",
            FullPath = "C:\\Root\\sample.txt",
            IsDirectory = false,
            LogicalSize = 1000,
            FileCount = 1,
            Extension = ".txt"
        };
        rootNode.Children.Add(childFile);

        var scanResult = new ScanResult(
            TargetPath: "C:\\Root",
            RootNode: rootNode,
            FinalState: ScanState.Completed,
            Duration: TimeSpan.FromSeconds(3),
            TotalFiles: 2,
            TotalDirectories: 1,
            TotalLogicalBytes: 1000,
            Errors: [new ScanError("C:\\Root\\locked.dat", "ACCESS_DENIED", "Akses ditolak")],
            Options: new ScanOptions(IncludeHidden: true, IncludeSystem: false),
            ScannedAt: DateTimeOffset.UtcNow,
            IsSnapshot: false);

        var store = new SnapshotStore();
        using var stream = new MemoryStream();

        // Save
        await store.SaveAsync(scanResult, stream);

        // Load
        stream.Position = 0;
        var loadedResult = await store.LoadAsync(stream);

        // Assert
        Assert.True(loadedResult.IsSnapshot);
        Assert.Equal(scanResult.TargetPath, loadedResult.TargetPath);
        Assert.Equal(scanResult.TotalLogicalBytes, loadedResult.TotalLogicalBytes);
        Assert.Equal(scanResult.TotalFiles, loadedResult.TotalFiles);
        Assert.Single(loadedResult.Errors);
        Assert.Equal("ACCESS_DENIED", loadedResult.Errors[0].ErrorCode);

        Assert.NotNull(loadedResult.RootNode);
        Assert.Equal("Root", loadedResult.RootNode.Name);
        Assert.Single(loadedResult.RootNode.Children);
        Assert.Equal("sample.txt", loadedResult.RootNode.Children[0].Name);
    }
}
