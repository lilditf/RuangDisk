namespace RuangDisk.Core.Models;

/// <summary>
/// Status siklus hidup proses scanning.
/// </summary>
public enum ScanState
{
    Idle,
    Preparing,
    Scanning,
    Paused,
    Completed,
    Canceled,
    Failed
}
