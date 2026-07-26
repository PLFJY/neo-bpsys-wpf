using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal interface ILegacyFrontendInputSource
{
    string ConfigPath { get; }

    string? CustomUiRoot { get; }

    IEnumerable<string> EnumerateLegacyLayoutFiles();

    Stream OpenConfig();

    Stream OpenLegacyLayoutFile(string filePath);
}

internal sealed class LegacyBpuiDirectoryInputSource(string extractionRoot) : ILegacyFrontendInputSource
{
    public string ConfigPath { get; } = Path.Combine(extractionRoot, "Config.json");

    public string? CustomUiRoot { get; } = Path.Combine(extractionRoot, "CustomUi");

    public IEnumerable<string> EnumerateLegacyLayoutFiles()
    {
        var root = Path.Combine(extractionRoot, "FrontElementsConfig");
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            : [];
    }

    public Stream OpenConfig() => File.OpenRead(ConfigPath);

    public Stream OpenLegacyLayoutFile(string filePath) => File.OpenRead(filePath);
}

internal sealed class LegacyLocalAppDataInputSource(string appDataRoot) : ILegacyFrontendInputSource
{
    public string ConfigPath { get; } = Path.Combine(appDataRoot, "Config.json");

    public string? CustomUiRoot { get; } = Path.Combine(appDataRoot, "CustomUi");

    public IEnumerable<string> EnumerateLegacyLayoutFiles()
    {
        if (!Directory.Exists(appDataRoot))
        {
            return [];
        }

        var known = FrontedLayoutPackageLegacyConverter.LegacyLayoutFileNames
            .Select(fileName => Path.Combine(appDataRoot, fileName))
            .Where(File.Exists);
        var unknown = Directory.EnumerateFiles(appDataRoot, "*Config-*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !FrontedLayoutPackageLegacyConverter.IsKnownLegacyLayoutFileName(Path.GetFileName(path)!));
        return known.Concat(unknown);
    }

    public Stream OpenConfig() => File.OpenRead(ConfigPath);

    public Stream OpenLegacyLayoutFile(string filePath) => File.OpenRead(filePath);
}
