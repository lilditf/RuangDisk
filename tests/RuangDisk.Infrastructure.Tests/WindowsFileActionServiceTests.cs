using System.Runtime.Versioning;
using RuangDisk.Infrastructure.Actions;
using Xunit;

namespace RuangDisk.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public class WindowsFileActionServiceTests
{
    [Fact]
    public void CanDelete_DriveRoot_ReturnsFalseWithReason()
    {
        var service = new WindowsFileActionService();
        bool canDelete = service.CanDelete("C:\\", null, out string? reason);

        Assert.False(canDelete);
        Assert.NotNull(reason);
        Assert.Contains("Drive root", reason);
    }

    [Fact]
    public void CanDelete_ActiveScanRoot_ReturnsFalseWithReason()
    {
        var service = new WindowsFileActionService();
        string scanRoot = "D:\\MyData\\Project";

        bool canDelete = service.CanDelete("D:\\MyData\\Project", scanRoot, out string? reason);

        Assert.False(canDelete);
        Assert.NotNull(reason);
        Assert.Contains("root pemindaian", reason);
    }

    [Fact]
    public void CanDelete_WindowsSystemFolder_ReturnsFalseWithReason()
    {
        var service = new WindowsFileActionService();
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        bool canDelete = service.CanDelete(winDir, null, out string? reason);

        Assert.False(canDelete);
        Assert.NotNull(reason);
        Assert.Contains("terlindungi", reason);
    }

    [Fact]
    public void CanDelete_NormalSubdirectory_ReturnsTrue()
    {
        var service = new WindowsFileActionService();
        string normalPath = "C:\\NormalFolder\\SubFolder\\file.txt";

        bool canDelete = service.CanDelete(normalPath, "C:\\OtherRoot", out string? reason);

        Assert.True(canDelete);
        Assert.Null(reason);
    }
}
