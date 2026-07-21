extern alias host;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.WebRenderer;
using neo_bpsys_wpf.WebRenderer.Protocol;
using neo_bpsys_wpf.WebRenderer.Services;
using StaticClientVerifier = host::neo_bpsys_wpf.WebRenderer.Host.StaticClientVerifier;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Web Renderer 基础设施契约测试。
/// </summary>
public sealed class WebRendererInfrastructureTest
{
    /// <summary>最终 sidecar 只能接受引用完整且具有 build id 的静态入口页。</summary>
    [Fact]
    public void StaticClientVerifierAcceptsCompleteClient()
    {
        var root = CreateStaticClient("<meta name=\"web-renderer-client-build-id\" content=\"commit-20260719\" /><script type=\"module\" src=\"/assets/main-abc.js\"></script><link rel=\"stylesheet\" href=\"/assets/main-def.css\" />");
        try
        {
            File.WriteAllText(Path.Combine(root, "assets", "main-abc.js"), "console.log('ok');");
            File.WriteAllText(Path.Combine(root, "assets", "main-def.css"), "body{}");

            var client = StaticClientVerifier.Verify(root);

            Assert.Equal("commit-20260719", client.BuildId);
            Assert.Equal(["/assets/main-abc.js", "/assets/main-def.css"], client.LocalResourceUrls);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>入口页缺少引用文件、build id 或使用路径穿越时 sidecar 必须拒绝启动。</summary>
    [Theory]
    [InlineData("<meta name=\"web-renderer-client-build-id\" content=\"build\" /><script src=\"/assets/missing.js\"></script>")]
    [InlineData("<script src=\"/assets/main.js\"></script>")]
    [InlineData("<meta name=\"web-renderer-client-build-id\" content=\"build\" /><script src=\"/../outside.js\"></script>")]
    public void StaticClientVerifierRejectsInvalidDeployment(string head)
    {
        var root = CreateStaticClient(head);
        try
        {
            Assert.Throws<InvalidOperationException>(() => StaticClientVerifier.Verify(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// 未传入参数时应使用仅本机的固定默认监听地址。
    /// 默认 <see cref="WebRendererPluginSettings.StartWithApplication"/> 为 <see langword="false"/>，
    /// 因此默认 <c>NoStart</c> 为 <see langword="true"/>（sidecar 不随应用启动）。
    /// </summary>
    [Fact]
    public void LaunchOptionsUseLocalhostDefaults()
    {
        var options = WebRendererLaunchOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal("127.0.0.1", options.Address);
        Assert.Equal(19527, options.Port);
        Assert.True(options.NoStart);
        Assert.Null(options.ValidationError);
    }

    /// <summary>
    /// 显式 LAN 模式接受全网卡监听地址，且命令行禁用启动仍有效。
    /// </summary>
    [Fact]
    public void LaunchOptionsAcceptAllInterfacesAndNoStart()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["web-host"] = "0.0.0.0",
            ["web-port"] = "23000",
            ["web-no-start"] = "true"
        }).Build();

        var options = WebRendererLaunchOptions.FromConfiguration(configuration);

        Assert.Equal("0.0.0.0", options.Address);
        Assert.Equal(23000, options.Port);
        Assert.True(options.NoStart);
        Assert.Null(options.ValidationError);
    }

    /// <summary>
    /// 值为空的命令行开关也应禁用 sidecar 自动启动。
    /// </summary>
    [Fact]
    public void LaunchOptionsAcceptBareNoStartSwitch()
    {
        var options = WebRendererLaunchOptions.FromConfiguration(new ConfigurationBuilder()
            .AddCommandLine(["--web-no-start=true"])
            .Build());

        Assert.True(options.NoStart);
    }

    /// <summary>
    /// 非法端口不能进入 sidecar 启动流程。
    /// </summary>
    [Fact]
    public void LaunchOptionsRejectInvalidPort()
    {
        var options = WebRendererLaunchOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["web-port"] = "0" }).Build());

        Assert.NotNull(options.ValidationError);
    }

    /// <summary>
    /// IPC 信封必须以约定的小写字段进行序列化，并可 round-trip。
    /// </summary>
    [Fact]
    public void IpcMessageRoundTripsRequiredContract()
    {
        var message = new WebRendererIpcMessage
        {
            ProtocolVersion = WebRendererIpcProtocol.Version,
            Sequence = 7,
            Type = WebRendererIpcProtocol.Heartbeat,
            Payload = JsonSerializer.SerializeToElement(new { connected = true })
        };

        var json = JsonSerializer.Serialize(message);
        var restored = JsonSerializer.Deserialize<WebRendererIpcMessage>(json);

        Assert.Contains("\"protocolVersion\"", json);
        Assert.Contains("\"sequence\"", json);
        Assert.Contains("\"type\"", json);
        Assert.Contains("\"payload\"", json);
        Assert.NotNull(restored);
        Assert.Equal(7, restored.Sequence);
        Assert.Equal(WebRendererIpcProtocol.Heartbeat, restored.Type);
    }

