using RuangDisk.App.Models;
using RuangDisk.Core.Models;
using RuangDisk.Core.Utilities;

namespace RuangDisk.App.Services;

/// <summary>
/// Layanan penghitung tata letak Treemap (Squarified Layout) yang terisolasi dan efisien.
/// </summary>
public sealed class TreemapLayoutService
{
    private static readonly string[] ColorPalette =
    [
        "#3B82F6", // Blue
        "#10B981", // Emerald
        "#8B5CF6", // Purple
        "#F59E0B", // Amber
        "#EC4899", // Pink
        "#06B6D4", // Cyan
        "#EF4444", // Red
        "#6366F1", // Indigo
        "#14B8A6", // Teal
        "#84CC16"  // Lime
    ];

    private const string OtherColor = "#64748B"; // Slate Gray

    public IReadOnlyList<TreemapItem> ComputeLayout(
        FileSystemNode? parentNode,
        double availableWidth,
        double availableHeight,
        SizeUnitFormat format = SizeUnitFormat.Binary)
    {
        if (parentNode == null || parentNode.Children.Count == 0 || availableWidth < 20 || availableHeight < 20)
        {
            return Array.Empty<TreemapItem>();
        }

        // Ambil anak-anak yang memiliki ukuran > 0
        var validChildren = parentNode.Children
            .Where(c => c.LogicalSize > 0)
            .OrderByDescending(c => c.LogicalSize)
            .ToList();

        if (validChildren.Count == 0) return Array.Empty<TreemapItem>();

        long totalSize = validChildren.Sum(c => c.LogicalSize);
        if (totalSize <= 0) return Array.Empty<TreemapItem>();

        var items = new List<TreemapItem>();
        long otherSize = 0;
        int colorIdx = 0;

        // Batasi jumlah kotak: item dengan persentase < 1.0% atau di atas peringkat 40 digabung ke "Other"
        for (int i = 0; i < validChildren.Count; i++)
        {
            var child = validChildren[i];
            double pct = (double)child.LogicalSize / totalSize * 100.0;

            if (pct < 1.0 || i >= 40)
            {
                otherSize += child.LogicalSize;
            }
            else
            {
                items.Add(new TreemapItem
                {
                    Node = child,
                    DisplayName = child.Name,
                    FullPath = child.FullPath,
                    SizeBytes = child.LogicalSize,
                    FormattedSize = SizeFormatter.FormatBytes(child.LogicalSize, format),
                    Percentage = pct,
                    ColorHex = ColorPalette[colorIdx % ColorPalette.Length],
                    IsOther = false
                });
                colorIdx++;
            }
        }

        if (otherSize > 0)
        {
            double otherPct = (double)otherSize / totalSize * 100.0;
            items.Add(new TreemapItem
            {
                Node = null,
                DisplayName = "Other / Lainnya",
                FullPath = parentNode.FullPath,
                SizeBytes = otherSize,
                FormattedSize = SizeFormatter.FormatBytes(otherSize, format),
                Percentage = otherPct,
                ColorHex = OtherColor,
                IsOther = true
            });
        }

        // Hitung squarified layout
        Squarify(items, 0, 0, availableWidth, availableHeight, totalSize);

        return items;
    }

    private static void Squarify(
        List<TreemapItem> items,
        double x,
        double y,
        double width,
        double height,
        long totalSize)
    {
        if (items.Count == 0 || width <= 0 || height <= 0 || totalSize <= 0) return;

        double currentX = x;
        double currentY = y;
        double remainingWidth = width;
        double remainingHeight = height;
        long remainingTotal = totalSize;

        int index = 0;
        while (index < items.Count)
        {
            // Ambil row elemen berikutnya
            bool horizontal = remainingWidth >= remainingHeight;
            double side = horizontal ? remainingHeight : remainingWidth;

            var row = new List<TreemapItem>();
            long rowSize = 0;

            while (index < items.Count)
            {
                var candidate = items[index];
                if (row.Count == 0)
                {
                    row.Add(candidate);
                    rowSize += candidate.SizeBytes;
                    index++;
                    continue;
                }

                // Cek apakah menambahkan candidate memperbaiki atau memperburuk rasio aspek
                double currentWorst = WorstAspect(row, rowSize, side, remainingTotal, horizontal ? remainingWidth : remainingHeight);
                double candidateWorst = WorstAspect([.. row, candidate], rowSize + candidate.SizeBytes, side, remainingTotal, horizontal ? remainingWidth : remainingHeight);

                if (candidateWorst <= currentWorst)
                {
                    row.Add(candidate);
                    rowSize += candidate.SizeBytes;
                    index++;
                }
                else
                {
                    break;
                }
            }

            // Layout row yang telah terbentuk
            double rowExtent = remainingTotal > 0
                ? (horizontal ? remainingWidth : remainingHeight) * ((double)rowSize / remainingTotal)
                : 0;

            double offset = 0;
            foreach (var item in row)
            {
                double itemRatio = rowSize > 0 ? (double)item.SizeBytes / rowSize : 0;
                double itemLength = side * itemRatio;

                if (horizontal)
                {
                    item.X = currentX;
                    item.Y = currentY + offset;
                    item.Width = Math.Max(0, rowExtent);
                    item.Height = Math.Max(0, itemLength);
                }
                else
                {
                    item.X = currentX + offset;
                    item.Y = currentY;
                    item.Width = Math.Max(0, itemLength);
                    item.Height = Math.Max(0, rowExtent);
                }

                offset += itemLength;
            }

            // Update sisa area
            if (horizontal)
            {
                currentX += rowExtent;
                remainingWidth = Math.Max(0, remainingWidth - rowExtent);
            }
            else
            {
                currentY += rowExtent;
                remainingHeight = Math.Max(0, remainingHeight - rowExtent);
            }

            remainingTotal = Math.Max(0, remainingTotal - rowSize);
        }
    }

    private static double WorstAspect(
        List<TreemapItem> row,
        long rowTotal,
        double side,
        long remainingTotal,
        double remainingOtherSide)
    {
        if (rowTotal <= 0 || side <= 0 || remainingTotal <= 0 || remainingOtherSide <= 0) return double.MaxValue;

        double rowExtent = remainingOtherSide * ((double)rowTotal / remainingTotal);
        if (rowExtent <= 0) return double.MaxValue;

        double maxAspect = 0;
        foreach (var item in row)
        {
            double itemLength = side * ((double)item.SizeBytes / rowTotal);
            if (itemLength <= 0) continue;

            double aspect = Math.Max(rowExtent / itemLength, itemLength / rowExtent);
            if (aspect > maxAspect) maxAspect = aspect;
        }

        return maxAspect;
    }
}
