namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Paddle OCR 推理后端类型。
/// </summary>
public enum OcrInferenceBackend
{
    /// <summary>
    /// 使用 CPU（MKLDNN）推理。
    /// </summary>
    Cpu,

    /// <summary>
    /// 使用 NVIDIA CUDA GPU 推理。
    /// </summary>
    Cuda
}
