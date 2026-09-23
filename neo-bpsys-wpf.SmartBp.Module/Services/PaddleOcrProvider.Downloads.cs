using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Shared;
using System.Collections;
using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// PaddleOCR 提供者的模型下载逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 下载并解压单个模型组件资源。
    /// </summary>
    /// <param name="sourceUri">资源地址。</param>
    /// <param name="targetDirectory">目标目录。</param>
    /// <param name="stageText">阶段状态文本。</param>
    /// <param name="stepIndex">当前步骤序号（从 1 开始）。</param>
    /// <param name="stepCount">总步骤数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    private async Task DownloadAndExtractModelAssetAsync(
        Uri sourceUri,
        string targetDirectory,
        string stageText,
        int stepIndex,
        int stepCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _currentDownloadStep = stepIndex;
        _totalDownloadSteps = stepCount;

        DownloadStatusText = stageText;
        RaiseDownloadStateChanged();

        var cacheKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri)));
        var tempDirectory = Path.Combine(AppConstants.AppTempPath, "OcrModelDownload", cacheKey);
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var archivePath = Path.Combine(tempDirectory, "model.tar");
            await DownloadAssetAsync(sourceUri.ToString(), archivePath, cancellationToken);
            ExtractModelAsset(archivePath, targetDirectory);
        }
        finally
        {
            var archivePath = Path.Combine(tempDirectory, "model.tar");
            var hasResumablePartial = File.Exists(archivePath + ".download.part");
            if (Directory.Exists(tempDirectory) && !hasResumablePartial)
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// 下载文件到指定路径。
    /// </summary>
    /// <param name="sourceUrl">资源地址。</param>
    /// <param name="destinationFilePath">目标文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    private async Task DownloadAssetAsync(
        string sourceUrl,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SmartBpParallelDownload.DownloadFileAsync(
            _fileDownloadService,
            sourceUrl,
            destinationFilePath,
            cancellationToken,
            progress => OnDownloadProgressChanged(progress),
            operation => _currentDownload = operation);
    }

    /// <summary>
    /// 解压模型归档并复制有效模型文件到目标目录。
    /// </summary>
    /// <param name="archivePath">归档文件路径。</param>
    /// <param name="targetDirectory">目标目录。</param>
    private static void ExtractModelAsset(string archivePath, string targetDirectory)
    {
        var extractDirectory = Path.Combine(Path.GetDirectoryName(archivePath)!, "extract");
        Directory.CreateDirectory(extractDirectory);

        TarFile.ExtractToDirectory(archivePath, extractDirectory, overwriteFiles: true);

        var sourceDirectory = ResolveModelSourceDirectory(extractDirectory);
        if (sourceDirectory == null)
        {
            var sample = string.Join(
                ", ",
                Directory.EnumerateFiles(extractDirectory, "*", SearchOption.AllDirectories)
                    .Take(12)
                    .Select(path => Path.GetRelativePath(extractDirectory, path)));
            throw new InvalidOperationException(
                Lf("SmartBpOcrArchiveMissingModelFilesFormat", sample));
        }

        RecreateDirectory(targetDirectory);
        CopyDirectoryContent(sourceDirectory, targetDirectory);
    }

    /// <summary>
    /// 在解压目录中定位实际模型文件所在目录。
    /// </summary>
    /// <param name="extractDirectory">解压根目录。</param>
    /// <returns>模型目录；未找到时返回 <see langword="null"/>。</returns>
    private static string? ResolveModelSourceDirectory(string extractDirectory)
    {
        // Paddle 3.x/PIR 模型可能使用 inference.json；旧格式使用 inference.pdmodel。
        return Directory
            .EnumerateFiles(extractDirectory, "inference.pdiparams", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .FirstOrDefault(dir =>
                File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                File.Exists(Path.Combine(dir, "inference.json")));
    }

    /// <summary>
    /// 重建目录：若已存在则先删除再创建。
    /// </summary>
    /// <param name="directoryPath">目录路径。</param>
    private static void RecreateDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }

        Directory.CreateDirectory(directoryPath);
    }

    /// <summary>
    /// 递归复制目录内容到目标目录。
    /// </summary>
    /// <param name="sourceDirectory">源目录。</param>
    /// <param name="targetDirectory">目标目录。</param>
    private static void CopyDirectoryContent(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    /// <summary>
    /// 清理模型下载失败后残留的目录与字典文件。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    private static void CleanupModelDownloadResidue(string modelKey)
    {
        var modelDirectory = SmartBpOcrModelRegistry.GetModelDirectory(modelKey);
        if (Directory.Exists(modelDirectory))
        {
            Directory.Delete(modelDirectory, recursive: true);
        }

        var dictPath = SmartBpOcrModelRegistry.GetRecDictPath(modelKey);
        if (File.Exists(dictPath))
        {
            File.Delete(dictPath);
        }
    }

    /// <summary>
    /// 处理下载进度变化并更新总体进度。
    /// </summary>
    /// <param name="progress">下载进度参数。</param>
    private void OnDownloadProgressChanged(FileDownloadProgress progress)
    {
        var stepProgress = (progress.Percentage ?? 0) / 100.0;
        var overallProgress = ((_currentDownloadStep - 1) + stepProgress) / _totalDownloadSteps * 100;

        DownloadProgress = overallProgress;
        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 触发下载状态变化事件。
    /// </summary>
    private void RaiseDownloadStateChanged() => DownloadStateChanged?.Invoke(this, EventArgs.Empty);
}
