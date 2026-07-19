using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.WebRenderer.Services;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using System.Text.Json;
using System.IO;
using System.Collections.ObjectModel;
using System.Text;

namespace neo_bpsys_wpf.WebRenderer;

/// <summary>Web Renderer 管理页的状态与命令。</summary>
public sealed partial class WebRendererManagementViewModel : ViewModelBase
{
    private readonly WebRendererSidecarService _service;
    private readonly WebRendererSettingsStore _settingsStore;
    private readonly IFrontedWindowRegistry _windowRegistry;
    private readonly ISettingsHostService _settingsHostService;
    private string _host;
    private double _port;
    private bool _startWithApplication;
    private bool _logProtocol;
    private double _exitTimeoutMs;
    private double _enterTimeoutMs;

    /// <summary>初始化 ViewModel。</summary>
    public WebRendererManagementViewModel(WebRendererSidecarService service, WebRendererSettingsStore settingsStore,
        IFrontedWindowRegistry windowRegistry, ISettingsHostService settingsHostService)
    {
        _service = service; _settingsStore = settingsStore;
        _windowRegistry = windowRegistry; _settingsHostService = settingsHostService;
        _host = settingsStore.Settings.Host; _port = settingsStore.Settings.Port;
        _startWithApplication = settingsStore.Settings.StartWithApplication; _logProtocol = settingsStore.Settings.LogProtocol;
        _exitTimeoutMs = settingsStore.Settings.ExitTimeoutMs; _enterTimeoutMs = settingsStore.Settings.EnterTimeoutMs;
        _service.StatusChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    /// <summary>获取或设置监听地址。</summary>
    public string Host { get => _host; set => SetProperty(ref _host, value); }
    /// <summary>获取或设置监听端口。</summary>
    public double Port { get => _port; set => SetProperty(ref _port, value); }
    /// <summary>获取或设置随应用启动选项。</summary>
    public bool StartWithApplication { get => _startWithApplication; set => SetProperty(ref _startWithApplication, value); }
    /// <summary>获取或设置协议日志选项。</summary>
    public bool LogProtocol { get => _logProtocol; set => SetProperty(ref _logProtocol, value); }
    /// <summary>获取或设置 Exit fail-open 超时（毫秒）。</summary>
    public double ExitTimeoutMs { get => _exitTimeoutMs; set => SetProperty(ref _exitTimeoutMs, value); }
    /// <summary>获取或设置 Enter fail-open 超时（毫秒）。</summary>
    public double EnterTimeoutMs { get => _enterTimeoutMs; set => SetProperty(ref _enterTimeoutMs, value); }
    /// <summary>获取服务状态文本。</summary>
    public string ServiceState { get; private set; } = "未启动";
    /// <summary>获取本机访问 URL。</summary>
    public string LocalUrl { get; private set; } = string.Empty;
    /// <summary>获取局域网访问 URL。</summary>
    public string LanUrl { get; private set; } = string.Empty;
    /// <summary>获取客户端数量。</summary>
    public int ClientCount { get; private set; }
    /// <summary>获取活动包标识。</summary>
    public string ActivePackageId { get; private set; } = "-";
    /// <summary>获取公开窗口文本。</summary>
    public string Windows { get; private set; } = "-";
    /// <summary>获取最近错误。</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>可直接用于 OBS 浏览器源的 Web 前台地址列表。</summary>
    public ObservableCollection<WebRendererWindowLink> WindowLinks { get; } = [];

    [RelayCommand] private Task StartAsync() => _service.StartRendererAsync();
    [RelayCommand] private Task StopAsync() => _service.StopRendererAsync();
    [RelayCommand] private Task RestartAsync() => _service.RestartRendererAsync();
    [RelayCommand] private void CopyUrl() => Clipboard.SetText(LocalUrl);
    [RelayCommand] private void CopyWindowUrl(WebRendererWindowLink? window)
    {
        if (window is not null)
            Clipboard.SetText(window.Url);
    }
    [RelayCommand] private void OpenUrl() => Process.Start(new ProcessStartInfo(LocalUrl) { UseShellExecute = true });
    [RelayCommand] private void ExportDiagnostics()
    {
        var dialog = new SaveFileDialog { Filter = "JSON 文件 (*.json)|*.json", DefaultExt = ".json", FileName = "web-renderer-diagnostics" };
        if (dialog.ShowDialog() != true) return;
        var status = _service.Status;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new
        {
            status.IsRunning, status.ProcessId, status.Address, status.Port, status.ClientCount,
            status.BootstrapGeneration, status.LogProtocol, status.ActivePackageId, status.Windows, status.LastError
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    [RelayCommand] private async Task SaveAsync()
    {
        _settingsStore.Settings.Host = Host; _settingsStore.Settings.Port = (int)Port;
        _settingsStore.Settings.StartWithApplication = StartWithApplication; _settingsStore.Settings.LogProtocol = LogProtocol;
        _settingsStore.Settings.ExitTimeoutMs = (int)ExitTimeoutMs; _settingsStore.Settings.EnterTimeoutMs = (int)EnterTimeoutMs;
        _settingsStore.Save(); _service.ApplySettings(_settingsStore.Settings); await _service.RestartRendererAsync();
    }

    private void Refresh()
    {
        var status = _service.Status;
        ServiceState = status.IsRunning
            ? $"{status.LifecycleState} (PID {status.ProcessId})"
            : status.LifecycleState.ToString();
        LocalUrl = $"http://127.0.0.1:{status.Port}/";
        LanUrl = $"http://{status.Address}:{status.Port}/";
        ClientCount = status.ClientCount; ActivePackageId = status.ActivePackageId ?? "-";
        Windows = status.Windows.Count == 0 ? "-" : string.Join(", ", status.Windows); LastError = status.LastError ?? string.Empty;
        RebuildWindowLinks();
        foreach (var name in new[] { nameof(ServiceState), nameof(LocalUrl), nameof(LanUrl), nameof(ClientCount), nameof(ActivePackageId), nameof(Windows), nameof(LastError) }) OnPropertyChanged(name);
    }

    private void RebuildWindowLinks()
    {
        var hasBootstrap = _service.HasBootstrapSnapshot;
        var published = _service.GetPublishedWindows()
            .ToDictionary(window => window.FullWindowType, StringComparer.OrdinalIgnoreCase);
        var settings = _settingsHostService.Settings;
        var items = _windowRegistry.GetCustomizableLayoutWindows()
            .OrderBy(window => window.DisplayOrder ?? int.MaxValue)
            .Where(descriptor => published.ContainsKey(descriptor.FullWindowType))
            .Select(descriptor =>
            {
                published.TryGetValue(descriptor.FullWindowType, out var snapshot);
                var name = FrontedWindowDisplayNameResolver.ResolveDisplayName(
                    descriptor, settings.Language, settings.CultureInfo);
                var url = $"{LocalUrl.TrimEnd('/')}/render/{EncodeWindowType(descriptor.FullWindowType)}";
                return new WebRendererWindowLink(
                    name,
                    url,
                    snapshot?.CanvasWidth,
                    snapshot?.CanvasHeight,
                    hasBootstrap && snapshot is not null,
                    snapshot?.IsLayoutAvailable == true,
                    snapshot?.Diagnostics.FirstOrDefault());
            });
        WindowLinks.Clear();
        foreach (var item in items)
            WindowLinks.Add(item);
    }

    private static string EncodeWindowType(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>一个可复制的 Web 前台窗口地址。</summary>
public sealed record WebRendererWindowLink(string DisplayName, string Url, double? CanvasWidth,
    double? CanvasHeight, bool HasBootstrap, bool IsLayoutAvailable, string? Diagnostic)
{
    /// <summary>用于 OBS 的尺寸说明。</summary>
    public string ObsHint => CanvasWidth is > 0 && CanvasHeight is > 0
        ? $"分辨率：{CanvasWidth:0} × {CanvasHeight:0} ｜ OBS：浏览器源 → 粘贴 URL → 设置宽高"
        : !HasBootstrap
            ? "正在等待主程序发送当前布局。"
            : IsLayoutAvailable
            ? "OBS：浏览器源 → 粘贴 URL"
            : "当前活动布局包未提供该窗口的布局。";
}
