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
/// OCR 服务实现。
/// 提供模型管理（枚举、下载、删除、切换）与图像文本识别能力。
/// </summary>
public sealed partial class PaddleOcrProvider : IOcrProvider
{
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<PaddleOcrProvider> _logger;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly IPaddleRuntimeState _runtimeState;
    private readonly IPaddleRuntimeManifestProvider _runtimeManifestProvider;
    private readonly IGlobalRestartService _globalRestartService;
    private readonly IFileDownloadService _fileDownloadService;
    private readonly Lock _ocrLock = new();
    private readonly Lock _downloadLock = new();

    private PaddleOcrAll? _ocr;
    private CancellationTokenSource? _downloadCts;
    private IFileDownloadOperation? _currentDownload;
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
    /// 当前模型下载是否已暂停。
    /// </summary>
    public bool IsDownloadPaused => _currentDownload?.State == FileDownloadState.Paused;

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
    /// <param name="debugLog">SmartBP 统一识别调试日志。</param>
    /// <param name="modelPathProvider">OCR 模型路径提供器。</param>
    /// <param name="runtimeState">Paddle runtime 运行时状态，用于决定推理后端（CPU/CUDA）。</param>
    /// <param name="runtimeManifestProvider">Paddle runtime manifest 提供者，用于记录当前模块 runtime 版本。</param>
    /// <param name="globalRestartService">全局重启服务，用于在 CUDA 故障时标记需要重启。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    public PaddleOcrProvider(
        ISettingsHostService settingsHostService,
        ILogger<PaddleOcrProvider> logger,
        ISmartBpDebugLog debugLog,
        ISmartBpOcrModelPathProvider modelPathProvider,
        IPaddleRuntimeState runtimeState,
        IPaddleRuntimeManifestProvider runtimeManifestProvider,
        IGlobalRestartService globalRestartService,
        IFileDownloadService fileDownloadService)
    {
        _settingsHostService = settingsHostService;
        _logger = logger;
        _debugLog = debugLog;
        _runtimeState = runtimeState;
        _runtimeManifestProvider = runtimeManifestProvider;
        _globalRestartService = globalRestartService;
        _fileDownloadService = fileDownloadService;
        SmartBpOcrModelRegistry.ConfigurePathProvider(modelPathProvider);
    }
}
