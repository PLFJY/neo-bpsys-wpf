using Microsoft.Extensions.Configuration;
using System.Net;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// Web Renderer 的启动选项。
/// </summary>
public sealed record WebRendererLaunchOptions(string Address, int Port, bool NoStart, bool LogProtocol, string? ValidationError)
{
    /// <summary>
    /// 兼容旧调用方创建启动选项。
    /// </summary>
    /// <param name="address">监听地址。</param>
    /// <param name="port">监听端口。</param>
    /// <param name="noStart">是否禁止自动启动。</param>
    /// <param name="validationError">验证错误。</param>
    public WebRendererLaunchOptions(string address, int port, bool noStart, string? validationError)
        : this(address, port, noStart, false, validationError)
    {
    }

    /// <summary>Web Exit 确认的 fail-open 等待上限。</summary>
    public TimeSpan ExitTimeout { get; init; } = TimeSpan.FromMilliseconds(2000);

    /// <summary>Web Enter 确认的 fail-open 等待上限。</summary>
    public TimeSpan EnterTimeout { get; init; } = TimeSpan.FromMilliseconds(2000);
    /// <summary>默认监听地址。</summary>
    public const string DefaultAddress = "127.0.0.1";

    /// <summary>默认监听端口。</summary>
    public const int DefaultPort = 19527;

    /// <summary>
    /// 从宿主配置读取并验证选项。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="settings">插件私有持久化设置。</param>
    /// <returns>已验证的启动选项。</returns>
    public static WebRendererLaunchOptions FromConfiguration(IConfiguration configuration, WebRendererPluginSettings settings)
    {
        var address = configuration["web-host"] ?? configuration["host"] ?? settings.Host;
        var portText = configuration["web-port"] ?? configuration["port"];
        var commandNoStart = HasSwitch(configuration, "web-no-start");
        var noStart = commandNoStart || !settings.StartWithApplication;

        if (!IPAddress.TryParse(address, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return new(DefaultAddress, DefaultPort, noStart, settings.LogProtocol, "--web-host 必须是 IPv4 地址。");

        var port = settings.Port;
        if (!string.IsNullOrWhiteSpace(portText)
            && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
            return new(address, DefaultPort, noStart, settings.LogProtocol, "--web-port 必须是 1 到 65535 之间的端口号。");

        return new(address, port, noStart, settings.LogProtocol || HasSwitch(configuration, "web-log-protocol"), null)
        {
            ExitTimeout = ReadTimeout(configuration["web-transition-exit-timeout-ms"], settings.ExitTimeoutMs),
            EnterTimeout = ReadTimeout(configuration["web-transition-enter-timeout-ms"], settings.EnterTimeoutMs)
        };
    }

    /// <summary>
    /// 从宿主配置读取选项，使用默认的插件私有设置。
    /// 此重载保留给尚未接入插件设置存储的调用方。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <returns>已验证的启动选项。</returns>
    public static WebRendererLaunchOptions FromConfiguration(IConfiguration configuration) =>
        FromConfiguration(configuration, new WebRendererPluginSettings());

    private static bool HasSwitch(IConfiguration configuration, string key)
    {
        if (bool.TryParse(configuration[key], out var value))
            return value;

        if (configuration.AsEnumerable().Any(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)))
            return true;

        // AddCommandLine drops a bare option without a value. The actual host
        // process still has the original argument, so inspect it explicitly.
        return Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, $"--{key}", StringComparison.OrdinalIgnoreCase));
    }

    private static TimeSpan ReadTimeout(string? value, int fallback) =>
        int.TryParse(value, out var milliseconds) && milliseconds is > 0 and <= 30000
            ? TimeSpan.FromMilliseconds(milliseconds)
            : TimeSpan.FromMilliseconds(fallback is > 0 and <= 30000 ? fallback : 2000);
}
