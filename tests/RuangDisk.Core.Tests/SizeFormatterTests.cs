using RuangDisk.Core.Utilities;
using Xunit;

namespace RuangDisk.Core.Tests;

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(-100, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KiB")]
    [InlineData(1048576, "1 MiB")]
    [InlineData(1073741824, "1 GiB")]
    public void FormatBytes_Binary_FormatsCorrectly(long bytes, string expected)
    {
        string result = SizeFormatter.FormatBytes(bytes, SizeUnitFormat.Binary);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1000, "1 kB")]
    [InlineData(1000000, "1 MB")]
    [InlineData(1000000000, "1 GB")]
    public void FormatBytes_Decimal_FormatsCorrectly(long bytes, string expected)
    {
        string result = SizeFormatter.FormatBytes(bytes, SizeUnitFormat.Decimal);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatPercentage_CalculatesProperly()
    {
        Assert.Equal("50.0%", SizeFormatter.FormatPercentage(50, 100, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0.0%", SizeFormatter.FormatPercentage(0, 100, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("100.0%", SizeFormatter.FormatPercentage(150, 100, System.Globalization.CultureInfo.InvariantCulture));
    }
}
