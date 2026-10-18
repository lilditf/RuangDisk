using System.Text;
using RuangDisk.Core.Models;
using RuangDisk.Infrastructure.Export;
using Xunit;

namespace RuangDisk.Infrastructure.Tests;

public class ExportServiceTests
{
    [Fact]
    public async Task ExportToCsvAsync_QuotesFieldsWithCommas_AndWritesValidCsv()
    {
        var rootNode = new FileSystemNode
        {
            Name = "Folder, with comma",
            FullPath = "C:\\Data\\Folder, with comma",
            IsDirectory = true,
            LogicalSize = 5000,
            FileCount = 1,
            DirectoryCount = 0
        };

        var child = new FileSystemNode
        {
            Name = "test.txt",
            FullPath = "C:\\Data\\Folder, with comma\\test.txt",
            IsDirectory = false,
            LogicalSize = 5000,
            FileCount = 1,
            Extension = ".txt"
        };
        rootNode.Children.Add(child);

        var service = new ExportService();
        using var ms = new MemoryStream();

        await service.ExportToCsvAsync(rootNode, ms);

        string csvContent = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("Name,FullPath,Type,LogicalBytes,FileCount,DirectoryCount,LastModifiedUtc,Extension", csvContent);
        Assert.Contains("\"Folder, with comma\"", csvContent);
        Assert.Contains("5000", csvContent);
    }
}
