using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;

namespace RuangDisk.Infrastructure.Persistence;

/// <summary>
/// Implementasi penyimpanan dan pembacaan snapshot .ruangdisk dengan kompresi GZip dan schemaVersion.
/// </summary>
public sealed class SnapshotStore : ISnapshotStore
{
    public int CurrentSchemaVersion => 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(ScanResult result, Stream destinationStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(destinationStream);

        var dto = SnapshotDto.FromScanResult(result, CurrentSchemaVersion);

        await using var gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal, leaveOpen: true);
        await JsonSerializer.SerializeAsync(gzipStream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
        await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveToFileAsync(ScanResult result, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await SaveAsync(result, fileStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScanResult> LoadAsync(Stream sourceStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        await using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress, leaveOpen: true);
        var dto = await JsonSerializer.DeserializeAsync<SnapshotDto>(gzipStream, JsonOptions, cancellationToken).ConfigureAwait(false);

        if (dto == null)
        {
            throw new InvalidDataException("Data snapshot tidak valid atau kosong.");
        }

        if (dto.SchemaVersion > CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Versi skema snapshot ({dto.SchemaVersion}) lebih baru dari versi yang didukung ({CurrentSchemaVersion}).");
        }

        return dto.ToScanResult();
    }

    public async Task<ScanResult> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File snapshot tidak ditemukan.", filePath);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await LoadAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    private sealed class SnapshotDto
    {
        public int SchemaVersion { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public DateTimeOffset ScannedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public long TotalFiles { get; set; }
        public long TotalDirectories { get; set; }
        public long TotalLogicalBytes { get; set; }
        public ScanOptionsDto? Options { get; set; }
        public List<ScanErrorDto> Errors { get; set; } = [];
        public FileSystemNodeDto? RootNode { get; set; }

        public static SnapshotDto FromScanResult(ScanResult result, int version)
        {
            return new SnapshotDto
            {
                SchemaVersion = version,
                TargetPath = result.TargetPath,
                ScannedAt = result.ScannedAt,
                Duration = result.Duration,
                TotalFiles = result.TotalFiles,
                TotalDirectories = result.TotalDirectories,
                TotalLogicalBytes = result.TotalLogicalBytes,
                Options = new ScanOptionsDto
                {
                    IncludeHidden = result.Options.IncludeHidden,
                    IncludeSystem = result.Options.IncludeSystem,
                    FollowReparsePoints = result.Options.FollowReparsePoints,
                    ExclusionPatterns = result.Options.ExclusionPatterns.ToList()
                },
                Errors = result.Errors.Select(e => new ScanErrorDto
                {
                    Path = e.Path,
                    ErrorCode = e.ErrorCode,
                    SafeMessage = e.SafeMessage
                }).ToList(),
                RootNode = result.RootNode != null ? FileSystemNodeDto.FromNode(result.RootNode) : null
            };
        }

        public ScanResult ToScanResult()
        {
            var options = new ScanOptions(
                IncludeHidden: Options?.IncludeHidden ?? false,
                IncludeSystem: Options?.IncludeSystem ?? false,
                FollowReparsePoints: Options?.FollowReparsePoints ?? false,
                ExclusionPatterns: Options?.ExclusionPatterns ?? []);

            var errors = Errors.Select(e => new ScanError(e.Path, e.ErrorCode, e.SafeMessage)).ToList();
            var root = RootNode?.ToNode();

            return new ScanResult(
                TargetPath: TargetPath,
                RootNode: root,
                FinalState: ScanState.Completed,
                Duration: Duration,
                TotalFiles: TotalFiles,
                TotalDirectories: TotalDirectories,
                TotalLogicalBytes: TotalLogicalBytes,
                Errors: errors.AsReadOnly(),
                Options: options,
                ScannedAt: ScannedAt,
                IsSnapshot: true);
        }
    }

    private sealed class ScanOptionsDto
    {
        public bool IncludeHidden { get; set; }
        public bool IncludeSystem { get; set; }
        public bool FollowReparsePoints { get; set; }
        public List<string> ExclusionPatterns { get; set; } = [];
    }

    private sealed class ScanErrorDto
    {
        public string Path { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string SafeMessage { get; set; } = string.Empty;
    }

    private sealed class FileSystemNodeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long LogicalSize { get; set; }
        public long AllocatedSize { get; set; }
        public long FileCount { get; set; }
        public long DirectoryCount { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public bool IsReparsePoint { get; set; }
        public string Extension { get; set; } = string.Empty;
        public List<FileSystemNodeDto> Children { get; set; } = [];

        public static FileSystemNodeDto FromNode(FileSystemNode node)
        {
            var dto = new FileSystemNodeDto
            {
                Id = node.Id,
                Name = node.Name,
                FullPath = node.FullPath,
                IsDirectory = node.IsDirectory,
                LogicalSize = node.LogicalSize,
                AllocatedSize = node.AllocatedSize,
                FileCount = node.FileCount,
                DirectoryCount = node.DirectoryCount,
                LastModified = node.LastModified,
                IsReparsePoint = node.IsReparsePoint,
                Extension = node.Extension
            };

            foreach (var child in node.Children)
            {
                dto.Children.Add(FromNode(child));
            }

            return dto;
        }

        public FileSystemNode ToNode()
        {
            var node = new FileSystemNode
            {
                Id = Id != Guid.Empty ? Id : Guid.NewGuid(),
                Name = Name,
                FullPath = FullPath,
                IsDirectory = IsDirectory,
                LogicalSize = LogicalSize,
                AllocatedSize = AllocatedSize > 0 ? AllocatedSize : LogicalSize,
                FileCount = FileCount,
                DirectoryCount = DirectoryCount,
                LastModified = LastModified,
                IsReparsePoint = IsReparsePoint,
                Extension = Extension,
                IsComplete = true
            };

            foreach (var childDto in Children)
            {
                node.Children.Add(childDto.ToNode());
            }

            return node;
        }
    }
}
