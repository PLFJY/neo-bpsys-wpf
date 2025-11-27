using System.Text.Json;
using neo_bpsys.Core.Abstractions.Services;
using neo_bpsys.Core.Models;

namespace neo_bpsys.Core.Services;

public class SettingsHostService : ISettingsHostService
{
    private Settings _settings = new();
    public Settings Settings
    {
        get => _settings;
        set
        {
            if (_settings == value) return;
            _settings = value;
            SettingsChanged?.Invoke(this, value);
        }
    }

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void SaveConfig()
    {
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);
        var jsonStr = JsonSerializer.Serialize(Settings, _jsonSerializerOptions);
        File.WriteAllText(AppConstants.ConfigFilePath, jsonStr);
    }

    public void LoadConfig()
    {
        if (!File.Exists(AppConstants.ConfigFilePath)) ResetConfig();
        var json = File.ReadAllText(AppConstants.ConfigFilePath);
        var settings = JsonSerializer.Deserialize<Settings>(json, _jsonSerializerOptions);
        Settings = settings ?? new Settings();
    }

    public void ResetConfig()
    {
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);
        Settings = new Settings();
        SaveConfig();
        LoadConfig();
    }

    public event EventHandler<Settings>? SettingsChanged;
}
