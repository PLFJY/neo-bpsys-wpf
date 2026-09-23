using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// SmartBP 模块管理器的Operations逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    /// <summary>
    /// 执行功能命令。
    /// </summary>
    /// <param name="commandId">命令标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步操作的任务。</returns>
    public Task ExecuteFeatureCommandAsync(string commandId, CancellationToken cancellationToken)
    {
        if (!IsModuleLoaded)
            return Task.CompletedTask;

        var command = _featureCommands.FirstOrDefault(c => c.CommandId == commandId);
        return command?.ExecuteAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>
    /// 赛后数据识别进度变化时触发。
    /// </summary>
    public event EventHandler<SmartBpPostGameRecognitionProgressEventArgs>? PostGameRecognitionProgressChanged;

    /// <summary>
    /// 获取最近一次赛后数据识别进度快照；模块未加载或未开始识别时为 <see cref="SmartBpPostGameRecognitionProgress.Idle"/>。
    /// </summary>
    public SmartBpPostGameRecognitionProgress CurrentPostGameRecognitionProgress
        => _postGameRecognitionProgressSource?.CurrentProgress ?? SmartBpPostGameRecognitionProgress.Idle;

    private void OnPostGameRecognitionProgressChanged(object? sender, SmartBpPostGameRecognitionProgressEventArgs e)
        => PostGameRecognitionProgressChanged?.Invoke(sender, e);

    /// <summary>
    /// 校验模块目录。
    /// </summary>
    /// <param name="moduleRoot">模块根目录。</param>
    /// <param name="allowDevelopmentDirectory">是否允许调试开发目录。</param>
    /// <param name="manifest">组件清单。</param>
    /// <param name="error">校验错误。</param>
    /// <returns>校验结果。</returns>
    public bool ValidateModuleDirectory(
        string moduleRoot,
        bool allowDevelopmentDirectory,
        out SmartBpModuleManifest? manifest,
        out string error)
    {
        manifest = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(moduleRoot) || !Directory.Exists(moduleRoot))
        {
            error = "Module directory does not exist.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} ModuleRoot={ModuleRoot}", error, moduleRoot);
            return false;
        }

        var entryAssembly = Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName);
        if (!File.Exists(entryAssembly))
        {
            error = "Module entry assembly is missing.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} EntryAssembly={EntryAssembly}", error, entryAssembly);
            return false;
        }

        var componentPath = Path.Combine(moduleRoot, "component.json");
        if (!File.Exists(componentPath))
        {
            _logger.LogDebug(
                "SmartBP module component manifest is missing. AllowDevelopmentDirectory={AllowDevelopmentDirectory}, ModuleRoot={ModuleRoot}",
                allowDevelopmentDirectory,
                moduleRoot);
            return allowDevelopmentDirectory;
        }

        manifest = JsonSerializer.Deserialize<SmartBpModuleManifest>(File.ReadAllText(componentPath), JsonOptions);
        if (manifest == null || manifest.ComponentId != SmartBpModuleConstants.ComponentId)
        {
            error = "ComponentId mismatch.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} ComponentPath={ComponentPath}", error, componentPath);
            return false;
        }

        if (!string.Equals(manifest.Rid, SmartBpModuleConstants.Rid, StringComparison.OrdinalIgnoreCase))
        {
            error = "RID mismatch.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} LocalRid={LocalRid}, ExpectedRid={ExpectedRid}", error, manifest.Rid, SmartBpModuleConstants.Rid);
            return false;
        }

        if (manifest.RuntimeAbiVersion != SmartBpModuleConstants.RuntimeAbiVersion)
        {
            error = "Runtime ABI mismatch.";
            _logger.LogDebug(
                "SmartBP module directory validation failed: {Error} LocalAbi={LocalAbi}, ExpectedAbi={ExpectedAbi}",
                error,
                manifest.RuntimeAbiVersion,
                SmartBpModuleConstants.RuntimeAbiVersion);
            return false;
        }

        _logger.LogDebug(
            "SmartBP module directory validation succeeded. ModuleRoot={ModuleRoot}, ModuleVersion={ModuleVersion}, RuntimeAbiVersion={RuntimeAbiVersion}, Rid={Rid}",
            moduleRoot,
            manifest.ModuleVersion,
            manifest.RuntimeAbiVersion,
            manifest.Rid);
        return true;
    }

    /// <summary>
    /// 判断路径是否不适合作为模块安装目录。
    /// </summary>
    /// <param name="path">候选路径。</param>
    /// <returns>路径不安全时返回 <see langword="true"/>。</returns>
    public static bool IsUnsafeInstallPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            return true;

        var blocked = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory
        };
        return blocked.Any(b => !string.IsNullOrWhiteSpace(b) && IsSameOrChildPath(full, b)) ||
               !HasWriteAccess(full);
    }

    /// <summary>
    /// 计算文件的 SHA-256 哈希。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>小写 SHA-256 哈希字符串。</returns>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 获取当前应用发布标签对应的 SmartBP 模块 manifest。
    /// </summary>
    /// <returns>要求的模块清单；离线、不可用或不需要时返回 <see langword="null"/>。</returns>
    public async Task<SmartBpModuleManifest?> TryFetchRequiredModuleManifestAsync()
    {
        if (IsDebugBuild() || IsPreviewBuild())
            return null;

        return await TryFetchCurrentTagManifestAsync();
    }

    /// <summary>
    /// 判断本地模块版本是否满足要求的模块版本。
    /// </summary>
    /// <param name="localVersion">本地模块版本。</param>
    /// <param name="requiredVersion">要求的模块版本。</param>
    /// <returns>本地版本等于或新于要求版本时返回 <see langword="true"/>。</returns>
    public static bool IsModuleVersionAllowed(string localVersion, string requiredVersion)
    {
        if (Version.TryParse(localVersion.Replace('-', '.'), out var local) &&
            Version.TryParse(requiredVersion.Replace('-', '.'), out var required))
        {
            return local >= required;
        }

        return string.Compare(localVersion, requiredVersion, StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
