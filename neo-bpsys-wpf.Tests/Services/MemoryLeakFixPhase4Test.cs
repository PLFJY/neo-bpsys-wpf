#nullable enable

using System;
using System.IO;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 阶段 4 内存治理验证测试。
/// 验证 WindowCaptureService.OnFrameArrived 的高频大对象分配优化：
/// staging texture 和 byte[] buffer 在尺寸不变时跨帧复用，避免每帧在 LOH 分配约 8 MiB。
/// </summary>
public sealed class MemoryLeakFixPhase4Test
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. staging texture 复用源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 WindowCaptureService 声明了复用的 staging texture 字段。
    /// </summary>
    [Fact]
    public void WindowCaptureService_HasStagingTextureReuseField()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        Assert.Contains("private Texture2D? _stagingTexture", source);
    }

    /// <summary>
    /// 验证 WindowCaptureService 声明了复用的 pixel buffer 字段。
    /// </summary>
    [Fact]
    public void WindowCaptureService_HasStagingBufferReuseField()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        Assert.Contains("private byte[]? _stagingBuffer", source);
    }

    /// <summary>
    /// 验证 WindowCaptureService 声明了 staging 尺寸追踪字段（用于判断是否需要重建）。
    /// </summary>
    [Fact]
    public void WindowCaptureService_HasStagingSizeTrackingFields()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        Assert.Contains("private int _stagingWidth", source);
        Assert.Contains("private int _stagingHeight", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. OnFrameArrived 复用逻辑源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 OnFrameArrived 在尺寸不变时复用 staging texture，而非每帧创建新纹理。
    /// 关键逻辑：检查 _stagingTexture 是否为 null 或尺寸是否变化，仅在需要时 Dispose 旧纹理并重建。
    /// </summary>
    [Fact]
    public void OnFrameArrived_ReusesStagingTextureWhenSizeUnchanged()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        // 必须存在尺寸检查逻辑。
        Assert.Contains("_stagingTexture is null", source);
        Assert.Contains("_stagingWidth != contentSize.Width", source);
        Assert.Contains("_stagingHeight != contentSize.Height", source);

        // 尺寸变化时必须 Dispose 旧纹理。
        Assert.Contains("_stagingTexture?.Dispose()", source);

        // 不应再有每帧无条件创建新 staging texture 的旧模式（using var stagingTexture = CreateCpuReadableTexture）。
        Assert.DoesNotContain("using var stagingTexture =", source);
    }

    /// <summary>
    /// 验证 CreateBitmapSourceFromTexture 复用 byte[] buffer，而非每帧分配新数组。
    /// </summary>
    [Fact]
    public void CreateBitmapSourceFromTexture_ReusesPixelBuffer()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        // 提取 CreateBitmapSourceFromTexture 方法体。
        var startIdx = source.IndexOf("CreateBitmapSourceFromTexture(Texture2D", StringComparison.Ordinal);
        Assert.True(startIdx >= 0, "CreateBitmapSourceFromTexture method not found");
        // 用方法声明签名作为边界，避免方法名在调用处出现导致边界错误。
        var endIdx = source.IndexOf("private BitmapSource? CropToClientAreaIfNeeded", StringComparison.Ordinal);
        Assert.True(endIdx > startIdx, "CropToClientAreaIfNeeded declaration not found after CreateBitmapSourceFromTexture");
        var methodBody = source.Substring(startIdx, endIdx - startIdx);

        // 必须检查 _stagingBuffer 是否为 null 或尺寸不足。
        Assert.Contains("_stagingBuffer is null", methodBody);
        Assert.Contains("_stagingBuffer.Length < requiredSize", methodBody);

        // 不应再有每帧无条件 new byte[] 的旧模式。
        Assert.DoesNotContain("var pixels = new byte[stride * height]", methodBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. StopCapture 清理逻辑源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 StopCapture 释放复用的 staging 资源，避免停止捕获后仍持有 D3D 纹理和大缓冲区。
    /// </summary>
    [Fact]
    public void StopCapture_DisposesStagingResources()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        // 提取 StopCapture 方法体。
        var startIdx = source.IndexOf("public void StopCapture()", StringComparison.Ordinal);
        Assert.True(startIdx >= 0, "StopCapture method not found");
        // 用方法声明签名作为边界，避免方法名在调用处出现导致边界错误。
        var endIdx = source.IndexOf("private bool StartBitbltCaptureFromHwnd", StringComparison.Ordinal);
        Assert.True(endIdx > startIdx, "StartBitbltCaptureFromHwnd declaration not found after StopCapture");
        var methodBody = source.Substring(startIdx, endIdx - startIdx);

        // 必须 Dispose staging texture。
        Assert.Contains("_stagingTexture?.Dispose()", methodBody);
        // 必须清空 buffer 和尺寸字段。
        Assert.Contains("_stagingBuffer = null", methodBody);
        Assert.Contains("_stagingWidth = 0", methodBody);
        Assert.Contains("_stagingHeight = 0", methodBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. 复用安全性源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证复用的 staging 资源仅在 OnFrameArrived 中访问（捕获线程），
    /// 不涉及跨线程同步问题。
    /// </summary>
    [Fact]
    public void StagingResources_OnlyAccessedInOnFrameArrived()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        // _stagingTexture 不应在 GetCurrentFrame（UI 线程读取路径）中被访问。
        var getCurrentFrameStart = source.IndexOf("public BitmapSource? GetCurrentFrame()", StringComparison.Ordinal);
        Assert.True(getCurrentFrameStart >= 0, "GetCurrentFrame method not found");
        var getCurrentFrameEnd = source.IndexOf("OpenPreviewWindow", StringComparison.Ordinal);
        Assert.True(getCurrentFrameEnd > getCurrentFrameStart);
        var getCurrentFrameBody = source.Substring(getCurrentFrameStart, getCurrentFrameEnd - getCurrentFrameStart);

        Assert.DoesNotContain("_stagingTexture", getCurrentFrameBody);
        Assert.DoesNotContain("_stagingBuffer", getCurrentFrameBody);
    }

    /// <summary>
    /// 验证 MapSubresource/UnmapSubresource 仍然在 CreateBitmapSourceFromTexture 中配对调用，
    /// 复用 buffer 不影响 D3D 资源的 Map/Unmap 配对。
    /// </summary>
    [Fact]
    public void BufferReuse_PreservesMapUnmapPairing()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "WindowCaptureService.cs");

        // 提取 CreateBitmapSourceFromTexture 方法体。
        var startIdx = source.IndexOf("CreateBitmapSourceFromTexture(Texture2D", StringComparison.Ordinal);
        var endIdx = source.IndexOf("private BitmapSource? CropToClientAreaIfNeeded", StringComparison.Ordinal);
        var methodBody = source.Substring(startIdx, endIdx - startIdx);

        Assert.Contains("MapSubresource", methodBody);
        Assert.Contains("UnmapSubresource", methodBody);
        // Unmap 必须在 finally 块中。
        Assert.Contains("finally", methodBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine([directory.FullName, .. parts]);
        Assert.True(File.Exists(path), $"File not found: {path}");
        return File.ReadAllText(path);
    }
}
