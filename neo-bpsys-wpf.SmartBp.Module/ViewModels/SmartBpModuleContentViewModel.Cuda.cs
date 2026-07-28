using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// SmartBP 模块 CUDA 硬件加速设置相关的视图模型逻辑（partial）。
/// 流程：检测 NVIDIA GPU → （未安装时）下载 Paddle runtime 与 NVIDIA 可再发行依赖 → 启用开关 →
/// 利用全局重启服务提示用户重启以应用硬件加速。
/// </summary>
public partial class SmartBpModuleContentViewModel
{
    /// <summary>当前选中的 CUDA 设备；无设备或未受支持时为 <see langword="null"/>。</summary>
    private CudaDeviceInfo? _selectedCudaDevice;

    /// <summary>当前匹配到的 CUDA runtime 包；未匹配时为 <see langword="null"/>。</summary>
    private PaddleRuntimePackageInfo? _selectedCudaPackage;

    /// <summary>CUDA runtime 下载取消令牌源；无进行中下载时为 <see langword="null"/>。</summary>
    private CancellationTokenSource? _cudaDownloadCts;

    /// <summary>是否正在执行 CUDA 开关切换流程，用于防止命令重复触发。</summary>
    private bool _isCudaToggling;

    /// <summary>
    /// 当前 <see cref="CudaUnsupportedReason"/> 对应的资源键；无提示时为 <see langword="null"/>。
    /// 语言切换后通过 <see cref="RefreshCudaUnsupportedReason"/> 重新解析，避免提示文本滞留旧语言。
    /// </summary>
    private string? _cudaUnsupportedReasonKey;

    /// <summary>
    /// 当前 <see cref="CudaDownloadStageText"/> 对应的资源键；无进行中操作时为 <see langword="null"/>。
    /// </summary>
    private string? _cudaDownloadStageKey;

    /// <summary>
    /// 最近一次 CUDA 安装失败的原始错误详情；无错误时为 <see langword="null"/>。
    /// </summary>
    private string? _cudaInstallationErrorDetail;

    /// <summary>
    /// 获取或设置是否检测到至少一张 NVIDIA CUDA 设备。
    /// </summary>
    [ObservableProperty]
    public partial bool IsCudaAvailable { get; set; }

    /// <summary>
    /// 获取或设置当前检测到的 CUDA 设备是否受 PaddleInference 支持。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleCuda))]
    [NotifyPropertyChangedFor(nameof(CanDownloadCudaDependency))]
    public partial bool IsCudaSupported { get; set; }

    /// <summary>
    /// 获取或设置检测到的 CUDA 显卡名称。
    /// </summary>
    [ObservableProperty]
    public partial string CudaDeviceName { get; set; } = "";

    /// <summary>
    /// 获取或设置 CUDA 加速卡片当前状态文本（已本地化）。
    /// </summary>
    [ObservableProperty]
    public partial string CudaStatusText { get; set; } = "";

    /// <summary>
    /// 获取或设置 CUDA 加速开关当前状态（用户偏好）。
    /// </summary>
    [ObservableProperty]
    public partial bool IsCudaEnabled { get; set; }

    /// <summary>
    /// 获取或设置是否正在下载 CUDA runtime 组件。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleCuda))]
    [NotifyPropertyChangedFor(nameof(CanDownloadCudaDependency))]
    public partial bool IsCudaDownloading { get; set; }

    /// <summary>
    /// 获取或设置 CUDA runtime 组件下载进度（0-100）。
    /// </summary>
    [ObservableProperty]
    public partial double CudaDownloadProgress { get; set; }

    /// <summary>
    /// 获取或设置 CUDA runtime 组件下载进度文本（如 <c>50.00%</c>）。
    /// </summary>
    [ObservableProperty]
    public partial string CudaDownloadProgressText { get; set; } = "";

    /// <summary>
    /// 获取或设置 CUDA runtime 组件下载速度文本（如 <c>1.23 MB/s</c>）。
    /// </summary>
    [ObservableProperty]
    public partial string CudaDownloadSpeedText { get; set; } = "";

    /// <summary>
    /// 获取或设置 CUDA 依赖下载、校验或安装阶段的本地化状态文本。
    /// </summary>
    [ObservableProperty]
    public partial string CudaDownloadStageText { get; set; } = "";

