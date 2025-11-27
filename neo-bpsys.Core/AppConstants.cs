using System;
using System.IO;

namespace neo_bpsys.Core;

public static class AppConstants
{
    public const string AppName = "neo-bpsys";
    public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    public static readonly string AppOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);
    public static readonly string ConfigFilePath = Path.Combine(AppDataPath, "Config.json");
    public static readonly string AppTempPath = Path.Combine(Path.GetTempPath(), AppName);
    public static readonly string CustomUiPath = Path.Combine(AppDataPath, "CustomUi");
    public static readonly string LogPath = Path.Combine(AppDataPath, "Log");
    public static readonly string ResourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
}
