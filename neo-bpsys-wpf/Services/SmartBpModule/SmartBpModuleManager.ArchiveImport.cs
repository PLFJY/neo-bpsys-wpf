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
/// SmartBP 模块管理器的ArchiveImport逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    /// <summary>
    /// 通过暂存目录导入模块压缩包。
    /// </summary>
    /// <param name="archivePath">压缩包路径。</param>
    /// <param name="targetRoot">最终目标根目录。</param>
    /// <returns>成功导入并加载，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportArchiveAsync(string archivePath, string targetRoot)
    {
        return await ImportArchiveAsync(archivePath, targetRoot, "PreviewArchiveImport", progress: null);
    }

    /// <summary>
    /// 通过暂存目录导入模块压缩包，并在解压过程中报告进度。
    /// </summary>
    /// <param name="archivePath">压缩包路径。</param>
    /// <param name="targetRoot">最终目标根目录。</param>
    /// <param name="progress">解压阶段进度报告器。</param>
    /// <returns>成功导入并加载，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportArchiveAsync(
        string archivePath,
        string targetRoot,
        IProgress<ArchiveProgress>? progress)
    {
        return await ImportArchiveAsync(archivePath, targetRoot, "PreviewArchiveImport", progress);
    }

    /// <summary>
    /// 通过暂存目录导入模块压缩包。
    /// </summary>
    /// <param name="archivePath">压缩包路径。</param>
    /// <param name="targetRoot">最终目标根目录。</param>
    /// <param name="installKind">写入模块状态的安装来源标签。</param>
    /// <param name="progress">可选的解压阶段进度报告器。</param>
    /// <returns>成功导入并加载，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportArchiveAsync(
        string archivePath,
        string targetRoot,
        string installKind,
        IProgress<ArchiveProgress>? progress = null)
    {
        IsRestartRequiredForPendingModuleImport = false;
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            LastFailureMessage = "Target module path is empty.";
            return false;
        }

        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        _logger.LogInformation(
            "Importing SmartBP module archive. ArchivePath={ArchivePath}, TargetRoot={TargetRoot}, InstallKind={InstallKind}",
            archivePath,
            normalizedTargetRoot,
            installKind);
        if (IsUnsafeInstallPath(normalizedTargetRoot))
        {
            _logger.LogWarning("SmartBP module import target is unsafe or not writable: {TargetRoot}", normalizedTargetRoot);
            return false;
        }

        var staging = Path.Combine(AppConstants.AppTempPath, "SmartBpModule", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await _archiveService.ExtractToDirectoryAsync(archivePath, staging, progress);
            var candidateRoot = File.Exists(Path.Combine(staging, "component.json"))
                ? staging
                : Directory.EnumerateDirectories(staging).FirstOrDefault() ?? staging;
            if (!ValidateModuleDirectory(candidateRoot, allowDevelopmentDirectory: false, out var manifest, out var validationError))
            {
                LastFailureMessage = validationError;
                _logger.LogWarning("Imported SmartBP module archive failed validation: {ValidationError}", validationError);
                return false;
            }

            if (IsModulePhysicallyLoaded)
            {
                PrepareArchiveImportForRestart(candidateRoot, normalizedTargetRoot, installKind, manifest);
                return true;
            }

            try
            {
                ReplaceModuleRootPreservingManagedAssets(candidateRoot, normalizedTargetRoot);
                return await LoadModuleFromDirectoryAsync(normalizedTargetRoot, installKind);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogInformation(
                    ex,
                    "SmartBP module target could not be replaced immediately. Staging import for next restart. TargetRoot={TargetRoot}",
                    normalizedTargetRoot);
                PrepareArchiveImportForRestart(candidateRoot, normalizedTargetRoot, installKind, manifest);
                return true;
            }
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

}
