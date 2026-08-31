using System.Text.Json;

namespace AudioDeviceGuardian;

/// <summary>
/// Persisted preferences: which devices to enforce, and whether enforcement
/// is currently suspended. Devices are matched by name substring rather than
/// ID, since IDs can change across reboots/reconnects more often than names do.
/// </summary>
public sealed class AppConfig
{
    public string? PreferredPlaybackDevice { get; set; }
    public string? PreferredRecordingDevice { get; set; }

    // Optional: Windows tracks a separate "communications" role default
    // (used by softphones, Teams, etc). Leave blank to skip enforcing it.
    public string? PreferredPlaybackCommsDevice { get; set; }
    public string? PreferredRecordingCommsDevice { get; set; }

    public bool Suspended { get; set; }
    public DateTime? SuspendedUntilUtc { get; set; }

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioDeviceGuardian");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static string ConfigFolder => ConfigDir;

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crash at startup.
        }

        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static void OpenInEditor()
    {
        Directory.CreateDirectory(ConfigDir);
        if (!File.Exists(ConfigPath))
            new AppConfig().Save();

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigPath)
        {
            UseShellExecute = true
        });
    }
}
