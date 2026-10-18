using System.IO;
using System.Text.Json;
using RuangDisk.Core.Utilities;

namespace RuangDisk.App.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System"; // System, Light, Dark
    public SizeUnitFormat SizeFormat { get; set; } = SizeUnitFormat.Binary;
    public bool IncludeHidden { get; set; } = false;
    public bool IncludeSystem { get; set; } = false;
    public bool FollowReparsePoints { get; set; } = false;
    public int LargestFilesLimit { get; set; } = 100;
    public List<string> ExclusionPatterns { get; set; } = ["*.tmp", "node_modules", "$Recycle.Bin"];
    public bool ConfirmBeforeRecycle { get; set; } = true;
    public List<string> RecentTargets { get; set; } = [];
}

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
    void AddRecentTarget(string path);
}

public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RuangDisk");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch
        {
            // Fallback ke default settings jika pembacaan gagal
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Abaikan kegagalan penulisan disk pengaturan
        }
    }

    public void AddRecentTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        Settings.RecentTargets.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Settings.RecentTargets.Insert(0, path);

        if (Settings.RecentTargets.Count > 10)
        {
            Settings.RecentTargets.RemoveRange(10, Settings.RecentTargets.Count - 10);
        }

        Save();
    }
}