    /// <summary>
    /// 获取或设置当前 CUDA 依赖操作是否具有可量化的网络传输进度。
    /// </summary>
    [ObservableProperty]
    public partial bool IsCudaTransferActive { get; set; }

    /// <summary>
    /// 获取或设置最近一次 CUDA 安装失败的常驻本地化错误文本；无错误时为 <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    public partial string? CudaInstallationErrorText { get; set; }

    /// <summary>
    /// 获取或设置是否需要重启应用以应用 CUDA 后端切换。
    /// </summary>
    [ObservableProperty]
    public partial bool IsCudaRestartRequired { get; set; }

    /// <summary>
    /// 获取或设置 CUDA 操作提示（如下载失败原因、需先下载依赖等，已本地化）；无提示时为 <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    public partial string? CudaUnsupportedReason { get; set; }

    /// <summary>
    /// 获取或设置 CUDA 加速卡片是否可见（无 NVIDIA GPU 时为 <see langword="false"/>）。
    /// </summary>
    [ObservableProperty]
    public partial bool IsCudaCardVisible { get; set; }

    /// <summary>
    /// 获取或设置 CUDA runtime 依赖是否已安装就绪。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleCuda))]
    [NotifyPropertyChangedFor(nameof(CanDownloadCudaDependency))]
    public partial bool IsCudaDependencyInstalled { get; set; }

    /// <summary>
    /// 获取 CUDA 加速开关是否可交互：仅在 GPU 受支持、依赖已安装且无进行中下载时允许切换。
    /// </summary>
    public bool CanToggleCuda => IsCudaSupported && IsCudaDependencyInstalled && !IsCudaDownloading;

    /// <summary>
    /// 获取"下载 CUDA 依赖"按钮是否可点击：仅在 GPU 受支持、依赖未安装且无进行中下载时允许。
    /// </summary>
    public bool CanDownloadCudaDependency => IsCudaSupported && !IsCudaDependencyInstalled && !IsCudaDownloading;

    /// <summary>
    /// 初始化 CUDA 加速设置卡片：检测设备、解析匹配包、查询安装状态、订阅运行时与下载事件、刷新状态。
    /// 在主构造函数中调用；设计时无参构造函数不会调用此方法。
    /// </summary>
    private void InitializeCuda()
    {
        var devices = _cudaDeviceDetector.DetectDevices();
        IsCudaAvailable = devices.Count > 0;
        IsCudaCardVisible = devices.Count > 0;
        _selectedCudaDevice = devices.FirstOrDefault(d => d.IsSupported) ?? devices.FirstOrDefault();
        IsCudaSupported = _selectedCudaDevice is { IsSupported: true };
        CudaDeviceName = _selectedCudaDevice?.DeviceName ?? "";
        SetCudaUnsupportedReason(IsCudaSupported ? null : "CudaStatusUnavailable");

        ResolveSelectedCudaPackage();

        IsCudaDependencyInstalled = IsCudaDependencyReady();
        // 开关状态仅在依赖已安装时才反映用户偏好；未安装时强制显示关闭，等待用户下载依赖。
        IsCudaEnabled = IsCudaDependencyInstalled
            && _settingsHostService.Settings.PreferredOcrBackend == OcrInferenceBackend.Cuda;
        IsCudaRestartRequired = _paddleRuntimeState.RestartRequired || _globalRestartService.IsRestartRequired;

        RefreshCudaStatus();

        _paddleRuntimeState.StateChanged += (_, _) => BeginOnUiThread(RefreshCudaStatus);
        _paddleRuntimeComponentService.DownloadStateChanged += (_, _) => BeginOnUiThread(OnCudaDownloadStateChanged);
        _paddleCudaPrerequisiteSetupService.StatusChanged += (_, _) => BeginOnUiThread(OnCudaPrerequisiteStateChanged);
        _globalRestartService.RestartRequiredStateChanged += (_, _) => BeginOnUiThread(OnCudaRestartRequiredChanged);
    }

    /// <summary>
    /// 根据当前选中设备的 Compute Capability 解析匹配的 CUDA runtime 包。
    /// </summary>
    private void ResolveSelectedCudaPackage()
    {
        if (_selectedCudaDevice is null)
        {
            _selectedCudaPackage = null;
            return;
        }

        _selectedCudaPackage = _paddleRuntimeManifestProvider.ResolveByComputeCapability(
            _selectedCudaDevice.ComputeCapabilityMajor,
            _selectedCudaDevice.ComputeCapabilityMinor);
    }

