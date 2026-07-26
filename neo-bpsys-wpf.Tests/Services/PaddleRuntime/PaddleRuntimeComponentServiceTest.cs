using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services.PaddleRuntime;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services.PaddleRuntime;

/// <summary>
/// <see cref="PaddleRuntimeComponentService"/> 的单元测试。
/// 覆盖公共 API 初始状态行为、构造函数校验、
/// 以及通过反射测试 <c>SafeGetDestinationPath</c>（Zip Slip 防护）与 <c>ExtractNativeFiles</c>（ZIP 提取）。
/// </summary>
public sealed class PaddleRuntimeComponentServiceTest : IDisposable
{
    private readonly List<string> _tempPaths = new();

    /// <summary>
    /// 释放测试中创建的临时文件和目录。
    /// </summary>
    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }

    private static PaddleRuntimeComponentService CreateService()
    {
        var logger = NullLogger<PaddleRuntimeComponentService>.Instance;
        var manifestProvider = new PaddleRuntimeManifestProvider();
        return new PaddleRuntimeComponentService(logger, manifestProvider);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var manifestProvider = new PaddleRuntimeManifestProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new PaddleRuntimeComponentService(null!, manifestProvider));
    }

    [Fact]
    public void Constructor_NullManifestProvider_ThrowsArgumentNullException()
    {
        var logger = NullLogger<PaddleRuntimeComponentService>.Instance;
        Assert.Throws<ArgumentNullException>(() =>
            new PaddleRuntimeComponentService(logger, null!));
    }

    [Fact]
    public void IsDownloading_InitiallyFalse()
    {
        var service = CreateService();
        Assert.False(service.IsDownloading);
    }

    [Fact]
    public void IsDownloadFinished_InitiallyFalse()
    {
        var service = CreateService();
        Assert.False(service.IsDownloadFinished);
    }

    [Fact]
    public void LastInstallSucceeded_InitiallyNull()
    {
        var service = CreateService();
        Assert.Null(service.LastInstallSucceeded);
    }

    [Fact]
    public void DownloadProgress_InitiallyNull()
    {
        var service = CreateService();
        Assert.Null(service.DownloadProgress);
    }

    [Fact]
    public void DownloadSpeed_InitiallyNull()
    {
        var service = CreateService();
        Assert.Null(service.DownloadSpeed);
    }

    [Fact]
    public void GetInstallStatus_WhenNotInstalled_ReturnsNotInstalled()
    {
        var service = CreateService();
        var status = service.GetInstallStatus();
        Assert.Equal(PaddleRuntimeInstallStatus.NotInstalled, status.Status);
        Assert.Null(status.PackageId);
        Assert.Null(status.PackageVersion);
        Assert.False(status.Verified);
    }

    [Fact]
    public void IsCompatibleWithCurrentVersion_WhenNotInstalled_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsCompatibleWithCurrentVersion());
    }

    [Fact]
    public void DeleteComponent_WhenNotInstalled_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.DeleteComponent());
    }

    [Fact]
    public async Task DownloadAsync_NullPackage_ThrowsArgumentNullException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.DownloadAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void SafeGetDestinationPath_NormalRelativePath_ReturnsCombinedPath()
    {
        var tempDir = GetTempDir();
        var result = InvokeSafeGetDestinationPath("paddle_inference_c.dll", tempDir);

        Assert.Equal(Path.Combine(tempDir, "paddle_inference_c.dll"), result);
    }

    [Fact]
    public void SafeGetDestinationPath_NormalNestedRelativePath_ReturnsCombinedPath()
    {
        var tempDir = GetTempDir();
        var result = InvokeSafeGetDestinationPath("subfolder/paddle_inference_c.dll", tempDir);

        Assert.Equal(Path.Combine(tempDir, "subfolder", "paddle_inference_c.dll"), result);
    }

    [Theory]
    [InlineData("../../../evil.dll")]
    [InlineData("..\\..\\..\\evil.dll")]
    [InlineData("foo/../../evil.dll")]
    public void SafeGetDestinationPath_ParentTraversal_ThrowsIOException(string relativePath)
    {
        var tempDir = GetTempDir();
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeSafeGetDestinationPath(relativePath, tempDir));
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\evil.dll")]
    public void SafeGetDestinationPath_AbsolutePath_ThrowsIOException(string absolutePath)
    {
        var tempDir = GetTempDir();
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeSafeGetDestinationPath(absolutePath, tempDir));
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    public void ExtractNativeFiles_ValidZip_ExtractsNativeFiles()
    {
        var zipPath = CreateTestNupkg(
            ("runtimes/win-x64/native/paddle_inference_c.dll", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }));
        var tempInstallDir = GetTempDir();
        Directory.CreateDirectory(tempInstallDir);

        InvokeExtractNativeFiles(zipPath, tempInstallDir, CancellationToken.None);

        var extractedFile = Path.Combine(tempInstallDir, "native", "paddle_inference_c.dll");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, File.ReadAllBytes(extractedFile));
    }

    [Fact]
    public void ExtractNativeFiles_SkipsNonNativeEntries()
    {
        var zipPath = CreateTestNupkg(
            ("runtimes/win-x64/native/paddle_inference_c.dll", new byte[] { 1 }),
            ("lib/net8.0/SomeLib.dll", new byte[] { 2 }),
            ("[Content_Types].xml", new byte[] { 3 }));
        var tempInstallDir = GetTempDir();
        Directory.CreateDirectory(tempInstallDir);

        InvokeExtractNativeFiles(zipPath, tempInstallDir, CancellationToken.None);

        var nativeFile = Path.Combine(tempInstallDir, "native", "paddle_inference_c.dll");
        Assert.True(File.Exists(nativeFile));
        var nonNativeFile = Path.Combine(tempInstallDir, "native", "SomeLib.dll");
        Assert.False(File.Exists(nonNativeFile));
    }

    [Fact]
    public void ExtractNativeFiles_MaliciousZipEntry_ThrowsIOException()
    {
        var zipPath = CreateTestNupkg(
            ("runtimes/win-x64/native/../../../evil.dll", new byte[] { 1 }));
        var tempInstallDir = GetTempDir();
        Directory.CreateDirectory(tempInstallDir);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeExtractNativeFiles(zipPath, tempInstallDir, CancellationToken.None));
        Assert.IsType<IOException>(ex.InnerException);

        var escapedFile = Path.Combine(tempInstallDir, "..", "..", "..", "evil.dll");
        Assert.False(File.Exists(escapedFile));
    }

    private string GetTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PaddleRuntimeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }

    private string CreateTestNupkg(params (string EntryPath, byte[] Content)[] entries)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"TestPkg_{Guid.NewGuid():N}.nupkg");
        _tempPaths.Add(zipPath);

        using var stream = File.Create(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            var entry = archive.CreateEntry(entryPath);
            using var entryStream = entry.Open();
            entryStream.Write(content, 0, content.Length);
        }

        return zipPath;
    }

    private static string InvokeSafeGetDestinationPath(string relativePath, string targetDir)
    {
        var method = typeof(PaddleRuntimeComponentService).GetMethod(
            "SafeGetDestinationPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object[] { relativePath, targetDir })!;
    }

    private static void InvokeExtractNativeFiles(string nupkgPath, string tempInstallDir, CancellationToken cancellationToken)
    {
        var method = typeof(PaddleRuntimeComponentService).GetMethod(
            "ExtractNativeFiles",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method!.Invoke(null, new object[] { nupkgPath, tempInstallDir, cancellationToken });
    }
}
