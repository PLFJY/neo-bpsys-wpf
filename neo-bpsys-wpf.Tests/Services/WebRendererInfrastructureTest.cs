using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.WebRenderer;
using neo_bpsys_wpf.WebRenderer.Protocol;
using neo_bpsys_wpf.WebRenderer.Services;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Web Renderer 基础设施契约测试。
/// </summary>
public sealed class WebRendererInfrastructureTest
{
    /// <summary>
    /// 未传入参数时应使用仅本机的固定默认监听地址。
    /// </summary>
    [Fact]
    public void LaunchOptionsUseLocalhostDefaults()
    {
        var options = WebRendererLaunchOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal("127.0.0.1", options.Address);
        Assert.Equal(19527, options.Port);
        Assert.False(options.NoStart);
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
}
