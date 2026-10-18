namespace RuangDisk.Core.Models;

/// <summary>
/// Node representasi direktori atau file dalam pohon filesystem hasil scanning.
/// </summary>
public sealed class FileSystemNode
{
    /// <summary>
    /// Identitas stabil node untuk sinkronisasi antarkomponen UI (Tree, Treemap, Details, Largest Files).
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Nama file atau folder.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Path lengkap file atau folder.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Menentukan apakah node ini merupakan direktori atau file.
    /// </summary>
    public required bool IsDirectory { get; init; }

    /// <summary>
    /// Ukuran logis (dalam byte). Ukuran murni data file.
    /// </summary>
    public long LogicalSize { get; set; }

    /// <summary>
    /// Ukuran teralokasi (dalam byte) pada cluster disk fisik.
    /// </summary>
    public long AllocatedSize { get; set; }

    /// <summary>
    /// Jumlah file di dalam folder (termasuk subfolder) jika direktori, atau 1 jika file.
    /// </summary>
    public long FileCount { get; set; }

    /// <summary>
    /// Jumlah subfolder di dalam folder ini jika direktori, atau 0 jika file.
    /// </summary>
    public long DirectoryCount { get; set; }

    /// <summary>
    /// Waktu modifikasi terakhir.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// Menentukan apakah item merupakan junction, symbolic link, atau reparse point lain.
    /// </summary>
    public bool IsReparsePoint { get; init; }

    /// <summary>
    /// Menandakan apakah pemindaian node dan anak-anaknya sudah selesai.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Ekstensi file dengan tanda titik (.ext) atau string kosong jika tanpa ekstensi atau folder.
    /// </summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Daftar child nodes jika direktori.
    /// </summary>
    public List<FileSystemNode> Children { get; } = [];

    /// <summary>
    /// Menghitung ulang agregasi ukuran dan jumlah file/folder dari anak-anaknya.
    /// </summary>
    public void RecalculateAggregates()
    {
        if (!IsDirectory)
        {
            FileCount = 1;
            DirectoryCount = 0;
            return;
        }

        long logical = 0;
        long allocated = 0;
        long files = 0;
        long dirs = 0;

        foreach (var child in Children)
        {
            if (child.IsDirectory)
            {
                dirs += 1 + child.DirectoryCount;
                files += child.FileCount;
            }
            else
            {
                files += 1;
            }
            logical += child.LogicalSize;
            allocated += child.AllocatedSize;
        }

        LogicalSize = logical;
        AllocatedSize = allocated;
        FileCount = files;
        DirectoryCount = dirs;
    }
}
