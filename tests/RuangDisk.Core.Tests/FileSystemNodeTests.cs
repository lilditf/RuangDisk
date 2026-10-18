using RuangDisk.Core.Models;
using Xunit;

namespace RuangDisk.Core.Tests;

public class FileSystemNodeTests
{
    [Fact]
    public void RecalculateAggregates_ShouldAggregateSizesAndCounts_Correctly()
    {
        // Arrange
        var root = new FileSystemNode
        {
            Name = "Root",
            FullPath = "C:\\TestRoot",
            IsDirectory = true
        };

        var subDir = new FileSystemNode
        {
            Name = "SubDir",
            FullPath = "C:\\TestRoot\\SubDir",
            IsDirectory = true
        };

        var file1 = new FileSystemNode
        {
            Name = "file1.txt",
            FullPath = "C:\\TestRoot\\file1.txt",
            IsDirectory = false,
            LogicalSize = 1000
        };

        var file2 = new FileSystemNode
        {
            Name = "file2.bin",
            FullPath = "C:\\TestRoot\\SubDir\\file2.bin",
            IsDirectory = false,
            LogicalSize = 2500
        };

        subDir.Children.Add(file2);
        subDir.RecalculateAggregates();

        root.Children.Add(file1);
        root.Children.Add(subDir);
        root.RecalculateAggregates();

        // Assert
        Assert.Equal(2500, subDir.LogicalSize);
        Assert.Equal(1, subDir.FileCount);
        Assert.Equal(0, subDir.DirectoryCount);

        Assert.Equal(3500, root.LogicalSize);
        Assert.Equal(2, root.FileCount);
        Assert.Equal(1, root.DirectoryCount);
    }

    [Fact]
    public void RecalculateAggregates_EmptyDirectory_ShouldHaveZeroSizeAndCounts()
    {
        var dir = new FileSystemNode
        {
            Name = "Empty",
            FullPath = "C:\\Empty",
            IsDirectory = true
        };

        dir.RecalculateAggregates();

        Assert.Equal(0, dir.LogicalSize);
        Assert.Equal(0, dir.FileCount);
        Assert.Equal(0, dir.DirectoryCount);
    }
}
