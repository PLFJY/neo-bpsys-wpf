using Microsoft.Extensions.Configuration;
using System.Net;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// Web Renderer 的启动选项。
/// </summary>
public sealed record WebRendererLaunchOptions(string Address, int Port, bool NoStart, string? ValidationError)
{
    /// <summary>默认监听地址。</summary>
    public const string DefaultAddress = "127.0.0.1";

    /// <summary>默认监听端口。</summary>
    public const int DefaultPort = 19527;

    /// <summary>
    /// 从宿主配置读取并验证选项。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <returns>已验证的启动选项。</returns>
    public static WebRendererLaunchOptions FromConfiguration(IConfiguration configuration)
    {
        var address = configuration["web-host"] ?? DefaultAddress;
        var portText = configuration["web-port"];
        var noStartText = configuration["web-no-start"];
        var noStart = bool.TryParse(noStartText, out var parsedNoStart)
            ? parsedNoStart
            : configuration.AsEnumerable().Any(pair => string.Equals(pair.Key, "web-no-start", StringComparison.OrdinalIgnoreCase));

        if (!IPAddress.TryParse(address, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return new(DefaultAddress, DefaultPort, noStart, "--web-host 必须是 IPv4 地址。");

        if (!IPAddress.IsLoopback(parsedAddress))
            return new(DefaultAddress, DefaultPort, noStart, "实时 Web Renderer 仅允许监听 localhost；局域网模式需要后续的鉴权支持。");

        var port = DefaultPort;
        if (!string.IsNullOrWhiteSpace(portText)
            && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
            return new(address, DefaultPort, noStart, "--web-port 必须是 1 到 65535 之间的端口号。");

        return new(address, port, noStart, null);
    }
}
