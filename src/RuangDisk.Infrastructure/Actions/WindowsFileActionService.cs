using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using RuangDisk.Core.Contracts;
using RuangDisk.Core.Models;

namespace RuangDisk.Infrastructure.Actions;

/// <summary>
/// Layanan aksi file Windows yang aman dengan proteksi lokasi kritis dan penghapusan khusus Recycle Bin.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsFileActionService : IFileActionService
{
    private readonly ILogger<WindowsFileActionService>? _logger;

    public WindowsFileActionService(ILogger<WindowsFileActionService>? logger = null)
    {
        _logger = logger;
    }

    public bool OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Gagal membuka file.");
            return false;
        }
    }

    public bool RevealInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return false;

        try
        {
            string argument = Directory.Exists(path)
                ? $"\"{path}\""
                : $"/select,\"{path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = argument,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Gagal membuka Explorer untuk path.");
            return false;
        }
    }

    public bool CopyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            // Menjalankan di background STA thread bila dipanggil di luar thread UI
            var thread = new Thread(() =>
            {
                try
                {
                    // Gunakan P/Invoke atau clip.exe jika belum ada WPF Context
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c chcp 65001 >nul && echo | set /p=\"{path}\" | clip",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(1000);
                }
                catch
                {
                    // Fallback diabaikan
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(1500);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Gagal menyalin path ke clipboard.");
            return false;
        }
    }

    public bool CanDelete(string path, string? activeScanRoot, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Path kosong.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            reason = "Format path tidak valid.";
            return false;
        }

        // 1. Cek Root Drive (contoh: C:, D:)
        var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Drive root tidak dapat dihapus demi keamanan sistem.";
            return false;
        }

        // 2. Cek Root Scan Aktif
        if (!string.IsNullOrWhiteSpace(activeScanRoot))
        {
            var activeRootNorm = Path.GetFullPath(activeScanRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, activeRootNorm, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Folder root pemindaian yang sedang aktif tidak dapat dihapus.";
                return false;
            }
        }

        // 3. Cek Lokasi Windows Kritis
        string[] protectedLocations =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // ProgramData
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) // Entire user profile root
        ];

        foreach (var loc in protectedLocations)
        {
            if (string.IsNullOrWhiteSpace(loc)) continue;
            var locNorm = Path.GetFullPath(loc).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(fullPath, locNorm, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Lokasi sistem terlindungi ({Path.GetFileName(locNorm)}) tidak dapat dihapus.";
                return false;
            }
        }

        return true;
    }

    public async Task<FileActionResult> MoveToRecycleBinAsync(
        IReadOnlyList<string> paths,
        string? activeScanRoot = null,
        CancellationToken cancellationToken = default)
    {
        if (paths == null || paths.Count == 0)
        {
            return new FileActionResult(true, 0, 0, Array.Empty<string>(), Array.Empty<string>());
        }

        int succeeded = 0;
        int failed = 0;
        var failedPaths = new List<string>();
        var errorMessages = new List<string>();

        await Task.Run(() =>
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!CanDelete(path, activeScanRoot, out string? reason))
                {
                    failed++;
                    failedPaths.Add(path);
                    errorMessages.Add(reason ?? "Lokasi dilindungi.");
                    continue;
                }

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    failed++;
                    failedPaths.Add(path);
                    errorMessages.Add("File atau direktori sudah tidak ada.");
                    continue;
                }

                bool deleted = SendToRecycleBin(path, out string? err);
                if (deleted)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                    failedPaths.Add(path);
                    errorMessages.Add(err ?? "Gagal memindahkan ke Recycle Bin.");
                }
            }
        }, cancellationToken).ConfigureAwait(false);

        return new FileActionResult(
            Success: failed == 0,
            SucceededCount: succeeded,
            FailedCount: failed,
            FailedPaths: failedPaths.AsReadOnly(),
            ErrorMessages: errorMessages.AsReadOnly());
    }

    private static bool SendToRecycleBin(string path, out string? error)
    {
        error = null;
        try
        {
            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                // pFrom harus berakhiran double-null \0\0
                pFrom = path + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            int result = SHFileOperation(ref shf);
            if (result != 0 || shf.fAnyOperationsAborted)
            {
                error = $"SHFileOperation gagal dengan kode error {result}.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
}
