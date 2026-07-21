#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 阶段 5 内存治理验证测试。
/// 验证 WebRenderer sidecar 默认不随应用启动（<c>StartWithApplication</c> 默认 <c>false</c>），
/// 避免未使用 Web Renderer 时产生常驻子进程内存占用。
/// </summary>
public sealed class MemoryLeakFixPhase5Test
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. 默认值源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 <see cref="WebRendererPluginSettings.StartWithApplication"/> 默认值为 <see langword="false"/>。
    /// 这是 Phase 5 的核心改动：新安装不自动启动 sidecar 进程。
    /// </summary>
    [Fact]
    public void StartWithApplication_DefaultValue_IsFalse()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRendererPluginSettings.cs");

        // 不应有 "= true" 初始化器。
        var propertyLine = ExtractPropertyDeclaration(source, "StartWithApplication");
        Assert.DoesNotContain("= true", propertyLine);
    }

    /// <summary>
    /// 验证默认值文档注释说明默认不随应用启动。
    /// </summary>
    [Fact]
    public void StartWithApplication_DocComment_ExplainsLazyStartDefault()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRendererPluginSettings.cs");

        Assert.Contains("默认值为 <see langword=\"false\"", source);
        Assert.Contains("不随应用启动自动运行", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. 默认值行为验证
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证新建 <see cref="WebRendererPluginSettings"/> 实例的 <c>StartWithApplication</c> 为 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void NewSettings_Instance_HasStartWithApplicationFalse()
    {
        var settings = new WebRendererPluginSettings();

        Assert.False(settings.StartWithApplication);
    }

    /// <summary>
    /// 验证使用默认设置时 <see cref="WebRendererLaunchOptions.NoStart"/> 为 <see langword="true"/>。
    /// 即默认不自动启动 sidecar 进程。
    /// </summary>
    [Fact]
    public void LaunchOptions_DefaultSettings_NoStartIsTrue()
    {
        var options = WebRendererLaunchOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.True(options.NoStart);
    }

    /// <summary>
    /// 验证显式设置 <c>StartWithApplication = true</c> 时 <c>NoStart</c> 为 <see langword="false"/>。
    /// 用户仍可通过设置恢复自动启动行为。
    /// </summary>
    [Fact]
    public void LaunchSettings_StartWithApplicationTrue_NoStartIsFalse()
    {
        var settings = new WebRendererPluginSettings { StartWithApplication = true };
        var options = WebRendererLaunchOptions.FromConfiguration(
            new ConfigurationBuilder().Build(),
            settings);

        Assert.False(options.NoStart);
    }

    /// <summary>
    /// 验证命令行 <c>--web-no-start</c> 覆盖 <c>StartWithApplication = true</c>。
    /// 命令行开关始终优先于设置文件。
    /// </summary>
    [Fact]
    public void LaunchOptions_CommandLineNoStart_OverridesStartWithApplicationTrue()
    {
        var settings = new WebRendererPluginSettings { StartWithApplication = true };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["web-no-start"] = "true"
            })
            .Build();

        var options = WebRendererLaunchOptions.FromConfiguration(configuration, settings);

        Assert.True(options.NoStart);
    }

    /// <summary>
    /// 验证 <c>StartWithApplication = false</c> 且无命令行开关时 <c>NoStart</c> 为 <see langword="true"/>。
    /// 这是新安装用户的默认体验。
    /// </summary>
    [Fact]
    public void LaunchOptions_StartWithApplicationFalse_NoCommandLine_NoStartIsTrue()
    {
        var settings = new WebRendererPluginSettings { StartWithApplication = false };
        var options = WebRendererLaunchOptions.FromConfiguration(
            new ConfigurationBuilder().Build(),
            settings);

        Assert.True(options.NoStart);
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

    private static string ExtractPropertyDeclaration(string source, string propertyName)
    {
        var startIdx = source.IndexOf($"public bool {propertyName}", StringComparison.Ordinal);
        Assert.True(startIdx >= 0, $"{propertyName} property not found");
        var endIdx = source.IndexOf('}', startIdx);
        Assert.True(endIdx > startIdx, $"End of {propertyName} property not found");
        return source.Substring(startIdx, endIdx - startIdx + 1);
    }
}
