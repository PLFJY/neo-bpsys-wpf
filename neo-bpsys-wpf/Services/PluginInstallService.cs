using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.Abstractions;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace neo_bpsys_wpf.Services;

public sealed class PluginInstallService(ILogger<PluginInstallService> logger) : IPluginInstallService
{
    public PluginInstallResult InstallFromExtractedDirectory(string extractedDirectoryPath)
    {
        var manifestPath = Path.Combine(extractedDirectoryPath, "manifest.yml");
        if (!File.Exists(manifestPath))
        {
            throw new Exception(I18nHelper.GetLocalizedString("CannotFindManifest"));
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var manifest = deserializer.Deserialize<PluginManifest?>(File.ReadAllText(manifestPath));
        if (manifest == null)
        {
            throw new Exception(I18nHelper.GetLocalizedString("ManifestNotValid"));
        }

        var compatibility = PluginApiVersionHelper.Evaluate(manifest.ApiVersion);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(compatibility.IsTooHigh
                ? I18nHelper.GetLocalizedString("PluginMarketInstallBlockedHostVersionTooLow")
                : compatibility.Message);
        }

        var pluginFolderPath = Path.Combine(AppConstants.PluginPath, manifest.Id);
        if (Directory.Exists(pluginFolderPath))
        {
            var stagedRoot = Path.Combine(AppConstants.PluginPath, ".new");
            Directory.CreateDirectory(stagedRoot);
            var stagedPath = Path.Combine(stagedRoot, manifest.Id);
            if (Directory.Exists(stagedPath))
            {
                Directory.Delete(stagedPath, true);
            }

            Directory.Move(extractedDirectoryPath, stagedPath);

            var local = IPluginService.LoadedPlugins.FirstOrDefault(x => x.Manifest.Id == manifest.Id);
            if (local != null)
            {
                local.IsRestartRequired = true;
                local.NewVersion = manifest.Version;
                local.IsNewVersionInstalled = true;
            }
            else
            {
                logger.LogWarning(
                    "Plugin directory already exists for {PluginId}, but no matching loaded plugin info was found.",
                    manifest.Id);
            }

            return new PluginInstallResult
            {
                Manifest = manifest,
                IsUpdate = true,
                RestartRequired = true
            };
        }

        Directory.CreateDirectory(AppConstants.PluginPath);
        Directory.Move(extractedDirectoryPath, pluginFolderPath);

        return new PluginInstallResult
        {
            Manifest = manifest,
            IsUpdate = false,
            RestartRequired = true
        };
    }
}
