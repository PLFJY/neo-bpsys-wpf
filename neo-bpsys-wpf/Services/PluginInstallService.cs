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

public sealed class PluginInstallService(
    ILogger<PluginInstallService> logger,
    IArchiveService archiveService,
    IGlobalRestartService globalRestartService) : IPluginInstallService
{
    /// <inheritdoc />
    public PluginInstallResult InstallFromArchive(string archivePath, string extractedDirectoryPath)
    {
        return InstallFromArchiveAsync(archivePath, extractedDirectoryPath).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PluginInstallResult> InstallFromArchiveAsync(
        string archivePath,
        string extractedDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        await archiveService.ExtractToDirectoryAsync(archivePath, extractedDirectoryPath, cancellationToken: cancellationToken);
        return InstallFromExtractedDirectory(extractedDirectoryPath);
    }

    /// <inheritdoc />
    public PluginInstallResult InstallFromExtractedDirectory(string extractedDirectoryPath)
    {
        var manifestPath = Path.Combine(extractedDirectoryPath, "manifest.yml");
        if (!File.Exists(manifestPath))
        {
            throw new Exception(I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "CannotFindManifest"));
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var manifest = deserializer.Deserialize<PluginManifest?>(File.ReadAllText(manifestPath));
        if (manifest == null)
        {
            logger.LogError("Manifest file is not valid at {Path}", manifestPath);
            throw new Exception(I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "ManifestNotValid"));
        }

        var compatibility = PluginApiVersionHelper.Evaluate(manifest.ApiVersion);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(compatibility.IsTooHigh
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginMarketInstallBlockedHostVersionTooLow")
                : compatibility.Message);
        }

        // manifest.yml 来自外部插件包，插件 ID 属于不可信输入。必须先确认它只是单层目录名，
        // 否则绝对路径或 "..\.." 会让后续 Directory.Delete/Move 作用于插件目录之外的任意位置。
        ValidatePluginFolderName(manifest.Id);

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

            globalRestartService.IsRestartRequired = true;
            return new PluginInstallResult
            {
                Manifest = manifest,
                IsUpdate = true,
                RestartRequired = true
            };
        }

        Directory.CreateDirectory(AppConstants.PluginPath);
        Directory.Move(extractedDirectoryPath, pluginFolderPath);

        globalRestartService.IsRestartRequired = true;
        return new PluginInstallResult
        {
            Manifest = manifest,
            IsUpdate = false,
            RestartRequired = true
        };
    }

    /// <summary>
    /// 校验插件清单中的插件 ID 可以作为 <see cref="AppConstants.PluginPath"/> 下的单层目录名安全使用。
    /// </summary>
    /// <param name="pluginId">manifest.yml 中声明的插件 ID。</param>
    /// <exception cref="Exception">当插件 ID 不是安全的单层目录名时抛出。</exception>
    private static void ValidatePluginFolderName(string pluginId)
    {
        if (IsSafePluginFolderName(pluginId))
        {
            return;
        }

        throw new Exception(I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "ManifestIdInvalid"));
    }

    /// <summary>
    /// 判断插件 ID 是否是可以安全拼接为插件目录名的单层目录名。
    /// 拒绝空值、"."/".."、绝对路径、路径分隔符、非法文件名字符以及 Windows 会被忽略的结尾点/空格。
    /// </summary>
    /// <param name="pluginId">manifest.yml 中声明的插件 ID。</param>
    /// <returns>可以安全用作单层目录名时返回 <see langword="true"/>。</returns>
    private static bool IsSafePluginFolderName(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || pluginId is "." or "..")
        {
            return false;
        }

        if (Path.IsPathRooted(pluginId) || pluginId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        // Windows 会忽略目录名结尾的点和空格，避免 ".. " 这类名称被系统归一化成上级目录。
        if (pluginId[^1] is '.' or ' ')
        {
            return false;
        }

        // 兜底：按实际拼接结果再确认一次仍是插件目录的直接子项。
        var pluginRootPath = Path.GetFullPath(AppConstants.PluginPath) + Path.DirectorySeparatorChar;
        var candidatePath = Path.GetFullPath(Path.Combine(AppConstants.PluginPath, pluginId)) + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(pluginRootPath, StringComparison.OrdinalIgnoreCase);
    }
}
