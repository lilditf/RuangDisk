using System.Globalization;
using System.Text;
using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;

namespace RuangDisk.Infrastructure.Export;

/// <summary>
/// Layanan ekspor data hierarki hasil scan ke format CSV dengan escaping standar RFC 4180 dan nilai numerik invariant culture.
/// </summary>
public sealed class ExportService : IExportService
{
    public async Task ExportToCsvAsync(FileSystemNode rootNode, Stream outputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        ArgumentNullException.ThrowIfNull(outputStream);

        // UTF8 dengan BOM agar kompatibel dengan Microsoft Excel
        using var writer = new StreamWriter(outputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 4096, leaveOpen: true);

        // Header
        await writer.WriteLineAsync("Name,FullPath,Type,LogicalBytes,FileCount,DirectoryCount,LastModifiedUtc,Extension").ConfigureAwait(false);

        await WriteNodeRecursiveAsync(rootNode, writer, cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportToCsvFileAsync(FileSystemNode rootNode, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await ExportToCsvAsync(rootNode, fileStream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteNodeRecursiveAsync(FileSystemNode node, StreamWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string line = string.Join(",",
            EscapeCsvField(node.Name),
            EscapeCsvField(node.FullPath),
            node.IsDirectory ? "Folder" : "File",
            node.LogicalSize.ToString(CultureInfo.InvariantCulture),
            node.FileCount.ToString(CultureInfo.InvariantCulture),
            node.DirectoryCount.ToString(CultureInfo.InvariantCulture),
            EscapeCsvField(node.LastModified?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
            EscapeCsvField(node.Extension)
        );

        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);

        foreach (var child in node.Children)
        {
            await WriteNodeRecursiveAsync(child, writer, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        bool mustQuote = field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n');
        if (mustQuote)
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