    /// <summary>IPC 协议公开确认消息和完整生命周期，避免以“已写入”误判为就绪。</summary>
    [Fact]
    public void IpcProtocolDefinesAcknowledgedLifecycle()
    {
        Assert.Equal(8, WebRendererIpcProtocol.Version);
        Assert.Equal("bootstrap.applied", WebRendererIpcProtocol.BootstrapApplied);
        Assert.Equal("bootstrap.failed", WebRendererIpcProtocol.BootstrapFailed);
        Assert.Equal("bootstrap.rejected", WebRendererIpcProtocol.BootstrapRejected);
        Assert.Equal("localization.replace", WebRendererIpcProtocol.LocalizationReplace);
        Assert.Equal("localization.applied", WebRendererIpcProtocol.LocalizationApplied);
        Assert.Equal("session.state", WebRendererIpcProtocol.SessionState);
        Assert.Equal(
            [WebRendererLifecycleState.Stopped, WebRendererLifecycleState.StartingProcess,
                WebRendererLifecycleState.WaitingForPipe, WebRendererLifecycleState.PipeConnected,
                WebRendererLifecycleState.BuildingBootstrap, WebRendererLifecycleState.WaitingForBootstrapAck,
                WebRendererLifecycleState.Ready, WebRendererLifecycleState.Stopping, WebRendererLifecycleState.Faulted],
            Enum.GetValues<WebRendererLifecycleState>());
    }

    /// <summary>系统字体与显式 pack/package 字体必须按引用形式分类。</summary>
    [Theory]
    [InlineData("Arial", WebFontReferenceKind.SystemFont)]
    [InlineData("Segoe UI", WebFontReferenceKind.SystemFont)]
    [InlineData("Microsoft YaHei", WebFontReferenceKind.SystemFont)]
    [InlineData("Times New Roman", WebFontReferenceKind.SystemFont)]
    [InlineData("sans-serif", WebFontReferenceKind.SystemFont)]
    [InlineData("serif", WebFontReferenceKind.SystemFont)]
    [InlineData("monospace", WebFontReferenceKind.SystemFont)]
    [InlineData("pack://application:,,,/Assets/Fonts/#Noto Sans", WebFontReferenceKind.ApplicationPack)]
    [InlineData("bpui://package/Resources/fonts/custom.ttf#Custom", WebFontReferenceKind.PackageFont)]
    [InlineData("Resources/fonts/custom.woff2#Custom", WebFontReferenceKind.PackageFont)]
    public void FontReferencesAreClassifiedBySyntax(string value, WebFontReferenceKind expected)
    {
        Assert.Equal(expected, WebRendererBootstrapBuilder.ClassifyFontReference(value));
    }

    /// <summary>sidecar 没有 IPC/bootstrap 时，窗口 API 必须明确返回不可用而不是空成功数组。</summary>
    [Fact]
    public void SidecarWindowsAreUnavailableBeforeIpcHandshake()
    {
        var settings = new host::SidecarSettings("test-pipe", Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            System.Net.IPAddress.Loopback, 19527, "test");
        var state = new host::WebRendererHostState(settings, "test-client");

        var result = state.Windows();

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ((IStatusCodeHttpResult)result).StatusCode);
    }

    /// <summary>生命周期协调器必须串行执行重复管理命令，并在完成后恢复忙碌状态。</summary>
    [Fact]
    public async Task LifecycleCoordinatorSerializesOperationsAndRecoversState()
    {
        var coordinator = new WebRendererLifecycleOperationCoordinator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = coordinator.RunAsync("Stop", TimeSpan.FromSeconds(2), async _ => { entered.SetResult(); await release.Task; });
        await entered.Task;
        var secondEntered = false;
        var second = coordinator.RunAsync("Start", TimeSpan.FromSeconds(2), _ => { secondEntered = true; return Task.CompletedTask; });

        Assert.True(coordinator.IsLifecycleOperationRunning);
        Assert.False(secondEntered);
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.True(secondEntered);
        Assert.False(coordinator.IsLifecycleOperationRunning);
        Assert.Null(coordinator.CurrentOperation);
    }

    /// <summary>
    /// <c>--web-no-start</c> 的生命周期不应尝试启动进程，且停止可安全完成。
    /// </summary>
    [Fact]
    public async Task NoStartLifecycleIsFailSafe()
    {
        using var service = new WebRendererSidecarService(
            new WebRendererLaunchOptions("127.0.0.1", 19527, true, null),
            new WebRendererRuntimeDetector(),
            new WebRendererPlugin(),
            Mock.Of<ISnackbarService>(),
            Mock.Of<ILogger<WebRendererSidecarService>>());

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    private static string CreateStaticClient(string head)
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-bpsys-wpf-web-client-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "index.html"), $"<!doctype html><html><head>{head}</head><body></body></html>");
        return root;
    }
}
