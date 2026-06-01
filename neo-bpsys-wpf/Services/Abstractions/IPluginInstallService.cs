using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services.Abstractions;

public interface IPluginInstallService
{
    PluginInstallResult InstallFromExtractedDirectory(string extractedDirectoryPath);
}

public sealed class PluginInstallResult
{
    public required PluginManifest Manifest { get; init; }

    public bool IsUpdate { get; init; }

    public bool RestartRequired { get; init; }
}
