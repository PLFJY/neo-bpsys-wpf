using Downloader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Shared;
using System.Collections;
using System.Formats.Tar;
using System.IO;
using System.Text;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// OCR 服务实现。
/// 提供模型管理（枚举、下载、删除、切换）与图像文本识别能力。
/// </summary>
public sealed class PaddleOcrProvider : IOcrProvider
{
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<PaddleOcrProvider> _logger;
    private readonly IPaddleRuntimeState _runtimeState;
    private readonly IGlobalRestartService _globalRestartService;
    private readonly Lock _ocrLock = new();
    private readonly Lock _downloadLock = new();

    private PaddleOcrAll? _ocr;
    private CancellationTokenSource? _downloadCts;
    private int _currentDownloadStep = 1;
    private int _totalDownloadSteps = 1;
    private int _missingModelWarningShown;
    private volatile bool _isModelLoading;

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
    /// 获取 OCR 模型是否正在后台加载。
    /// </summary>
    public bool IsModelLoading => _isModelLoading;

    /// <summary>
    /// 模型加载状态变化时触发。
    /// </summary>
    public event EventHandler? ModelLoadStateChanged;

    private void RaiseModelLoadStateChanged() => ModelLoadStateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 初始化 OCR 服务。构造函数仅注册路径，不加载模型——模型加载由
    /// <see cref="StartLoadingPreferredModel"/> 在页面空闲后触发，避免阻塞 DI 解析
    /// 和引发原生 DLL loader lock 死锁。
    /// </summary>
    /// <param name="settingsHostService">设置服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="modelPathProvider">OCR 模型路径提供器。</param>
    /// <param name="runtimeState">Paddle runtime 运行时状态，用于决定推理后端（CPU/CUDA）。</param>
    /// <param name="globalRestartService">全局重启服务，用于在 CUDA 故障时标记需要重启。</param>
    public PaddleOcrProvider(
        ISettingsHostService settingsHostService,
        ILogger<PaddleOcrProvider> logger,
        ISmartBpOcrModelPathProvider modelPathProvider,
        IPaddleRuntimeState runtimeState,
        IGlobalRestartService globalRestartService)
    {
        _settingsHostService = settingsHostService;
        _logger = logger;
        _runtimeState = runtimeState;
        _globalRestartService = globalRestartService;
        SmartBpOcrModelRegistry.ConfigurePathProvider(modelPathProvider);
    }

    /// <summary>
    /// 在后台线程加载用户偏好 OCR 模型。应在页面显示完毕后调用，避免与 UI 线程
    /// 的渲染/DLL 加载竞争 Windows loader lock。
    /// </summary>
    public void StartLoadingPreferredModel()
    {
        if (_isModelLoading || _ocr != null)
            return;
        _ = Task.Run(TryLoadPreferredModel);
    }

    /// <inheritdoc />
    public SmartBpOcrProviderKind Kind => SmartBpOcrProviderKind.Paddle;

    /// <inheritdoc />
    public bool IsReady
    {
        get
        {
            lock (_ocrLock)
                return _ocr != null;
        }
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
            throw new InvalidOperationException(Lf("SmartBpOcrUnsupportedModelFormat", modelKey));
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                throw new InvalidOperationException(L("SmartBpOcrDownloadAlreadyInProgress"));
            }

