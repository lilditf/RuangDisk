namespace RuangDisk.Core.Models;

/// <summary>
/// Informasi error saat scanning tanpa mengekspos exception mentah atau stack trace.
/// </summary>
public sealed record ScanError(
    string Path,
    string ErrorCode,
    string SafeMessage);
