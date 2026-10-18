using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;

namespace RuangDisk.Infrastructure.Scanning;

/// <summary>
/// Mesin pemindaian filesystem berkecepatan tinggi berbasis Win32 Native API (FindFirstFileExW + FIND_FIRST_EX_LARGE_FETCH)
/// dengan dukungan Pause/Resume, throttling cerdas, dan deteksi reparse point.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileSystemScanner : IFileSystemScanner
{
    private readonly ILogger<FileSystemScanner>? _logger;
    private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(150);

    private readonly ManualResetEventSlim _pauseEvent = new(initialState: true);
    private volatile bool _isPaused;

    public bool IsPaused => _isPaused;

    public FileSystemScanner(ILogger<FileSystemScanner>? logger = null)
    {
        _logger = logger;
    }

    public void Pause()
    {
        _pauseEvent.Reset();
        _isPaused = true;
        _logger?.LogInformation("Pemindaian dijeda.");
    }

    public void Resume()
    {
        _isPaused = false;
        _pauseEvent.Set();
        _logger?.LogInformation("Pemindaian dilanjutkan.");
    }

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        options ??= ScanOptions.Default;

        // Reset pause state ke unpaused
        Resume();

        var fullRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException($"Target direktori tidak ditemukan: {fullRootPath}");
        }

        var rootDirInfo = new DirectoryInfo(fullRootPath);
        var rootNode = new FileSystemNode
        {
            Name = string.IsNullOrEmpty(rootDirInfo.Name) ? fullRootPath : rootDirInfo.Name,
            FullPath = fullRootPath,
            IsDirectory = true,
            LastModified = rootDirInfo.LastWriteTimeUtc,
            IsReparsePoint = (rootDirInfo.Attributes & FileAttributes.ReparsePoint) != 0
        };

        var errors = new List<ScanError>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stopwatch = Stopwatch.StartNew();
        var lastProgressReport = Stopwatch.StartNew();

        long filesScanned = 0;
        long dirsScanned = 0;
        long bytesFound = 0;

        void ReportProgress(ScanState state, string currentPath, bool force = false)
        {
            if (progress == null) return;
            if (force || lastProgressReport.Elapsed >= ProgressThrottleInterval)
            {
                lastProgressReport.Restart();
                double elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                long totalItems = Interlocked.Read(ref filesScanned) + Interlocked.Read(ref dirsScanned);
                long currentBytes = Interlocked.Read(ref bytesFound);

                progress.Report(new ScanProgress(
                    state,
                    currentPath,
                    Interlocked.Read(ref filesScanned),
                    Interlocked.Read(ref dirsScanned),
                    currentBytes,
                    errors.Count,
                    stopwatch.Elapsed,
                    ItemsPerSecond: totalItems / elapsedSec,
                    BytesPerSecond: currentBytes / elapsedSec));
            }
        }

        ReportProgress(ScanState.Preparing, fullRootPath, force: true);
        ScanState finalState = ScanState.Completed;

        try
        {
            await Task.Run(() =>
            {
                ReportProgress(ScanState.Scanning, fullRootPath, force: true);

                ScanDirectoryNative(
                    fullRootPath,
                    rootNode,
                    options,
                    errors,
                    visitedPaths,
                    ref filesScanned,
                    ref dirsScanned,
                    ref bytesFound,
                    ReportProgress,
                    cancellationToken);

                rootNode.RecalculateAggregates();
                rootNode.IsComplete = true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            finalState = ScanState.Canceled;
            rootNode.RecalculateAggregates();
            _logger?.LogInformation("Pemindaian dibatalkan.");
        }
        catch (Exception ex)
        {
            finalState = ScanState.Failed;
            errors.Add(new ScanError(fullRootPath, "SCAN_CRITICAL_FAILURE", ex.Message));
            _logger?.LogError(ex, "Pemindaian terhenti karena kesalahan.");
        }
        finally
        {
            stopwatch.Stop();
            ReportProgress(finalState, fullRootPath, force: true);
        }

        return new ScanResult(
            TargetPath: fullRootPath,
            RootNode: rootNode,
            FinalState: finalState,
            Duration: stopwatch.Elapsed,
            TotalFiles: Interlocked.Read(ref filesScanned),
            TotalDirectories: Interlocked.Read(ref dirsScanned),
            TotalLogicalBytes: Interlocked.Read(ref bytesFound),
            Errors: errors.AsReadOnly(),
            Options: options,
            ScannedAt: DateTimeOffset.UtcNow,
            IsSnapshot: false);
    }

    private void ScanDirectoryNative(
        string currentPath,
        FileSystemNode currentNode,
        ScanOptions options,
        List<ScanError> errors,
        HashSet<string> visitedPaths,
        ref long filesScanned,
        ref long dirsScanned,
        ref long bytesFound,
        Action<ScanState, string, bool> reportProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Dukungan jeda (pause) tanpa menyia-nyiakan CPU
        if (_isPaused)
        {
            reportProgress(ScanState.Paused, currentPath, true);
            _pauseEvent.Wait(cancellationToken);
            reportProgress(ScanState.Scanning, currentPath, true);
        }

        if (!visitedPaths.Add(currentPath))
        {
            // Cycle terdeteksi
            return;
        }

        reportProgress(ScanState.Scanning, currentPath, false);

        string searchPattern = Path.Combine(currentPath, "*");
        IntPtr hFind = FindFirstFileExW(
            searchPattern,
            FINDEX_INFO_LEVELS.FindExInfoBasic,
            out WIN32_FIND_DATA findData,
            FINDEX_SEARCH_OPS.FindExSearchNameMatch,
            IntPtr.Zero,
            FIND_FIRST_EX_LARGE_FETCH);

        if (hFind == IntPtr.Zero || hFind == INVALID_HANDLE_VALUE)
        {
            int err = Marshal.GetLastWin32Error();
            if (err == ERROR_ACCESS_DENIED)
            {
                lock (errors)
                {
                    errors.Add(new ScanError(currentPath, "ACCESS_DENIED", "Akses ditolak ke direktori."));
                }
            }
            return;
        }

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_isPaused)
                {
                    reportProgress(ScanState.Paused, currentPath, true);
                    _pauseEvent.Wait(cancellationToken);
                    reportProgress(ScanState.Scanning, currentPath, true);
                }

                string fileName = findData.cFileName;
                if (fileName == "." || fileName == "..")
                    continue;

                uint attrs = findData.dwFileAttributes;
                bool isDir = (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0;
                bool isHidden = (attrs & FILE_ATTRIBUTE_HIDDEN) != 0;
                bool isSystem = (attrs & FILE_ATTRIBUTE_SYSTEM) != 0;
                bool isReparse = (attrs & FILE_ATTRIBUTE_REPARSE_POINT) != 0;

                if (isHidden && !options.IncludeHidden) continue;
                if (isSystem && !options.IncludeSystem) continue;
                if (IsExcluded(fileName, options.ExclusionPatterns)) continue;

                string childPath = Path.Combine(currentPath, fileName);
                DateTimeOffset? lastModified = ConvertFileTimeToDateTimeOffset(findData.ftLastWriteTime);

                if (isDir)
                {
                    Interlocked.Increment(ref dirsScanned);

                    var dirNode = new FileSystemNode
                    {
                        Name = fileName,
                        FullPath = childPath,
                        IsDirectory = true,
                        LastModified = lastModified,
                        IsReparsePoint = isReparse
                    };

                    currentNode.Children.Add(dirNode);

                    // Jangan ikuti reparse point / junction jika opsi tidak aktif
                    if (isReparse && !options.FollowReparsePoints)
                    {
                        dirNode.IsComplete = true;
                        continue;
                    }

                    ScanDirectoryNative(
                        childPath,
                        dirNode,
                        options,
                        errors,
                        visitedPaths,
                        ref filesScanned,
                        ref dirsScanned,
                        ref bytesFound,
                        reportProgress,
                        cancellationToken);

                    dirNode.RecalculateAggregates();
                    dirNode.IsComplete = true;
                }
                else
                {
                    long fileSize = ((long)findData.nFileSizeHigh << 32) | (long)findData.nFileSizeLow;
                    Interlocked.Increment(ref filesScanned);
                    Interlocked.Add(ref bytesFound, fileSize);

                    var fileNode = new FileSystemNode
                    {
                        Name = fileName,
                        FullPath = childPath,
                        IsDirectory = false,
                        LogicalSize = fileSize,
                        FileCount = 1,
                        DirectoryCount = 0,
                        Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                        LastModified = lastModified,
                        IsReparsePoint = isReparse,
                        IsComplete = true
                    };

                    currentNode.Children.Add(fileNode);
                }

            } while (FindNextFileW(hFind, out findData));
        }
        finally
        {
            FindClose(hFind);
        }
    }

    private static bool IsExcluded(string name, IReadOnlyList<string> exclusionPatterns)
    {
        if (exclusionPatterns == null || exclusionPatterns.Count == 0) return false;

        foreach (var pattern in exclusionPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            if (pattern.StartsWith("*.", StringComparison.OrdinalIgnoreCase))
            {
                string ext = pattern[1..];
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (name.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTimeOffset? ConvertFileTimeToDateTimeOffset(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        long fileTime = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        if (fileTime <= 0) return null;
        try
        {
            return DateTimeOffset.FromFileTime(fileTime);
        }
        catch
        {
            return null;
        }
    }

    #region Win32 P/Invoke Declarations

    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;

    private const uint FIND_FIRST_EX_LARGE_FETCH = 0x00000002;
    private const int ERROR_ACCESS_DENIED = 5;

    private enum FINDEX_INFO_LEVELS
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1
    }

    private enum FINDEX_SEARCH_OPS
    {
        FindExSearchNameMatch = 0
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindFirstFileExW(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        uint dwAdditionalFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr hFindFile);

    #endregion
}
