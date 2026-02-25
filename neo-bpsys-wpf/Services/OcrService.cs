using System.Formats.Tar;
using System.IO;
using System.Text;
using Downloader;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Shared;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// OCR 服务实现。
/// 提供模型管理（枚举、下载、删除、切换）与图像文本识别能力。
/// </summary>
public class OcrService : IOcrService
{
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<OcrService> _logger;
    private readonly Lock _ocrLock = new();
    private readonly Lock _downloadLock = new();
    private readonly DownloadService _downloader;

    private PaddleOcrAll? _ocr;
    private CancellationTokenSource? _downloadCts;
    private int _currentDownloadStep = 1;
    private int _totalDownloadSteps = 1;
    private int _missingModelWarningShown;

    /// <summary>
    /// 当前正在使用的 OCR 模型键。
    /// </summary>
    public string? CurrentOcrModelKey { get; private set; }

    /// <summary>
    /// 当前是否处于模型下载中。
    /// </summary>
    public bool IsDownloading { get; private set; }

    /// <summary>
    /// 当前下载进度（0-100）；未知时为 <see langword="null"/>。
    /// </summary>
    public double? DownloadProgress { get; private set; }

    /// <summary>
    /// 当前下载状态文本。
    /// </summary>
    public string DownloadStatusText { get; private set; } = string.Empty;

    /// <summary>
    /// 下载状态变化事件。
    /// </summary>
    public event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 初始化 OCR 服务并尝试加载用户偏好模型。
    /// </summary>
    /// <param name="settingsHostService">设置服务。</param>
    public OcrService(ISettingsHostService settingsHostService, ILogger<OcrService> logger)
    {
        _settingsHostService = settingsHostService;
        _logger = logger;

        var downloadOpt = new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 3,
            ParallelCount = 6,
        };

        _downloader = new DownloadService(downloadOpt);
        _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;

