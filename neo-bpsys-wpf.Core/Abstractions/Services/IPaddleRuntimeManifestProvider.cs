namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 表示一个 PaddleInference CUDA runtime NuGet 包的固定描述。
/// </summary>
/// <param name="PackageId">NuGet 包 ID，如 <c>Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm86</c>。</param>
/// <param name="Version">NuGet 包版本，必须与当前 MKL runtime 版本精确匹配。</param>
/// <param name="ComputeCapabilityMajor">目标 GPU Compute Capability 主版本。</param>
/// <param name="ComputeCapabilityMinor">目标 GPU Compute Capability 次版本。</param>
/// <param name="CudaVersion">CUDA runtime 版本，如 <c>11.8</c>。</param>
/// <param name="CudnnVersion">cuDNN 版本，如 <c>8.9</c>。</param>
/// <param name="PackageHashSha256">包的 SHA-256 哈希（小写十六进制）。</param>
/// <param name="ExpectedNativeFiles">期望包含的关键 native 文件列表。</param>
public sealed record PaddleRuntimePackageInfo(
    string PackageId,
    string Version,
    int ComputeCapabilityMajor,
    int ComputeCapabilityMinor,
    string CudaVersion,
    string CudnnVersion,
    string PackageHashSha256,
    IReadOnlyList<string> ExpectedNativeFiles);

/// <summary>
/// Paddle runtime 固定 manifest 提供者。维护 Compute Capability 到 NuGet 包的静态映射。
/// </summary>
public interface IPaddleRuntimeManifestProvider
{
    /// <summary>
    /// 获取当前 PaddleInference runtime 的固定版本号（与 MKL 包版本一致）。
    /// </summary>
    string PaddleInferenceVersion { get; }

    /// <summary>
    /// 根据 GPU Compute Capability 解析唯一匹配的 CUDA runtime 包。
    /// </summary>
    /// <param name="major">Compute Capability 主版本。</param>
    /// <param name="minor">Compute Capability 次版本。</param>
    /// <returns>匹配的包信息；未匹配时为 <see langword="null"/>。</returns>
    PaddleRuntimePackageInfo? ResolveByComputeCapability(int major, int minor);

    /// <summary>
    /// 获取 manifest 中所有包信息。
    /// </summary>
    /// <returns>所有包信息的只读列表。</returns>
    IReadOnlyList<PaddleRuntimePackageInfo> GetAllPackages();

    /// <summary>
    /// 构造指定包的 NuGet V3 Flat Container 下载 URL。
    /// </summary>
    /// <param name="package">包信息。</param>
    /// <returns>下载 URL。</returns>
    string GetNuGetDownloadUrl(PaddleRuntimePackageInfo package);
}
