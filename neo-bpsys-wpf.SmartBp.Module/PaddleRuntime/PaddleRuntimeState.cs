using System;
using System.Collections.Generic;
using System.Threading;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// <see cref="IPaddleRuntimeState"/> 的实现。持有当前进程实际加载的 Paddle runtime 状态，
/// 状态字段由 Bootstrap 在启动时通过 internal 方法写入，运行期对前台为只读。
/// </summary>
public sealed class PaddleRuntimeState : IPaddleRuntimeState
{
    private readonly IGlobalRestartService _globalRestartService;

    // volatile 不支持 Nullable<T>，故 _selectedCudaDevice 改用 Volatile.Read / Volatile.Write
    private volatile OcrInferenceBackend _activeBackend = OcrInferenceBackend.Cpu;
    private volatile int _activeCudaDeviceId = 0;
    private volatile string? _loadedNativeModulePath;
    private volatile IReadOnlyList<CudaDeviceInfo> _detectedCudaDevices = Array.Empty<CudaDeviceInfo>();
    private CudaDeviceInfo? _selectedCudaDevice;
    private volatile bool _cudaRuntimeInstalled;
    private volatile bool _cudaRuntimeCompatible;
    private volatile string? _runtimeLoadError;
    private volatile bool _paddleBackendVerified;

    /// <summary>
    /// 初始化 <see cref="PaddleRuntimeState"/> 类的新实例。
    /// </summary>
    /// <param name="globalRestartService">全局重启服务，用于读取 <see cref="RestartRequired"/> 状态。仅读取，写入由 Bootstrap 负责。</param>
    /// <exception cref="ArgumentNullException"><paramref name="globalRestartService"/> 为 <see langword="null"/>。</exception>
    public PaddleRuntimeState(IGlobalRestartService globalRestartService)
    {
        _globalRestartService = globalRestartService ?? throw new ArgumentNullException(nameof(globalRestartService));
    }

    /// <inheritdoc/>
    public OcrInferenceBackend ActiveBackend => _activeBackend;

    /// <inheritdoc/>
    public int ActiveCudaDeviceId => _activeCudaDeviceId;

    /// <inheritdoc/>
    public string? LoadedNativeModulePath => _loadedNativeModulePath;

    /// <inheritdoc/>
    public IReadOnlyList<CudaDeviceInfo> DetectedCudaDevices => _detectedCudaDevices;

    /// <inheritdoc/>
    public CudaDeviceInfo? SelectedCudaDevice => Volatile.Read(ref _selectedCudaDevice);

    /// <inheritdoc/>
    public bool CudaRuntimeInstalled => _cudaRuntimeInstalled;

    /// <inheritdoc/>
    public bool CudaRuntimeCompatible => _cudaRuntimeCompatible;

    /// <inheritdoc/>
    public bool RestartRequired => _globalRestartService.IsRestartRequired;

    /// <inheritdoc/>
    public string? RuntimeLoadError => _runtimeLoadError;

    /// <inheritdoc/>
    public bool PaddleBackendVerified => _paddleBackendVerified;

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <summary>
    /// 触发 <see cref="StateChanged"/> 事件。供 Bootstrap 在完成状态变更后调用。
    /// </summary>
    internal void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 设置当前激活的后端、CUDA 设备 ID 与实际加载的本地模块路径。
    /// </summary>
    /// <param name="backend">激活的推理后端。</param>
    /// <param name="cudaDeviceId">实际使用的 CUDA 设备 ID；<paramref name="backend"/> 非 Cuda 时通常为 -1。</param>
    /// <param name="modulePath">实际加载的 <c>paddle_inference_c.dll</c> 完整路径；未加载时为 <see langword="null"/>。</param>
    internal void SetActiveBackend(OcrInferenceBackend backend, int cudaDeviceId, string? modulePath)
    {
        _activeBackend = backend;
        _activeCudaDeviceId = cudaDeviceId;
        _loadedNativeModulePath = modulePath;
        RaiseStateChanged();
    }

    /// <summary>
    /// 设置检测到的 CUDA 设备列表与当前选中的设备。
    /// </summary>
    /// <param name="devices">检测到的 CUDA 设备列表；<see langword="null"/> 视为空列表。</param>
    /// <param name="selected">当前选中的 CUDA 设备；无设备时为 <see langword="null"/>。</param>
    internal void SetDetectedDevices(IReadOnlyList<CudaDeviceInfo> devices, CudaDeviceInfo? selected)
    {
        _detectedCudaDevices = devices ?? Array.Empty<CudaDeviceInfo>();
        Volatile.Write(ref _selectedCudaDevice, selected);
        RaiseStateChanged();
    }

    /// <summary>
    /// 设置 CUDA runtime 安装与兼容状态。
    /// </summary>
    /// <param name="installed">CUDA runtime 组件是否已安装。</param>
    /// <param name="compatible">已安装的 CUDA runtime 是否与当前 PaddleInference 版本兼容。</param>
    internal void SetCudaRuntimeStatus(bool installed, bool compatible)
    {
        _cudaRuntimeInstalled = installed;
        _cudaRuntimeCompatible = compatible;
        RaiseStateChanged();
    }

    /// <summary>
    /// 设置 runtime 加载错误信息。
    /// </summary>
    /// <param name="error">错误信息；无错误时为 <see langword="null"/>。</param>
    internal void SetRuntimeLoadError(string? error)
    {
        _runtimeLoadError = error;
        RaiseStateChanged();
    }

    /// <summary>
    /// 设置 PaddleOCR Predictor 验证状态。
    /// 由 OcrService 在成功构造 <c>PaddleOcrAll</c>（含 det/cls/rec 三个 Predictor）后调用，
    /// 或在构造失败 / 后端切换时重置为 <see langword="false"/>。
    /// </summary>
    /// <param name="verified">是否已验证当前后端下 PaddleOCR Predictor 可用。</param>
    internal void SetPaddleBackendVerified(bool verified)
    {
        _paddleBackendVerified = verified;
        RaiseStateChanged();
    }
}
