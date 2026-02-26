using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.CoopBpPlugin.Models;

/// <summary>
/// 插件设置
/// 存储插件的持久化配置
/// </summary>
public partial class PluginSettings : ObservableObject
{
    /// <summary>默认端口</summary>
    [ObservableProperty]
    private int _defaultPort = 9527;

    /// <summary>最大连接数</summary>
    [ObservableProperty]
    private int _maxConnections = 10;

    /// <summary>是否启用爆破保护</summary>
    [ObservableProperty]
    private bool _enableBruteForceProtection = true;

    /// <summary>爆破封禁时长（分钟）</summary>
    [ObservableProperty]
    private int _bruteForceBanDurationMinutes = 30;

    /// <summary>最大失败尝试次数</summary>
    [ObservableProperty]
    private int _maxFailedAttempts = 5;

    /// <summary>失败尝试窗口时间（分钟）</summary>
    [ObservableProperty]
    private int _failedAttemptsWindowMinutes = 5;

    /// <summary>上次连接的服务器IP</summary>
    [ObservableProperty]
    private string _lastConnectedIp = string.Empty;

    /// <summary>上次连接的服务器端口</summary>
    [ObservableProperty]
    private int _lastConnectedPort = 9527;
}
