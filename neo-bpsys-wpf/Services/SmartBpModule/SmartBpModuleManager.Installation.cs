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
/// SmartBP 模块管理器的Installation逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    /// <summary>
    /// 下载当前应用标签对应的 SmartBP 模块包，并通过暂存目录完成安装。
    /// </summary>
    /// <param name="targetRoot">最终目标根目录。</param>
    /// <param name="progress">可选的进度报告器，范围 0 到 100。</param>
    /// <param name="extractionProgress">可选的解压阶段进度报告器，用于细化下载流程中解压包时的进度反馈。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功下载并安装，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> DownloadAndInstallCurrentModuleAsync(
        string targetRoot,
        IProgress<double>? progress,
        IProgress<ArchiveProgress>? extractionProgress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting SmartBP module download and install. TargetRoot={TargetRoot}", targetRoot);
        if (IsPreviewBuild())
        {
            _logger.LogInformation("SmartBP module online install is disabled in Preview builds.");
            return false;
        }

        if (IsUnsafeInstallPath(targetRoot))
        {
            _logger.LogWarning("SmartBP module target root is unsafe or not writable: {TargetRoot}", targetRoot);
            return false;
        }

        var manifest = await TryFetchRequiredModuleManifestAsync();
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Asset.Url))
        {
            _logger.LogWarning("SmartBP module manifest is unavailable or missing asset URL.");
            return false;
        }

        var url = GetMirroredDownloadUrl(manifest.Asset.Url.Replace("{tag}", AppConstants.AppVersion, StringComparison.OrdinalIgnoreCase));
        var downloadRoot = Path.Combine(
            AppConstants.AppTempPath,
            "SmartBpModule",
            "Downloads",
            AppConstants.AppVersion);
        var archivePath = Path.Combine(downloadRoot, Path.GetFileName(manifest.Asset.Name));
        Directory.CreateDirectory(downloadRoot);
        try
        {
            progress?.Report(5);
            _logger.LogInformation(
                "Downloading SmartBP module package. Url={Url}, ArchivePath={ArchivePath}",
                url,
                archivePath);
            var operation = _fileDownloadService.CreateDownload(new FileDownloadRequest(
                new Uri(url, UriKind.Absolute),
                archivePath)
            {
                UserAgent = AppConstants.AppName
            });
            _currentModuleDownload = operation;
            operation.StateChanged += OnModuleDownloadStateChanged;
            try
            {
                operation.StateChanged += (_, _) =>
                {
                    if (operation.Progress.Percentage is { } percentage)
                        progress?.Report(5 + percentage * 0.65);
                };
                await operation.StartAsync(cancellationToken);
            }
            finally
            {
                operation.StateChanged -= OnModuleDownloadStateChanged;
                if (ReferenceEquals(_currentModuleDownload, operation))
                    _currentModuleDownload = null;
                DownloadStateChanged?.Invoke(this, EventArgs.Empty);
            }

            progress?.Report(70);
            if (!string.IsNullOrWhiteSpace(manifest.Asset.Sha256))
            {
                var actual = ComputeSha256(archivePath);
                if (!string.Equals(actual, manifest.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("SmartBP module hash mismatch. Expected={Expected}, Actual={Actual}", manifest.Asset.Sha256, actual);
                    return false;
                }
            }

            progress?.Report(80);
            var installed = await ImportArchiveAsync(archivePath, targetRoot, "LiteDownload", extractionProgress);
            _logger.LogInformation("SmartBP module download install completed. Installed={Installed}", installed);
            progress?.Report(installed ? 100 : 80);
            return installed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download and install SmartBP module.");
            return false;
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }

    private void OnModuleDownloadStateChanged(object? sender, EventArgs e) =>
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);

}
