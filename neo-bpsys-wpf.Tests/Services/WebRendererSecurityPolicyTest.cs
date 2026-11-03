extern alias host;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;
using AddressPolicy = host::neo_bpsys_wpf.WebRenderer.Host.RemoteAssetAddressPolicy;
using RemoteAssetException = host::neo_bpsys_wpf.WebRenderer.Host.RemoteAssetException;
using WebSocketOriginPolicy = host::WebSocketOriginPolicy;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Web Renderer sidecar 安全策略测试：WebSocket Origin、运行时 feed 校验与 DNS 固定解析。
/// </summary>
public sealed class WebRendererSecurityPolicyTest
{
    /// <summary>非浏览器客户端（无 Origin）与同源页面允许连接；跨站页面与端口不符必须拒绝。</summary>
    [Theory]
    [InlineData(null, "127.0.0.1", 19527, true)]
    [InlineData("", "127.0.0.1", 19527, true)]
    [InlineData("http://127.0.0.1:19527", "127.0.0.1", 19527, true)]
    [InlineData("http://localhost:19527", "127.0.0.1", 19527, true)]
    [InlineData("http://[::1]:19527", "127.0.0.1", 19527, true)]
    [InlineData("http://evil.example:19527", "127.0.0.1", 19527, false)]
    [InlineData("http://127.0.0.1:1234", "127.0.0.1", 19527, false)]
    [InlineData("null", "127.0.0.1", 19527, false)]
    [InlineData("file:///C:/page.html", "127.0.0.1", 19527, false)]
    [InlineData("http://192.168.1.5:19527", "0.0.0.0", 19527, true)]
    [InlineData("http://192.168.1.5:19527", "192.168.1.5", 19527, true)]
    [InlineData("http://192.168.1.6:19527", "192.168.1.5", 19527, false)]
    public void WebSocketOriginPolicyMatchesListenEndpoint(string? origin, string listenAddress, int port, bool expected)
    {
        Assert.Equal(expected, WebSocketOriginPolicy.IsAllowed(origin, IPAddress.Parse(listenAddress), port));
    }

    /// <summary>远端版本号只能包含安全字符，且不得形成路径穿越。</summary>
    [Theory]
    [InlineData("10.0.10", true)]
    [InlineData("10.0.10-preview.1", true)]
    [InlineData("10.0.10+build.5", true)]
    [InlineData("", false)]
    [InlineData("..", false)]
    [InlineData("10.0.10..1", false)]
    [InlineData("10.0.10/../../x", false)]
    [InlineData("10.0.10\\x", false)]
    [InlineData("10.0.10 ", false)]
    [InlineData(".10.0.10", false)]
    public void RuntimeVersionMustBeSafeFileNameComponent(string version, bool expected)
    {
        Assert.Equal(expected, WebRendererRuntimeSetupService.IsSafeVersionComponent(version));
    }

    /// <summary>installer 只允许官方 HTTPS 主机，拒绝伪装域名与 http 地址。</summary>
    [Theory]
    [InlineData("https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.10/aspnetcore-runtime-10.0.10-win-x64.exe", true)]
    [InlineData("https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/10.0.10/x.exe", true)]
    [InlineData("http://builds.dotnet.microsoft.com/dotnet/x.exe", false)]
    [InlineData("https://builds.dotnet.microsoft.com.evil.example/x.exe", false)]
    [InlineData("https://evil.example/x.exe", false)]
    [InlineData("file:///C:/x.exe", false)]
    public void InstallerUriMustUseTrustedOfficialHost(string url, bool expected)
    {
        Assert.Equal(expected, WebRendererRuntimeSetupService.TryGetTrustedInstallerUri(url, out _));
    }

    /// <summary>installer 路径必须仍位于临时下载目录内。</summary>
    [Fact]
    public void InstallerPathContainmentDetectsTraversal()
    {
        var downloadDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "neo-bpsys-wpf_WebRenderer");

        Assert.True(WebRendererRuntimeSetupService.IsUnderDirectory(
            System.IO.Path.Combine(downloadDirectory, "aspnetcore-runtime-10.0.10-win-x64.exe"), downloadDirectory));
        Assert.False(WebRendererRuntimeSetupService.IsUnderDirectory(
            System.IO.Path.Combine(downloadDirectory, "..", "evil.exe"), downloadDirectory));
        Assert.False(WebRendererRuntimeSetupService.IsUnderDirectory(
            System.IO.Path.Combine(downloadDirectory, "sub", "..", "..", "evil.exe"), downloadDirectory));
    }

    /// <summary>离线 fallback 必须带官方 SHA-512，避免无校验的提权安装。</summary>
    [Fact]
    public void RuntimeFallbackProvidesOfficialSha512()
    {
        var release = new WebRendererRuntimeReleaseFeed(Microsoft.Extensions.Logging.Abstractions.NullLogger<WebRendererRuntimeReleaseFeed>.Instance).GetFallback();

        Assert.Equal(WebRendererRuntimeReleaseFeed.KnownFallbackVersion, release.Version);
        Assert.NotNull(release.Sha512);
        Assert.Equal(128, release.Sha512!.Length);
        Assert.True(release.Sha512.All(Uri.IsHexDigit));
    }

    /// <summary>连接级固定解析必须拒绝解析到回环地址的主机。</summary>
    [Fact]
    public async Task AddressPolicyRejectsLoopbackResolution()
    {
        await Assert.ThrowsAsync<RemoteAssetException>(() =>
            AddressPolicy.ResolvePublicAddressesAsync("localhost", CancellationToken.None));
    }

    /// <summary>带结尾点、IPv6 区域标识的写法不能绕过主机名基础校验。</summary>
    [Theory]
    [InlineData("http://localhost./asset.png")]
    [InlineData("http://[fe80::1%25eth0]/asset.png")]
    [InlineData("http://user:pass@example.test/asset.png")]
    [InlineData("ftp://example.test/asset.png")]
    public void AddressPolicyRejectsNonStandardUris(string value)
    {
        Assert.Throws<RemoteAssetException>(() =>
            AddressPolicy.ValidateUri(new Uri(value)));
    }
}
