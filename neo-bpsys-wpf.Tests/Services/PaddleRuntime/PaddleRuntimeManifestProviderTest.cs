using System;
using System.Linq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services.PaddleRuntime;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services.PaddleRuntime;

/// <summary>
/// <see cref="PaddleRuntimeManifestProvider"/> 的单元测试。
/// 覆盖 Compute Capability 到 NuGet 包的映射、manifest 完整性以及 NuGet 下载 URL 构造与安全校验。
/// </summary>
public sealed class PaddleRuntimeManifestProviderTest
{
    private readonly PaddleRuntimeManifestProvider _provider = new();

    [Theory]
    [InlineData(6, 1, "sm61")]
    [InlineData(7, 5, "sm75")]
    [InlineData(8, 6, "sm86")]
    [InlineData(8, 9, "sm89")]
    [InlineData(12, 0, "sm120")]
    public void ResolveByComputeCapability_SupportedCC_ReturnsMatchingPackage(int major, int minor, string expectedFragment)
    {
        var package = _provider.ResolveByComputeCapability(major, minor);

        Assert.NotNull(package);
        Assert.Contains(expectedFragment, package!.PackageId);
        Assert.Equal(major, package.ComputeCapabilityMajor);
        Assert.Equal(minor, package.ComputeCapabilityMinor);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(9, 9)]
    [InlineData(7, 0)]
    [InlineData(13, 0)]
    public void ResolveByComputeCapability_UnsupportedCC_ReturnsNull(int major, int minor)
    {
        var package = _provider.ResolveByComputeCapability(major, minor);

        Assert.Null(package);
    }

    [Fact]
    public void GetAllPackages_ReturnsFiveEntries()
    {
        var packages = _provider.GetAllPackages();

        Assert.Equal(5, packages.Count);
    }

    [Fact]
    public void PaddleInferenceVersion_ReturnsExpectedValue()
    {
        Assert.Equal("3.3.1.70", _provider.PaddleInferenceVersion);
    }

    [Fact]
    public void GetAllPackages_AllVersionsMatchExpected()
    {
        var packages = _provider.GetAllPackages();

        foreach (var package in packages)
        {
            Assert.Equal("3.3.1.70", package.Version);
        }
    }

    [Fact]
    public void GetAllPackages_AllContainPaddleInferenceCDll()
    {
        var packages = _provider.GetAllPackages();

        foreach (var package in packages)
        {
            Assert.Contains("paddle_inference_c.dll", package.ExpectedNativeFiles);
        }
    }

    [Fact]
    public void GetAllPackages_AllPackageIdsAreUnique()
    {
        var packages = _provider.GetAllPackages();

        var ids = packages.Select(p => p.PackageId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GetNuGetDownloadUrl_ReturnsCorrectFormat()
    {
        var package = _provider.GetAllPackages()[0];

        var url = _provider.GetNuGetDownloadUrl(package);

        var expected = $"https://api.nuget.org/v3-flatcontainer/{package.PackageId.ToLowerInvariant()}/{package.Version.ToLowerInvariant()}/{package.PackageId.ToLowerInvariant()}.{package.Version.ToLowerInvariant()}.nupkg";
        Assert.Equal(expected, url);
    }

    [Fact]
    public void GetNuGetDownloadUrl_LowercasesPackageIdAndVersion()
    {
        var package = new PaddleRuntimePackageInfo(
            PackageId: "Sdcb.PaddleInference.runtime.win64.cu118_cudnn89_sm86",
            Version: "3.3.1.70",
            ComputeCapabilityMajor: 8,
            ComputeCapabilityMinor: 6,
            CudaVersion: "11.8",
            CudnnVersion: "8.9",
            PackageHashSha256: "",
            ExpectedNativeFiles: new[] { "paddle_inference_c.dll" });

        var url = _provider.GetNuGetDownloadUrl(package);

        Assert.Contains("sdcb.paddleinference.runtime.win64.cu118_cudnn89_sm86", url);
        Assert.DoesNotContain("Sdcb", url);
        Assert.Contains("3.3.1.70", url);
    }

    [Fact]
    public void GetNuGetDownloadUrl_NullPackage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GetNuGetDownloadUrl(null!));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\\")]
    [InlineData(":")]
    [InlineData("?")]
    [InlineData("#")]
    [InlineData("&")]
    [InlineData("=")]
    [InlineData(" ")]
    public void GetNuGetDownloadUrl_DangerousCharInPackageId_ThrowsArgumentException(string dangerousChar)
    {
        var package = new PaddleRuntimePackageInfo(
            PackageId: "Bad" + dangerousChar + "Package",
            Version: "1.0.0",
            ComputeCapabilityMajor: 8,
            ComputeCapabilityMinor: 6,
            CudaVersion: "11.8",
            CudnnVersion: "8.9",
            PackageHashSha256: "",
            ExpectedNativeFiles: Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => _provider.GetNuGetDownloadUrl(package));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("@")]
    [InlineData("$")]
    [InlineData("%")]
    [InlineData("(")]
    public void GetNuGetDownloadUrl_IllegalNuGetCharInPackageId_ThrowsArgumentException(string illegalChar)
    {
        var package = new PaddleRuntimePackageInfo(
            PackageId: "Bad" + illegalChar + "Package",
            Version: "1.0.0",
            ComputeCapabilityMajor: 8,
            ComputeCapabilityMinor: 6,
            CudaVersion: "11.8",
            CudnnVersion: "8.9",
            PackageHashSha256: "",
            ExpectedNativeFiles: Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => _provider.GetNuGetDownloadUrl(package));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\\")]
    [InlineData(":")]
    public void GetNuGetDownloadUrl_DangerousCharInVersion_ThrowsArgumentException(string dangerousChar)
    {
        var package = new PaddleRuntimePackageInfo(
            PackageId: "Valid.Package.Id",
            Version: "1.0" + dangerousChar + "0",
            ComputeCapabilityMajor: 8,
            ComputeCapabilityMinor: 6,
            CudaVersion: "11.8",
            CudnnVersion: "8.9",
            PackageHashSha256: "",
            ExpectedNativeFiles: Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => _provider.GetNuGetDownloadUrl(package));
    }

    [Fact]
    public void GetNuGetDownloadUrl_EmptyPackageId_ThrowsArgumentException()
    {
        var package = new PaddleRuntimePackageInfo(
            PackageId: "",
            Version: "1.0.0",
            ComputeCapabilityMajor: 8,
            ComputeCapabilityMinor: 6,
            CudaVersion: "11.8",
            CudnnVersion: "8.9",
            PackageHashSha256: "",
            ExpectedNativeFiles: Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => _provider.GetNuGetDownloadUrl(package));
    }
}