        TryLoadPreferredModel();
    }

    /// <summary>
    /// 获取可用 OCR 模型定义列表。
    /// </summary>
    /// <returns>模型定义列表。</returns>
    public IReadOnlyList<OcrModelDefinition> GetAvailableModels() =>
    [
        .. SmartBpOcrModelRegistry.Models.Select(m => new OcrModelDefinition(
            m.Key,
            m.DisplayNameKey,
            m.DescriptionKey))
    ];

    /// <summary>
    /// 判断指定模型是否已安装。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>已安装返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool IsModelInstalled(string modelKey) => SmartBpOcrModelRegistry.IsModelInstalled(modelKey);

    /// <summary>
    /// 下载并解压指定 OCR 模型。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out var definition))
        {
            throw new InvalidOperationException($"不支持的 OCR 模型：{modelKey}");
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                throw new InvalidOperationException("已有 OCR 模型下载任务正在进行。");
            }

            IsDownloading = true;
            DownloadProgress = null;
            DownloadStatusText = "准备下载 OCR 模型...";
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            RaiseDownloadStateChanged();
        }

        try
        {
            await DownloadAndExtractModelAssetAsync(
                definition.OnlineModel.DetModel?.Uri
                    ?? throw new InvalidOperationException("OCR 检测模型元数据为空。"),
                SmartBpOcrModelRegistry.GetDetDirectory(definition.Key),
                "正在下载检测模型...",
                stepIndex: 1,
                stepCount: 3,
                _downloadCts.Token);

            await DownloadAndExtractModelAssetAsync(
                definition.OnlineModel.ClsModel?.Uri
                    ?? throw new InvalidOperationException("OCR 分类模型元数据为空。"),
                SmartBpOcrModelRegistry.GetClsDirectory(definition.Key),
                "正在下载方向分类模型...",
                stepIndex: 2,
                stepCount: 3,
                _downloadCts.Token);

            var recModel = definition.OnlineModel.RecModel
                ?? throw new InvalidOperationException("OCR 识别模型元数据为空。");

            await DownloadAndExtractModelAssetAsync(
                recModel.Uri,
                SmartBpOcrModelRegistry.GetRecDirectory(definition.Key),
                "正在下载识别模型...",
                stepIndex: 3,
                stepCount: 3,
                _downloadCts.Token);

            if (recModel.Version != ModelVersion.V5)
            {
                if (string.IsNullOrWhiteSpace(recModel.DictName))
                {
                    throw new InvalidOperationException("OCR 字典名称为空。");
                }

                var dicts = SharedUtils.LoadDicts(recModel.DictName);
                Directory.CreateDirectory(SmartBpOcrModelRegistry.GetModelDirectory(definition.Key));
                File.WriteAllLines(SmartBpOcrModelRegistry.GetRecDictPath(definition.Key), dicts, Encoding.UTF8);
            }

            DownloadProgress = 100;
            DownloadStatusText = "OCR 模型下载完成。";
            RaiseDownloadStateChanged();
        }
        catch (OperationCanceledException)
        {
            DownloadProgress = null;
            DownloadStatusText = "下载已取消。";
            RaiseDownloadStateChanged();
            throw;
        }
        catch
        {
            CleanupModelDownloadResidue(definition.Key);
            DownloadProgress = null;
            DownloadStatusText = "下载失败。";
            RaiseDownloadStateChanged();
            throw;
        }
        finally
        {
            lock (_downloadLock)
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }

            RaiseDownloadStateChanged();
        }
    }

    /// <summary>
    /// 取消当前下载任务。
    /// </summary>
    public void CancelDownload()
    {
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
        }

        _downloader.CancelAsync();
    }

    /// <summary>
    /// 尝试删除指定模型及其本地缓存文件。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>删除成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool TryDeleteModel(string modelKey, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out _))
        {
            errorMessage = $"不支持的 OCR 模型：{modelKey}";
            return false;
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                errorMessage = "下载进行中，无法删除模型。";
                return false;
            }
        }

        try
        {
            var deletingCurrent = CurrentOcrModelKey == modelKey;
            if (deletingCurrent)
            {
                lock (_ocrLock)
                {
                    _ocr?.Dispose();
                    _ocr = null;
                }

                CurrentOcrModelKey = null;
            }

            var modelDirectory = SmartBpOcrModelRegistry.GetModelDirectory(modelKey);
            if (Directory.Exists(modelDirectory))
            {
                Directory.Delete(modelDirectory, recursive: true);
            }

            if (_settingsHostService.Settings.OcrModelKey == modelKey)
            {
                _settingsHostService.Settings.OcrModelKey = null;
                _ = _settingsHostService.SaveConfigAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"删除 OCR 模型失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 尝试切换当前 OCR 模型并加载推理实例。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>切换成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool TrySwitchOcrModel(string modelKey, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out var definition))
        {
            errorMessage = $"不支持的 OCR 模型：{modelKey}";
            return false;
        }

        if (!SmartBpOcrModelRegistry.IsModelInstalled(modelKey))
        {
            errorMessage = "模型文件未完整下载，请先下载。";
            return false;
        }

        try
        {
            var fullModel = BuildLocalFullModel(modelKey, definition);
            var nextOcr = new PaddleOcrAll(fullModel, PaddleDevice.Mkldnn());

            lock (_ocrLock)
            {
                _ocr?.Dispose();
                _ocr = nextOcr;
                _ocr.AllowRotateDetection = false;
                // 游戏内文本方向稳定，关闭 180 分类可减少每次 OCR 的额外开销。
                _ocr.Enable180Classification = false;
            }

            _missingModelWarningShown = 0;
            CurrentOcrModelKey = modelKey;
            PersistCurrentModel(modelKey);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"加载 OCR 模型失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 识别图像中的文本。
    /// </summary>
    /// <param name="img">待识别图像。</param>
    /// <returns>识别文本；失败时返回 <see langword="null"/>。</returns>
    public string? RecognizeText(Mat img)
    {
        if (img.Empty()) return null;

        // PaddleOCR 全流程更稳的是 8UC3(BGR)
        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeTextCore(bgr);
        }

        return RecognizeTextCore(img);
    }

    private string? RecognizeTextCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            if (_ocr is not null)
            {
                try
                {
                    var r = _ocr.Run(bgr);
                    return string.IsNullOrWhiteSpace(r.Text) ? null : r.Text;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR run failed, trying to rebuild OCR predictor and retry once.");
                    if (!TryRebuildCurrentOcrUnsafe())
                    {
                        _logger.LogError("OCR rebuild failed, recognition aborted.");
                        return null;
                    }

                    try
                    {
                        var retry = _ocr!.Run(bgr);
                        return string.IsNullOrWhiteSpace(retry.Text) ? null : retry.Text;
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "OCR retry failed after rebuild.");
                        return null;
                    }
                }
            }
        }

        ShowMissingModelWarningOnce();
        return null;
    }

    /// <summary>
    /// 在 OCR 推理异常后尝试重建当前模型实例。
    /// 该方法要求调用方已持有 <see cref="_ocrLock"/>。
    /// </summary>
    private bool TryRebuildCurrentOcrUnsafe()
    {
        if (string.IsNullOrWhiteSpace(CurrentOcrModelKey))
            return false;

        if (!SmartBpOcrModelRegistry.TryGet(CurrentOcrModelKey, out var definition))
            return false;

        if (!SmartBpOcrModelRegistry.IsModelInstalled(CurrentOcrModelKey))
            return false;

        try
        {
            var fullModel = BuildLocalFullModel(CurrentOcrModelKey, definition);
            var rebuilt = new PaddleOcrAll(fullModel, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = false,
                Enable180Classification = false
            };

            _ocr?.Dispose();
            _ocr = rebuilt;
            _logger.LogInformation("OCR predictor rebuilt successfully for model: {ModelKey}", CurrentOcrModelKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild OCR predictor for model: {ModelKey}", CurrentOcrModelKey);
            return false;
        }
    }

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

        var tempDirectory = Path.Combine(
            AppConstants.AppTempPath,
            "OcrModelDownload",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var archivePath = Path.Combine(tempDirectory, "model.tar");
            await DownloadAssetAsync(sourceUri.ToString(), archivePath, cancellationToken);
            ExtractModelAsset(archivePath, targetDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private async Task DownloadAssetAsync(
        string sourceUrl,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _downloader.DownloadFileTaskAsync(sourceUrl, destinationFilePath);
    }

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
                $"模型压缩包中未找到可用模型文件（期望 inference.pdmodel 或 inference.json，且需包含 inference.pdiparams）。样例文件：{sample}");
        }

        RecreateDirectory(targetDirectory);
        CopyDirectoryContent(sourceDirectory, targetDirectory);
    }

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

    private static void RecreateDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }

        Directory.CreateDirectory(directoryPath);
    }

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

    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        var stepProgress = e.ProgressPercentage / 100.0;
        var overallProgress = ((_currentDownloadStep - 1) + stepProgress) / _totalDownloadSteps * 100;

        DownloadProgress = overallProgress;
        RaiseDownloadStateChanged();
    }

    private void RaiseDownloadStateChanged()
    {
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TryLoadPreferredModel()
    {
        string? preferredModel = _settingsHostService.Settings.OcrModelKey;

        if (string.IsNullOrWhiteSpace(preferredModel))
            return;

        _ = TrySwitchOcrModel(preferredModel, out _);
    }

    private void ShowMissingModelWarningOnce()
    {
        if (Interlocked.Exchange(ref _missingModelWarningShown, 1) == 1)
            return;

        _ = MessageBoxHelper.ShowErrorAsync("OCR 功能未就绪，请先下载并切换 OCR 模型后再启动相关功能。");
    }

    private void PersistCurrentModel(string modelKey)
    {
        _settingsHostService.Settings.OcrModelKey = modelKey;
        _ = _settingsHostService.SaveConfigAsync();
    }

    private static FullOcrModel BuildLocalFullModel(string modelKey, SmartBpOcrModelDefinition definition)
    {
        var onlineDet = definition.OnlineModel.DetModel
            ?? throw new InvalidOperationException("OCR 检测模型元数据为空。");
        var onlineCls = definition.OnlineModel.ClsModel
            ?? throw new InvalidOperationException("OCR 分类模型元数据为空。");
        var onlineRec = definition.OnlineModel.RecModel
            ?? throw new InvalidOperationException("OCR 识别模型元数据为空。");

        var detModel = DetectionModel.FromDirectory(
            SmartBpOcrModelRegistry.GetDetDirectory(modelKey),
            onlineDet.Version);
        var clsModel = ClassificationModel.FromDirectory(
            SmartBpOcrModelRegistry.GetClsDirectory(modelKey),
            onlineCls.Version);

        RecognizationModel recModel = onlineRec.Version switch
        {
            ModelVersion.V5 => RecognizationModel.FromDirectoryV5(SmartBpOcrModelRegistry.GetRecDirectory(modelKey)),
            _ => RecognizationModel.FromDirectory(
                SmartBpOcrModelRegistry.GetRecDirectory(modelKey),
                SmartBpOcrModelRegistry.GetRecDictPath(modelKey),
                onlineRec.Version)
        };

        return new FullOcrModel(detModel, clsModel, recModel);
    }

}
