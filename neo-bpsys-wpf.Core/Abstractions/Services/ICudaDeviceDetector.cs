namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 表示通过 NVIDIA CUDA Driver API 检测到的一张 CUDA 设备信息。
/// </summary>
/// <param name="DeviceId">CUDA 设备 ID（0 起）。</param>
/// <param name="DeviceName">设备名称（来自 <c>cuDeviceGetName</c>）。</param>
/// <param name="ComputeCapabilityMajor">Compute Capability 主版本。</param>
/// <param name="ComputeCapabilityMinor">Compute Capability 次版本。</param>
/// <param name="CudaDriverVersion">CUDA Driver 版本（来自 <c>cuDriverGetVersion</c>）。</param>
/// <param name="IsSupported">该设备的 Compute Capability 是否在受支持列表中。</param>
public sealed record CudaDeviceInfo(
    int DeviceId,
    string DeviceName,
    int ComputeCapabilityMajor,
    int ComputeCapabilityMinor,
    Version CudaDriverVersion,
    bool IsSupported);

/// <summary>
/// NVIDIA CUDA 设备检测器。通过 <c>nvcuda.dll</c> P/Invoke 直接调用 CUDA Driver API 进行检测。
/// </summary>
public interface ICudaDeviceDetector
{
    /// <summary>
    /// 检测系统中所有 NVIDIA CUDA 设备。
    /// </summary>
    /// <returns>检测到的设备列表。无设备或 <c>nvcuda.dll</c> 不存在时返回空列表。</returns>
    IReadOnlyList<CudaDeviceInfo> DetectDevices();
}
