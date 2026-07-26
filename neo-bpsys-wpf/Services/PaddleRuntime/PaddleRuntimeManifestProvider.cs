using System.Globalization;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;

// NOTE: PackageHashSha256 占位值，需在有网络环境时通过 Phase 5 脚本计算填入

namespace neo_bpsys_wpf.Services.PaddleRuntime;

/// <summary>
/// Paddle runtime 固定 manifest 提供者。维护 GPU Compute Capability 到
/// <c>Sdcb.PaddleInference.runtime.win64.*</c> NuGet 包的静态映射。
/// </summary>
public sealed class PaddleRuntimeManifestProvider : IPaddleRuntimeManifestProvider
{
    /// <summary>
    /// URL 路径段中禁止出现的危险字符集合，用于防止 URL 注入。
    /// </summary>
    private static readonly char[] UrlDangerousChars = ['/', '\\', ':', '?', '#', '&', '=', ' '];

    /// <summary>
    /// 固定 manifest 列表，仅初始化一次。
    /// </summary>
    private static readonly IReadOnlyList<PaddleRuntimePackageInfo> Packages = BuildPackages();

    /// <summary>
    /// 获取当前 PaddleInference runtime 的固定版本号（与 MKL 包版本一致）。
    /// </summary>
    public string PaddleInferenceVersion => AppConstants.PaddleInferenceRuntimeVersion;

    /// <summary>
    /// 根据 GPU Compute Capability 解析唯一匹配的 CUDA runtime 包。
    /// </summary>
    /// <param name="major">Compute Capability 主版本。</param>
    /// <param name="minor">Compute Capability 次版本。</param>
    /// <returns>匹配的包信息；未匹配时为 <see langword="null"/>。</returns>
    public PaddleRuntimePackageInfo? ResolveByComputeCapability(int major, int minor)
    {
        for (var i = 0; i < Packages.Count; i++)
        {
            var p = Packages[i];
            if (p.ComputeCapabilityMajor == major && p.ComputeCapabilityMinor == minor)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取 manifest 中所有包信息。
    /// </summary>
    /// <returns>所有包信息的只读列表。</returns>
    public IReadOnlyList<PaddleRuntimePackageInfo> GetAllPackages() => Packages;

    /// <summary>
    /// 构造指定包的 NuGet V3 Flat Container 下载 URL。
    /// </summary>
    /// <param name="package">包信息。</param>
    /// <returns>形如 <c>https://api.nuget.org/v3-flatcontainer/{lower-id}/{version}/{lower-id}.{version}.nupkg</c> 的下载 URL。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="package"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="package"/> 的 <c>PackageId</c> 或 <c>Version</c> 包含 URL 危险字符，
    /// 或 <c>PackageId</c> 包含非法 NuGet 包名字符。
    /// </exception>
    public string GetNuGetDownloadUrl(PaddleRuntimePackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidatePackageIdentifier(package.PackageId, nameof(package.PackageId));
        ValidatePackageIdentifier(package.Version, nameof(package.Version));

        var lowerId = package.PackageId.ToLowerInvariant();
        var lowerVersion = package.Version.ToLowerInvariant();
        return string.Format(
            CultureInfo.InvariantCulture,
            "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg",
            lowerId,
            lowerVersion);
    }

    /// <summary>
    /// 验证 NuGet 包标识符（PackageId 或 Version）不包含 URL 危险字符。
    /// 当 <paramref name="paramName"/> 标识为 PackageId 时，额外验证只包含合法 NuGet 包名字符
    /// （字母、数字、<c>.</c>、<c>-</c>、<c>_</c>）。
    /// </summary>
    /// <param name="value">待校验的值。</param>
    /// <param name="paramName">参数名，用于异常报告；为 <c>PackageId</c> 时启用 NuGet 包名字符校验。</param>
    /// <exception cref="ArgumentException">值为空，包含 URL 危险字符，或 PackageId 包含非法 NuGet 包名字符。</exception>
    private static void ValidatePackageIdentifier(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("NuGet 包标识符不能为空。", paramName);
        }

        if (value.IndexOfAny(UrlDangerousChars) >= 0)
        {
            throw new ArgumentException("NuGet 包标识符包含 URL 危险字符。", paramName);
        }

        if (paramName == nameof(PaddleRuntimePackageInfo.PackageId))
        {
            foreach (var c in value)
            {
                if (!IsValidNuGetPackageIdChar(c))
                {
                    throw new ArgumentException(
                        $"PackageId 包含非法 NuGet 包名字符 '{c}'。", paramName);
                }
            }
        }
    }

    /// <summary>
    /// 判断字符是否为合法的 NuGet 包名字符（字母、数字、<c>.</c>、<c>-</c>、<c>_</c>）。
    /// </summary>
    /// <param name="c">待判断的字符。</param>
    /// <returns>合法返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private static bool IsValidNuGetPackageIdChar(char c)
        => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

    /// <summary>
    /// 构造固定 manifest 列表。所有包版本统一为 <see cref="AppConstants.PaddleInferenceRuntimeVersion"/>。
    /// </summary>
    /// <returns>包信息只读列表。</returns>
    private static IReadOnlyList<PaddleRuntimePackageInfo> BuildPackages()
    {
        var version = AppConstants.PaddleInferenceRuntimeVersion;
        // TODO: Phase 5 下载实际包后计算并填入真实 SHA-256
        const string placeholderHash = "";
        var expectedNativeFiles = new[] { "paddle_inference_c.dll" };

        return
        [
            new PaddleRuntimePackageInfo(
                PackageId: "Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm61",
                Version: version,
                ComputeCapabilityMajor: 6,
                ComputeCapabilityMinor: 1,
                CudaVersion: "11.8",
                CudnnVersion: "8.9",
                PackageHashSha256: placeholderHash,
                ExpectedNativeFiles: expectedNativeFiles),
            new PaddleRuntimePackageInfo(
                PackageId: "Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm75",
                Version: version,
                ComputeCapabilityMajor: 7,
                ComputeCapabilityMinor: 5,
                CudaVersion: "11.8",
                CudnnVersion: "8.9",
                PackageHashSha256: placeholderHash,
                ExpectedNativeFiles: expectedNativeFiles),
            new PaddleRuntimePackageInfo(
                PackageId: "Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm86",
                Version: version,
                ComputeCapabilityMajor: 8,
                ComputeCapabilityMinor: 6,
                CudaVersion: "11.8",
                CudnnVersion: "8.9",
                PackageHashSha256: placeholderHash,
                ExpectedNativeFiles: expectedNativeFiles),
            new PaddleRuntimePackageInfo(
                PackageId: "Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm89",
                Version: version,
                ComputeCapabilityMajor: 8,
                ComputeCapabilityMinor: 9,
                CudaVersion: "11.8",
                CudnnVersion: "8.9",
                PackageHashSha256: placeholderHash,
                ExpectedNativeFiles: expectedNativeFiles),
            new PaddleRuntimePackageInfo(
                PackageId: "Sdcb.PaddleInference.runtime.win64.cu129_cudnn910_sm120",
                Version: version,
                ComputeCapabilityMajor: 12,
                ComputeCapabilityMinor: 0,
                CudaVersion: "12.9",
                CudnnVersion: "9.10",
                PackageHashSha256: placeholderHash,
                ExpectedNativeFiles: expectedNativeFiles),
        ];
    }
}
