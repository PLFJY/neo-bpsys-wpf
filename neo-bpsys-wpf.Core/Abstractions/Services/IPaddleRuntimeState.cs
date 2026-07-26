namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Paddle runtime 运行时状态。提供当前进程实际加载的后端、设备、模块路径等只读状态。
/// </summary>
public interface IPaddleRuntimeState
{
    /// <summary>
    /// 本进程实际激活的推理后端。由 Bootstrap 在启动时确定，运行期不可变。
    /// </summary>
    OcrInferenceBackend ActiveBackend { get; }

    /// <summary>
    /// 实际使用的 CUDA 设备 ID；<see cref="ActiveBackend"/> 非 Cuda 时为 -1。
    /// </summary>
    int ActiveCudaDeviceId { get; }

    /// <summary>
    /// 实际加载的 <c>paddle_inference_c.dll</c> 完整路径。
    /// </summary>
    string? LoadedNativeModulePath { get; }

    /// <summary>
    /// 检测到的 CUDA 设备列表。
    /// </summary>
    IReadOnlyList<CudaDeviceInfo> DetectedCudaDevices { get; }

    /// <summary>
    /// 选中的 CUDA 设备；无设备时为 <see langword="null"/>。
    /// </summary>
    CudaDeviceInfo? SelectedCudaDevice { get; }

    /// <summary>
    /// CUDA runtime 组件是否已安装。
    /// </summary>
    bool CudaRuntimeInstalled { get; }

    /// <summary>
    /// 已安装的 CUDA runtime 组件是否与当前 PaddleInference 版本兼容。
    /// </summary>
    bool CudaRuntimeCompatible { get; }

    /// <summary>
    /// 是否需要重启以应用后端切换。
    /// </summary>
    bool RestartRequired { get; }

    /// <summary>
    /// runtime 加载错误信息；无错误时为 <see langword="null"/>。
    /// </summary>
    string? RuntimeLoadError { get; }

    /// <summary>
    /// PaddleOCR Predictor 是否在当前后端下成功构造并验证。
    /// 仅当 <see cref="ActiveBackend"/> 为 <see cref="OcrInferenceBackend.Cuda"/> 且
    /// 真实 <c>PaddleOcrAll</c> 构造成功（含 det/cls/rec 三个 Predictor）后才为 <see langword="true"/>。
    /// 用于区分"runtime DLL 加载成功"与"真实模型在 GPU 上可用"。
    /// </summary>
    bool PaddleBackendVerified { get; }

    /// <summary>
    /// 状态变化事件。
    /// </summary>
    event EventHandler? StateChanged;
}