    /// <summary>
    /// CUDA 加速开关切换命令。仅在依赖已安装时可用，切换后通过全局重启服务提示重启。
    /// </summary>
    /// <returns>切换流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ToggleCudaAsync()
    {
        if (_isCudaToggling)
            return;

        _isCudaToggling = true;
        try
        {
            if (IsCudaEnabled)
                await EnableCudaCoreAsync();
            else
                await DisableCudaCoreAsync();
        }
        finally
        {
            _isCudaToggling = false;
        }
    }

    /// <summary>
    /// 下载并安装 CUDA runtime 与 NVIDIA 可再发行依赖。仅在依赖未安装时可用。
    /// </summary>
    /// <returns>下载、校验和安装完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanDownloadCudaDependency))]
    private async Task DownloadCudaDependencyAsync()
    {
        if (!IsCudaSupported || _selectedCudaPackage is null)
        {
            SetCudaUnsupportedReason("CudaInstallFailed");
            return;
        }

        // 清除上一次下载失败提示，开始新一轮下载。
        SetCudaUnsupportedReason(null);
        SetCudaInstallationError(null);

        _cudaDownloadCts = new CancellationTokenSource();
        IsCudaDownloading = true;
        CudaDownloadProgress = 0;
        CudaDownloadProgressText = "";
        CudaDownloadSpeedText = "";
        SetCudaDownloadStage("CudaStagePreparing", false);
        try
        {
            if (_paddleRuntimeComponentService.GetInstallStatus().Status != PaddleRuntimeInstallStatus.Installed)
            {
                await _paddleRuntimeComponentService.DownloadAsync(_selectedCudaPackage, _cudaDownloadCts.Token);
                await WaitForPaddleRuntimeInstallAsync(_cudaDownloadCts.Token);
                if (_paddleRuntimeComponentService.LastInstallSucceeded != true)
                {
                    SetCudaUnsupportedReason("CudaStatusDownloadFailed");
                    return;
                }
            }

            await _paddleCudaPrerequisiteSetupService.InstallAsync(_selectedCudaPackage, _cudaDownloadCts.Token);
            if (_paddleCudaPrerequisiteSetupService.Status.Status != PaddleCudaPrerequisiteInstallStatus.Installed)
            {
                SetCudaUnsupportedReason("CudaStatusDownloadFailed");
                SetCudaInstallationError(_paddleCudaPrerequisiteSetupService.Status.ErrorMessage);
            }
            else
            {
                SetCudaUnsupportedReason(null);
                SetCudaInstallationError(null);
            }
        }
        catch (OperationCanceledException)
        {
            SetCudaUnsupportedReason("CudaInstallCancelled");
            SetCudaInstallationError(null);
        }
        catch (InvalidOperationException ex)
        {
            SetCudaInstallationError(ex.Message);
            RefreshCudaStatus();
        }
        finally
        {
            _cudaDownloadCts?.Dispose();
            _cudaDownloadCts = null;
            IsCudaDownloading = false;
            CudaDownloadProgress = 0;
            CudaDownloadProgressText = "";
            CudaDownloadSpeedText = "";
            SetCudaDownloadStage(null, false);
            RefreshCudaStatus();
        }
    }

    /// <summary>
    /// 取消正在进行的 CUDA runtime 组件下载。
    /// </summary>
    [RelayCommand]
    private void CancelCudaDownload()
    {
        _cudaDownloadCts?.Cancel();
        _paddleRuntimeComponentService.CancelDownload();
    }

