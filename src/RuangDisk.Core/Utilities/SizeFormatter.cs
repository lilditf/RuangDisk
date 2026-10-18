using System.Globalization;

namespace RuangDisk.Core.Utilities;

/// <summary>
/// Mode format penamaan ukuran byte.
/// </summary>
public enum SizeUnitFormat
{
    Binary,  // KiB, MiB, GiB (1024)
    Decimal  // kB, MB, GB (1000)
}

/// <summary>
/// Helper untuk pemformatan ukuran byte yang jujur dan presisi.
/// </summary>
public static class SizeFormatter
{
    private static readonly string[] BinaryUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
    private static readonly string[] DecimalUnits = ["B", "kB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Memformat ukuran byte ke string yang mudah dibaca.
    /// </summary>
    public static string FormatBytes(long bytes, SizeUnitFormat format = SizeUnitFormat.Binary, int decimals = 2, IFormatProvider? formatProvider = null)
    {
        if (bytes <= 0) return "0 B";

        double size = bytes;
        int unitIndex = 0;
        double threshold = format == SizeUnitFormat.Binary ? 1024.0 : 1000.0;
        string[] units = format == SizeUnitFormat.Binary ? BinaryUnits : DecimalUnits;

        while (size >= threshold && unitIndex < units.Length - 1)
        {
            size /= threshold;
            unitIndex++;
        }

        var provider = formatProvider ?? CultureInfo.CurrentCulture;
        string formatString = unitIndex == 0 ? "0" : $"0.{new string('#', decimals)}";
        return $"{size.ToString(formatString, provider)} {units[unitIndex]}";
    }

    /// <summary>
    /// Menghitung dan memformat persentase terhadap nilai total.
    /// </summary>
    public static string FormatPercentage(long partBytes, long totalBytes, IFormatProvider? formatProvider = null)
    {
        if (totalBytes <= 0 || partBytes <= 0) return "0.0%";
        double pct = (double)partBytes / totalBytes * 100.0;
        if (pct > 100.0) pct = 100.0;
        var provider = formatProvider ?? CultureInfo.CurrentCulture;
        return $"{pct.ToString("F1", provider)}%";
    }

    /// <summary>
    /// Menghitung nilai persentase mentah (0.0 - 100.0).
    /// </summary>
    public static double CalculatePercentage(long partBytes, long totalBytes)
    {
        if (totalBytes <= 0 || partBytes <= 0) return 0.0;
        double pct = (double)partBytes / totalBytes * 100.0;
        return pct > 100.0 ? 100.0 : pct;
    }
}