            IsDownloading = true;
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadPreparing");
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            RaiseDownloadStateChanged();
        }

        try
        {
            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    definition.DetModel,
                    L("SmartBpOcrDetModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetDetDirectory(definition.Key),
                L("SmartBpOcrDownloadStageDet"),
                stepIndex: 1,
                stepCount: 3,
                _downloadCts.Token);

            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    definition.ClsModel,
                    L("SmartBpOcrClsModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetClsDirectory(definition.Key),
                L("SmartBpOcrDownloadStageCls"),
                stepIndex: 2,
                stepCount: 3,
                _downloadCts.Token);

            if (definition.RecModel == null)
            {
                _logger.LogError("Recognition model metadata is empty.");
                throw new InvalidOperationException(L("SmartBpOcrRecModelMetadataEmpty"));
            }
            var recModel = definition.RecModel;

            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    recModel,
                    L("SmartBpOcrRecModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetRecDirectory(definition.Key),
                L("SmartBpOcrDownloadStageRec"),
                stepIndex: 3,
                stepCount: 3,
                _downloadCts.Token);

            if (recModel.Version != ModelVersion.V5)
            {
                if (string.IsNullOrWhiteSpace(recModel.DictName))
                {
                    throw new InvalidOperationException(L("SmartBpOcrDictNameEmpty"));
                }

                var dicts = SharedUtils.LoadDicts(recModel.DictName);
                Directory.CreateDirectory(SmartBpOcrModelRegistry.GetModelDirectory(definition.Key));
                File.WriteAllLines(SmartBpOcrModelRegistry.GetRecDictPath(definition.Key), dicts, Encoding.UTF8);
            }

            DownloadProgress = 100;
            DownloadStatusText = L("SmartBpOcrDownloadCompleted");
            RaiseDownloadStateChanged();
        }
        catch (OperationCanceledException)
        {
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadCanceled");
            RaiseDownloadStateChanged();
            throw;
        }
        catch
        {
            CleanupModelDownloadResidue(definition.Key);
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadFailedSimple");
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
            errorMessage = Lf("SmartBpOcrUnsupportedModelFormat", modelKey);
            return false;
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                errorMessage = L("SmartBpOcrDeleteBlockedByDownloading");
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
            errorMessage = Lf("SmartBpOcrDeleteFailedFormat", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 基于 <see cref="IPaddleRuntimeState.ActiveBackend"/> 创建 Paddle 设备配置。
    /// 统一所有 PaddleOcrAll 创建路径的后端决策，避免分散硬编码 Mkldnn。
    /// </summary>
    /// <returns>Paddle 设备配置委托。</returns>
    private Action<PaddleConfig> CreatePaddleDevice()
    {
        return _runtimeState.ActiveBackend switch
        {
            OcrInferenceBackend.Cuda => PaddleDevice.Gpu(
                initialMemoryMB: 1024,
                deviceId: _runtimeState.ActiveCudaDeviceId),
            _ => PaddleDevice.Mkldnn()
        };
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
            errorMessage = Lf("SmartBpOcrUnsupportedModelFormat", modelKey);
            return false;
        }

        if (!SmartBpOcrModelRegistry.IsModelInstalled(modelKey))
        {
            errorMessage = L("SmartBpOcrModelFilesIncomplete");
            return false;
        }

        try
        {
            var fullModel = BuildLocalFullModel(modelKey, definition);
            var nextOcr = new PaddleOcrAll(fullModel, CreatePaddleDevice());

            lock (_ocrLock)
            {
                _ocr?.Dispose();
                _ocr = nextOcr;
                _ocr.AllowRotateDetection = false;
                _ocr.Enable180Classification = false;
            }

            _missingModelWarningShown = 0;
            CurrentOcrModelKey = modelKey;
            PersistCurrentModel(modelKey);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = Lf("SmartBpOcrLoadFailedFormat", ex.Message);
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

        // PaddleOCR 全流程更稳的是 8UC3(BGR)，现在先对其进行预处理
        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeTextCore(bgr);
        }

        return RecognizeTextCore(img);
    }

    /// <summary>
    /// 识别图像中的文本行和边界框。
    /// </summary>
    /// <param name="img">待识别图像。</param>
    /// <returns>文本行识别结果。</returns>
    public OcrTextBlockResult RecognizeTextLines(Mat img)
    {
        if (img.Empty()) return new([], string.Empty, "Paddle");

        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeTextLinesCore(bgr);
        }

        return RecognizeTextLinesCore(img);
    }

    /// <inheritdoc />
    OcrTextBlockResult IOcrProvider.RecognizeTextLines(Mat img, OcrRecognitionOptions? options) =>
        RecognizeTextLines(img);

    /// <summary>
    /// 执行 OCR 主流程，并在失败时尝试一次重建后重试。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>识别文本；失败返回 <see langword="null"/>。</returns>
    private string? RecognizeTextCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            var result = RunOcrWithRetryUnsafe(bgr);
            if (result != null)
                return string.IsNullOrWhiteSpace(result.Text) ? null : NormalizeOcrText(result.Text);
        }

        ShowMissingModelWarningOnce();
        return null;
    }

    /// <summary>
    /// 执行 OCR 文本行识别，并在失败时尝试一次重建后重试。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>文本行识别结果。</returns>
    private OcrTextBlockResult RecognizeTextLinesCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            var result = RunOcrWithRetryUnsafe(bgr);
            if (result != null)
                return ToTextBlockResult(result, bgr.Width, bgr.Height);
        }

        ShowMissingModelWarningOnce();
        return new([], string.Empty, "Paddle");
    }

    /// <summary>
    /// 在持锁状态下运行 OCR，并在失败后重建当前 predictor 重试一次。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>PaddleOCR 原始结果；失败或未就绪时返回 <see langword="null"/>。</returns>
    private PaddleOcrResult? RunOcrWithRetryUnsafe(Mat bgr)
    {
        if (_ocr is null)
            return null;

        try
        {
            return _ocr.Run(bgr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR run failed, trying to rebuild OCR predictor and retry once.");
            RecordCudaFailureIfNeeded(ex);
            if (!TryRebuildCurrentOcrUnsafe())
            {
                _logger.LogError("OCR rebuild failed, recognition aborted.");
                return null;
            }

            try
            {
                return _ocr!.Run(bgr);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "OCR retry failed after rebuild.");
                RecordCudaFailureIfNeeded(retryEx);
                return null;
            }
        }
    }

    /// <summary>
    /// 在 CUDA 后端下检测到明确的 CUDA runtime 故障时，记录故障信息并标记需要重启。
    /// 仅当 <see cref="IPaddleRuntimeState.ActiveBackend"/> 为 <see cref="OcrInferenceBackend.Cuda"/>
    /// （bootstrap 已通过 GPU Predictor probe）且异常明确指向 CUDA 库加载/初始化失败时才触发。
    /// 普通的 OCR 推理失败（模型问题、图像格式、内存不足、SEHException 等）不触发重启，
    /// 避免误判导致用户被反复要求重启。
    /// </summary>
    /// <param name="ex">捕获的异常。</param>
    private void RecordCudaFailureIfNeeded(Exception ex)
    {
        if (_runtimeState.ActiveBackend != OcrInferenceBackend.Cuda)
            return;

        if (!IsCudaRuntimeFailure(ex))
            return;

        _settingsHostService.Settings.LastCudaFailure = ex.Message;
        _settingsHostService.Settings.LastCudaFailureRuntimeVersion = AppConstants.PaddleInferenceRuntimeVersion;
        _ = _settingsHostService.SaveConfigAsync();
        _globalRestartService.IsRestartRequired = true;
        _logger.LogError(ex, "CUDA runtime failure detected, restart required.");
    }

    /// <summary>
    /// 判断异常是否明确指向 CUDA runtime 库加载或初始化失败。
    /// 仅匹配确定性的 CUDA 库缺失/加载失败关键字，不把 <see cref="System.Runtime.InteropServices.SEHException"/>
    /// 或泛义的 "cuda" 子串匹配当作 CUDA 故障——这些可能来自任何 native 崩溃或无关异常消息。
    /// </summary>
    /// <param name="ex">待判断的异常。</param>
    /// <returns>明确为 CUDA runtime 库故障返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsCudaRuntimeFailure(Exception ex)
    {
        var text = ex.GetType().Name + " " + (ex.Message ?? string.Empty);
        return text.Contains("cublas", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudnn", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudart", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudart64", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nvcuda", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unable to load", StringComparison.OrdinalIgnoreCase)
               && text.Contains("cuda", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 将 PaddleOCR 原始结果转换为稳定排序的文本块结果。
    /// </summary>
    /// <param name="result">PaddleOCR 原始结果。</param>
    /// <returns>文本块结果。</returns>
    private OcrTextBlockResult ToTextBlockResult(PaddleOcrResult result, int inputWidth, int inputHeight)
    {
        var lines = result.Regions
            .Select(region =>
            {
                var text = NormalizeOcrText(region.Text);
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                var originalBox = region.Rect.BoundingRect();
                var boundingBox = ClampToInput(originalBox, inputWidth, inputHeight);
                if (boundingBox != originalBox)
                    _logger.LogWarning(
                        "Paddle OCR returned an out-of-bounds box. input={Width}x{Height}; original={Original}; clamped={Clamped}",
                        inputWidth, inputHeight, originalBox, boundingBox);
                var centerX = boundingBox.X + boundingBox.Width / 2d;
                var centerY = boundingBox.Y + boundingBox.Height / 2d;
                _logger.LogDebug(
                    "provider=Paddle; input={Width}x{Height}; line bbox={Box}; center={CenterX:0.0},{CenterY:0.0}",
                    inputWidth, inputHeight, boundingBox, centerX, centerY);
                return new OcrTextLine(
                    text,
                    Math.Clamp(region.Score, 0, 1),
                    boundingBox,
                    centerX,
                    centerY,
                    "Paddle");
            })
            .Where(line => line != null)
            .Cast<OcrTextLine>()
            .OrderBy(line => line.CenterY)
            .ThenBy(line => line.CenterX)
            .ToArray();

        if (lines.Length == 0)
            return new([], string.Empty, "Paddle");

        return new OcrTextBlockResult(lines, string.Join(Environment.NewLine, lines.Select(line => line.Text)), "Paddle");
    }

    /// <summary>
    /// 将 OCR 返回的边界框裁剪到输入图像范围内。
    /// </summary>
    /// <param name="box">OCR 返回的原始边界框。</param>
    /// <param name="width">输入图像宽度。</param>
    /// <param name="height">输入图像高度。</param>
    /// <returns>裁剪后的边界框。</returns>
    private static Rect ClampToInput(Rect box, int width, int height)
    {
        var left = Math.Clamp(box.Left, 0, width);
        var top = Math.Clamp(box.Top, 0, height);
        var right = Math.Clamp(box.Right, left, width);
        var bottom = Math.Clamp(box.Bottom, top, height);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// 规范化 OCR 文本，便于本地规则解析。
    /// </summary>
    /// <param name="text">OCR 原始文本。</param>
    /// <returns>规范化文本。</returns>
    private static string NormalizeOcrText(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Normalize(NormalizationForm.FormKC).Trim();

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
            var rebuilt = new PaddleOcrAll(fullModel, CreatePaddleDevice())
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
            sourceUrl,
            destinationFilePath,
            cancellationToken,
            args => Downloader_DownloadProgressChanged(this, args));
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
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">下载进度参数。</param>
    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        var stepProgress = e.ProgressPercentage / 100.0;
        var overallProgress = ((_currentDownloadStep - 1) + stepProgress) / _totalDownloadSteps * 100;

        DownloadProgress = overallProgress;
        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 触发下载状态变化事件。
    /// </summary>
    private void RaiseDownloadStateChanged() => DownloadStateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 尝试加载用户设置中的首选 OCR 模型。
    /// </summary>
    private void TryLoadPreferredModel()
    {
        string? preferredModel = _settingsHostService.Settings.OcrModelKey;

        if (string.IsNullOrWhiteSpace(preferredModel))
            return;

        _isModelLoading = true;
        RaiseModelLoadStateChanged();
        try
        {
            _ = TrySwitchOcrModel(preferredModel, out _);
        }
        finally
        {
            _isModelLoading = false;
            RaiseModelLoadStateChanged();
        }
    }

    /// <summary>
    /// 仅在首次检测到 OCR 未就绪时弹出一次提示。
    /// </summary>
    private void ShowMissingModelWarningOnce()
    {
        if (Interlocked.Exchange(ref _missingModelWarningShown, 1) == 1)
            return;

        _ = MessageBoxHelper.ShowErrorAsync(L("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
    }

    /// <summary>
    /// 持久化当前模型键到配置。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    private void PersistCurrentModel(string modelKey)
    {
        _settingsHostService.Settings.OcrModelKey = modelKey;
        _ = _settingsHostService.SaveConfigAsync();
    }

    /// <summary>
    /// 基于本地模型目录构建完整 OCR 模型对象。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="definition">模型定义。</param>
    /// <returns>可用于推理的完整 OCR 模型。</returns>
    private FullOcrModel BuildLocalFullModel(string modelKey, SmartBpOcrModelDefinition definition)
    {
        if (definition.DetModel == null)
        {
            _logger.LogError("Detection model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrDetModelMetadataEmpty"));
        }
        var onlineDet = definition.DetModel;

        if (definition.ClsModel == null)
        {
            _logger.LogError("Classification model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrClsModelMetadataEmpty"));
        }
        var onlineCls = definition.ClsModel;

        if (definition.RecModel == null)
        {
            _logger.LogError("Recognition model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrRecModelMetadataEmpty"));
        }
        var onlineRec = definition.RecModel;

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

    /// <summary>
    /// 读取 SmartBP 模块本地化文本。
    /// </summary>
    /// <param name="key">资源 key。</param>
    /// <returns>本地化文本。</returns>
    private static string L(string key) => I18nHelper.GetLocalizedString(key);

    /// <summary>
    /// 读取并格式化 SmartBP 模块本地化文本。
    /// </summary>
    /// <param name="key">资源 key。</param>
    /// <param name="args">格式化参数。</param>
    /// <returns>格式化后的本地化文本。</returns>
    private static string Lf(string key, params object?[] args) =>
        string.Format(I18nHelper.GetLocalizedString(key), args);

    /// <summary>
    /// 从 PaddleOCR 模型元数据中选择下载地址。
    /// </summary>
    /// <param name="onlineModel">Sdcb 模型元数据对象。</param>
    /// <param name="errorMessage">元数据缺失时抛出的本地化错误。</param>
    /// <returns>模型下载地址。</returns>
    /// <exception cref="InvalidOperationException">模型元数据缺少可用下载地址时抛出。</exception>
    private Uri PickModelSourceUri(object? onlineModel, string errorMessage)
    {
        if (onlineModel is null)
        {
            _logger.LogError("PickModelSourceUri: onlineModel is null. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var modelType = onlineModel.GetType();

        // 兼容旧版：Uri
        var legacyUriValue = modelType.GetProperty("Uri")?.GetValue(onlineModel);
        if (legacyUriValue is Uri legacyUri)
        {
            return legacyUri;
        }

        if (legacyUriValue is string legacyUrl &&
            Uri.TryCreate(legacyUrl, UriKind.Absolute, out var parsedLegacyUri))
        {
            return parsedLegacyUri;
        }

        // 新版：Sources
        var sourcesValue = modelType.GetProperty("Sources")?.GetValue(onlineModel);
        if (sourcesValue is not IEnumerable sources)
        {
            _logger.LogError("PickModelSourceUri: Sources property is not IEnumerable. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var candidates = sources
            .Cast<object>()
            .Select(source =>
            {
                var sourceType = source.GetType();

                var uriValue =
                    sourceType.GetProperty("ArchiveUri")?.GetValue(source) ??
                    sourceType.GetProperty("Uri")?.GetValue(source) ??
                    sourceType.GetProperty("Url")?.GetValue(source);

                if (uriValue is Uri uri)
                {
                    return uri;
                }

                if (uriValue is string url &&
                    Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
                {
                    return parsedUri;
                }

                var description = sourceType.GetProperty("Description")?.GetValue(source)?.ToString();

                return Uri.TryCreate(description, UriKind.Absolute, out var descriptionUri)
                    ? descriptionUri
                    : null;
            })
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .ToList();

        var selected = candidates
            .Where(uri => uri.AbsoluteUri.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(uri => uri.Host.Contains("bcebos.com", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (selected == null)
        {
            _logger.LogError("PickModelSourceUri: no valid source URI found. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return selected;
    }
}