    /// <summary>
    /// 开启 CUDA 加速流程：仅在依赖已安装时调用，持久化偏好并标记需要重启。
    /// </summary>
    /// <returns>开启流程完成后的任务。</returns>
    private Task EnableCudaCoreAsync()
    {
        if (!IsCudaSupported || !IsCudaDependencyInstalled || _selectedCudaDevice is null)
        {
            IsCudaEnabled = false;
            return Task.CompletedTask;
        }

        ApplyCudaPreference();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 应用 CUDA 偏好：持久化后端偏好与设备 ID、清除历史故障标记（含一次性 CPU 保护）、
    /// 通过全局重启服务标记需要重启。
    /// </summary>
    private void ApplyCudaPreference()
    {
        if (_selectedCudaDevice is null)
            return;

        _settingsHostService.Settings.PreferredOcrBackend = OcrInferenceBackend.Cuda;
        _settingsHostService.Settings.PreferredCudaDeviceId = _selectedCudaDevice.DeviceId;
        // 用户主动启用 CUDA 时，清除所有历史故障标记，允许下次启动重新尝试 CUDA。
        if (!string.IsNullOrWhiteSpace(_settingsHostService.Settings.LastCudaFailure))
        {
            _settingsHostService.Settings.LastCudaFailure = null;
            _settingsHostService.Settings.LastCudaFailureRuntimeVersion = null;
        }
        _settingsHostService.Settings.ForceCpuForNextLaunch = false;
        _ = _settingsHostService.SaveConfigAsync();
        _globalRestartService.IsRestartRequired = true;
        IsCudaRestartRequired = true;
        RefreshCudaStatus();
    }

    /// <summary>
    /// 关闭 CUDA 加速流程：将偏好切换回 CPU、持久化并通过全局重启服务标记需要重启。
    /// </summary>
    /// <returns>关闭流程完成后的任务。</returns>
    private async Task DisableCudaCoreAsync()
    {
        _settingsHostService.Settings.PreferredOcrBackend = OcrInferenceBackend.Cpu;
        await _settingsHostService.SaveConfigAsync();
        _globalRestartService.IsRestartRequired = true;
        IsCudaRestartRequired = true;
        RefreshCudaStatus();
    }

    /// <summary>
    /// 根据运行时后端、下载状态、安装状态和错误信息刷新 <see cref="CudaStatusText"/>，
    /// 并同步 <see cref="IsCudaDependencyInstalled"/>。
    /// </summary>
    private void RefreshCudaStatus()
    {
        var runtimeState = _paddleRuntimeState;
        var componentService = _paddleRuntimeComponentService;
        var installInfo = componentService.GetInstallStatus();

        IsCudaDependencyInstalled = IsCudaDependencyReady();

        // 当前选中的 OCR 引擎（Paddle / Rapid / Tesseract）。CUDA 只服务于 PaddleOCR，
        // 其他引擎下即使 CUDA runtime 已就绪也不会被使用。
        var currentProvider = _recognitionSettingsService.Settings.SelectedOcrProviderMode;

        string statusKey;
        if (runtimeState.ActiveBackend == OcrInferenceBackend.Cuda && runtimeState.RuntimeLoadError is null)
        {
            if (currentProvider != SmartBpOcrProviderMode.Paddle)
            {
                // CUDA runtime 已加载，但当前 OCR 引擎不是 PaddleOCR，GPU 不会被使用。
                statusKey = "CudaStatusReadyButNotPaddle";
            }
            else if (runtimeState.PaddleBackendVerified)
            {
                // 真实 PaddleOcrAll 已在 GPU 上成功构造，确认正在使用 CUDA。
                statusKey = "CudaStatusEnabled";
            }
            else
            {
                // runtime DLL 已加载且 probe 通过，但真实 OCR 模型尚未在 GPU 上验证。
                statusKey = "CudaStatusPendingVerify";
            }
        }
        else if (runtimeState.ActiveBackend == OcrInferenceBackend.Cpu
                 && _settingsHostService.Settings.PreferredOcrBackend == OcrInferenceBackend.Cuda
                 && IsCudaRestartRequired)
        {
            statusKey = "CudaStatusRestartRequired";
        }
        else if (IsCudaDownloading)
        {
            statusKey = "CudaStatusDownloading";
        }
        else if (installInfo.Status == PaddleRuntimeInstallStatus.Installed
                 && _paddleCudaPrerequisiteSetupService.Status.Status != PaddleCudaPrerequisiteInstallStatus.Installed)
        {
            statusKey = "CudaStatusPrerequisitesMissing";
        }
        else if (installInfo.Status == PaddleRuntimeInstallStatus.NotInstalled
                 && _paddleCudaPrerequisiteSetupService.Status.Status == PaddleCudaPrerequisiteInstallStatus.Installed)
        {
            statusKey = "CudaStatusModuleRuntimeMissing";
        }
        else if (installInfo.Status == PaddleRuntimeInstallStatus.Installed && !IsCudaEnabled)
        {
            statusKey = "CudaStatusDependencyReady";
        }
        else if (installInfo.Status == PaddleRuntimeInstallStatus.VersionMismatch)
        {
            statusKey = "CudaStatusVersionMismatch";
        }
        else if (installInfo.Status == PaddleRuntimeInstallStatus.NotInstalled && IsCudaSupported)
        {
            statusKey = "CudaStatusSupportedNotInstalled";
        }
        else if (runtimeState.RuntimeLoadError is not null)
        {
            statusKey = "CudaStatusFailedFallback";
        }
        else if (!IsCudaSupported)
        {
            statusKey = "CudaStatusUnavailable";
        }
        else
        {
            statusKey = "CudaStatusUsingCpu";
        }

        CudaStatusText = ResolveLocalizedOrRaw(statusKey);
    }

    /// <summary>
    /// 统一设置 <see cref="CudaUnsupportedReason"/> 的资源键与解析后的文本。
    /// 所有提示赋值必须走此方法，以保证 <see cref="_cudaUnsupportedReasonKey"/> 与显示文本同步，
    /// 语言切换后 <see cref="RefreshCudaUnsupportedReason"/> 才能正确重新本地化。
    /// </summary>
    /// <param name="key">资源键；传 <see langword="null"/> 清除提示。</param>
    private void SetCudaUnsupportedReason(string? key)
    {
        _cudaUnsupportedReasonKey = key;
        CudaUnsupportedReason = key is null ? null : ResolveLocalizedOrRaw(key);
    }

    /// <summary>
    /// 语言切换后根据 <see cref="_cudaUnsupportedReasonKey"/> 重新解析 <see cref="CudaUnsupportedReason"/>。
    /// 由 <see cref="RefreshLocalizedState"/> 调用，避免 CUDA 提示文本在切换语言后滞留旧语言。
    /// </summary>
    private void RefreshCudaUnsupportedReason()
    {
        if (_cudaUnsupportedReasonKey is null)
            return;

        CudaUnsupportedReason = ResolveLocalizedOrRaw(_cudaUnsupportedReasonKey);
    }

    /// <summary>
    /// 设置 CUDA 依赖操作的阶段资源键、显示文本以及是否展示可量化下载进度。
    /// </summary>
    /// <param name="key">本地化资源键；传 <see langword="null"/> 清除阶段文本。</param>
    /// <param name="isTransferActive">当前阶段是否正在执行可量化的网络传输。</param>
    private void SetCudaDownloadStage(string? key, bool isTransferActive)
    {
        _cudaDownloadStageKey = key;
        CudaDownloadStageText = key is null ? "" : ResolveLocalizedOrRaw(key);
        IsCudaTransferActive = isTransferActive;
    }

    /// <summary>
    /// 语言切换后重新解析当前 CUDA 依赖操作的阶段文本。
    /// </summary>
    private void RefreshCudaDownloadStageText()
    {
        if (_cudaDownloadStageKey is null)
            return;

        CudaDownloadStageText = ResolveLocalizedOrRaw(_cudaDownloadStageKey);
    }

    /// <summary>
    /// 设置最近一次 CUDA 安装错误详情，并生成独立于操作区生命周期的常驻错误文本。
    /// </summary>
    /// <param name="detail">原始错误详情；传 <see langword="null"/> 清除错误。</param>
    private void SetCudaInstallationError(string? detail)
    {
        _cudaInstallationErrorDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        CudaInstallationErrorText = _cudaInstallationErrorDetail is null
            ? null
            : string.Format(ResolveLocalizedOrRaw("CudaInstallErrorDetails"), _cudaInstallationErrorDetail);
    }

    /// <summary>
    /// 语言切换后重新生成最近一次 CUDA 安装错误的本地化文本。
    /// </summary>
    private void RefreshCudaInstallationErrorText()
    {
        if (_cudaInstallationErrorDetail is not null)
            SetCudaInstallationError(_cudaInstallationErrorDetail);
    }

    /// <summary>
    /// 处理 Paddle runtime 下载状态变化，并同步下载进度展示。
    /// </summary>
    private void OnCudaDownloadStateChanged()
    {
        if (_paddleRuntimeComponentService.IsDownloading && IsCudaDownloading)
            SetCudaDownloadStage("CudaStageDownloadingPaddleRuntime", true);

        var progress = _paddleRuntimeComponentService.DownloadProgress ?? 0;
        var speed = _paddleRuntimeComponentService.DownloadSpeed ?? 0;
        CudaDownloadProgress = progress * 0.1;
        CudaDownloadProgressText = IsCudaDownloading ? $"{CudaDownloadProgress:0.00}%" : "";
        CudaDownloadSpeedText = IsCudaDownloading ? $"{speed / 1024 / 1024:0.00} MB/s" : "";
    }

    /// <summary>
    /// 处理 NVIDIA CUDA/cuDNN 可再发行依赖状态变化，并同步总体下载进度展示。
    /// </summary>
    private void OnCudaPrerequisiteStateChanged()
    {
        var status = _paddleCudaPrerequisiteSetupService.Status;
        if (status.IsBusy)
        {
            var (stageKey, isTransferActive) = status.CurrentStep switch
            {
                "DownloadingCudaToolkit" => ("CudaStageDownloadingCudaToolkit", true),
                "VerifyingCudaToolkit" => ("CudaStageVerifyingCudaToolkit", false),
                "InstallingCudaToolkit" => ("CudaStageInstallingCudaToolkit", false),
                "DownloadingCuDnn" => ("CudaStageDownloadingCuDnn", true),
                "VerifyingCuDnn" => ("CudaStageVerifyingCuDnn", false),
                "InstallingCuDnn" => ("CudaStageInstallingCuDnn", false),
                _ => ("CudaStagePreparing", false),
            };
            SetCudaDownloadStage(stageKey, isTransferActive);

            var runtimeAlreadyInstalled = _paddleRuntimeComponentService.GetInstallStatus().Status == PaddleRuntimeInstallStatus.Installed;
            CudaDownloadProgress = runtimeAlreadyInstalled
                ? status.DownloadProgress
                : 10 + status.DownloadProgress * 0.9;
            CudaDownloadProgressText = isTransferActive ? $"{CudaDownloadProgress:0.00}%" : "";
            CudaDownloadSpeedText = isTransferActive && status.DownloadSpeed is > 0
                ? $"{status.DownloadSpeed.Value / 1024 / 1024:0.00} MB/s"
                : "";
        }
        else if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
        {
            SetCudaInstallationError(status.ErrorMessage);
        }

        RefreshCudaStatus();
    }

    /// <summary>
    /// 等待 fire-and-forget Paddle CUDA runtime 组件下载完成。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>下载与安装完成后的任务。</returns>
    private Task WaitForPaddleRuntimeInstallAsync(CancellationToken cancellationToken)
    {
        if (!_paddleRuntimeComponentService.IsDownloading)
            return Task.CompletedTask;

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (!_paddleRuntimeComponentService.IsDownloading)
                completionSource.TrySetResult();
        };
        _paddleRuntimeComponentService.DownloadStateChanged += handler;
        if (!_paddleRuntimeComponentService.IsDownloading)
            completionSource.TrySetResult();

        return WaitAndUnsubscribeAsync(completionSource.Task, handler, cancellationToken);
    }

