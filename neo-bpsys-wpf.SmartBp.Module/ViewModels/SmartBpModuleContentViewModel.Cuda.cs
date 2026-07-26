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
/// 流程：检测 NVIDIA GPU → （未安装时）显示"下载依赖"按钮 → 用户下载并安装 → 启用开关 →
/// 利用全局重启服务提示用户重启以应用硬件加速。下载流程参照软件更新
/// <c>UpdaterService</c>：fire-and-forget 启动，通过 <see cref="IPaddleRuntimeComponentService.DownloadStateChanged"/>
/// 事件 + <see cref="IsCudaDownloading"/>/<see cref="IsCudaDependencyInstalled"/> 等属性同步 UI。
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

        IsCudaDependencyInstalled =
            _paddleRuntimeComponentService.GetInstallStatus().Status == PaddleRuntimeInstallStatus.Installed;
        // 开关状态仅在依赖已安装时才反映用户偏好；未安装时强制显示关闭，等待用户下载依赖。
        IsCudaEnabled = IsCudaDependencyInstalled
            && _settingsHostService.Settings.PreferredOcrBackend == OcrInferenceBackend.Cuda;
        IsCudaRestartRequired = _paddleRuntimeState.RestartRequired || _globalRestartService.IsRestartRequired;

        RefreshCudaStatus();

        _paddleRuntimeState.StateChanged += (_, _) => BeginOnUiThread(RefreshCudaStatus);
        _paddleRuntimeComponentService.DownloadStateChanged += (_, _) => BeginOnUiThread(OnCudaDownloadStateChanged);
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
    /// 下载 CUDA runtime 依赖命令（fire-and-forget）。仅在依赖未安装时可用。
    /// 参照软件更新 <c>UpdaterService.DownloadUpdate</c>：不 <see langword="await"/> 下载完成，
    /// 进度与结果通过 <see cref="OnCudaDownloadStateChanged"/> 同步。
    /// </summary>
    /// <returns>启动下载后的任务（不代表下载完成）。</returns>
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

        _cudaDownloadCts = new CancellationTokenSource();
        IsCudaDownloading = true;
        CudaDownloadProgress = 0;
        CudaDownloadProgressText = "";
        CudaDownloadSpeedText = "";
        try
        {
            // 参照软件更新：DownloadAsync 返回 Task.CompletedTask（fire-and-forget）。
            // 下载完成由 DownloadStateChanged 事件 → OnCudaDownloadStateChanged 处理。
            await _paddleRuntimeComponentService.DownloadAsync(_selectedCudaPackage, _cudaDownloadCts.Token);
        }
        catch (InvalidOperationException)
        {
            // 已有下载进行中，同步状态即可。
            _cudaDownloadCts?.Dispose();
            _cudaDownloadCts = null;
            IsCudaDownloading = _paddleRuntimeComponentService.IsDownloading;
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

        IsCudaDependencyInstalled = installInfo.Status == PaddleRuntimeInstallStatus.Installed;

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
        else if (componentService.IsDownloading)
        {
            statusKey = "CudaStatusDownloading";
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
    /// 处理 CUDA runtime 组件下载状态变化：同步进度/速度文本，并在下载结束时更新安装状态与提示。
    /// 参照软件更新 <c>RefreshUpdateDownloadState</c>：下载进行中只做属性赋值，
    /// <b>不调用</b> <see cref="RefreshCudaStatus"/>（后者会通过 <see cref="GetInstallStatus"/>
    /// 遍历文件系统，高频进度事件下会淹没 UI 线程导致卡死）。下载中状态文本固定为"下载中"，
    /// 由 <see cref="OnIsCudaDownloadingChanged"/> 在 <see cref="IsCudaDownloading"/> 翻转时刷新一次。
    /// </summary>
    private void OnCudaDownloadStateChanged()
    {
        var wasDownloading = IsCudaDownloading;
        IsCudaDownloading = _paddleRuntimeComponentService.IsDownloading;
        var progress = _paddleRuntimeComponentService.DownloadProgress ?? 0;
        var speed = _paddleRuntimeComponentService.DownloadSpeed ?? 0;
        CudaDownloadProgress = progress;
        CudaDownloadProgressText = IsCudaDownloading ? $"{progress:0.00}%" : "";
        CudaDownloadSpeedText = IsCudaDownloading ? $"{speed / 1024 / 1024:0.00} MB/s" : "";

        // 仅在下载结束（下降沿）时刷新安装状态并设置提示；
        // 下载进行中不调用 RefreshCudaStatus，避免每次进度事件都遍历文件系统。
        if (wasDownloading && !IsCudaDownloading)
        {
            _cudaDownloadCts?.Dispose();
            _cudaDownloadCts = null;

            // RefreshCudaStatus 会同步 IsCudaDependencyInstalled 与状态文本。
            RefreshCudaStatus();

            if (_paddleRuntimeComponentService.LastInstallSucceeded != true)
            {
                SetCudaUnsupportedReason("CudaStatusDownloadFailed");
            }
            else
            {
                SetCudaUnsupportedReason(null);
            }
        }
    }

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