    /// <summary>
    /// 等待指定任务完成并解除 Paddle runtime 下载状态事件订阅。
    /// </summary>
    /// <param name="task">要等待的任务。</param>
    /// <param name="handler">要解除的事件处理器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>等待任务。</returns>
    private async Task WaitAndUnsubscribeAsync(Task task, EventHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken);
        }
        finally
        {
            _paddleRuntimeComponentService.DownloadStateChanged -= handler;
        }
    }

    /// <summary>
    /// 判断 Paddle CUDA native runtime 与 NVIDIA 可再发行依赖是否均已就绪。
    /// </summary>
    /// <returns>两个依赖均已安装时返回 <see langword="true"/>。</returns>
    private bool IsCudaDependencyReady()
        => _paddleRuntimeComponentService.GetInstallStatus().Status == PaddleRuntimeInstallStatus.Installed
           && _paddleCudaPrerequisiteSetupService.Status.Status == PaddleCudaPrerequisiteInstallStatus.Installed;

    /// <summary>
    /// 处理全局重启需求变化：同步 <see cref="IsCudaRestartRequired"/> 并刷新状态文本。
    /// </summary>
    private void OnCudaRestartRequiredChanged()
    {
        IsCudaRestartRequired = _globalRestartService.IsRestartRequired || _paddleRuntimeState.RestartRequired;
        RefreshCudaStatus();
    }

    /// <summary>
    /// 当 <see cref="IsCudaDownloading"/> 变化时刷新状态文本。
    /// </summary>
    /// <param name="value">新的下载状态。</param>
    partial void OnIsCudaDownloadingChanged(bool value)
    {
        RefreshCudaStatus();
    }
}
