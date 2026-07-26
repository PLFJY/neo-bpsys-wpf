using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
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
    private readonly WebRendererLifecycleOperationCoordinator _lifecycleCoordinator;
    private readonly WebRendererRuntimeSetupService _runtimeSetupService;
    private readonly IGlobalRestartService _globalRestartService;
    private readonly WebRendererRuntimeDetector _runtimeDetector;
    private string _host;
    private double _port;
    private bool _startWithApplication;
    private bool _logProtocol;
    private double _exitTimeoutMs;
    private double _enterTimeoutMs;

    /// <summary>初始化 ViewModel。</summary>
    public WebRendererManagementViewModel(WebRendererSidecarService service, WebRendererSettingsStore settingsStore,
        IFrontedWindowRegistry windowRegistry, ISettingsHostService settingsHostService,
        WebRendererLifecycleOperationCoordinator lifecycleCoordinator,
        WebRendererRuntimeSetupService runtimeSetupService, IGlobalRestartService globalRestartService,
        WebRendererRuntimeDetector runtimeDetector)
    {
        _service = service; _settingsStore = settingsStore;
        _windowRegistry = windowRegistry; _settingsHostService = settingsHostService; _lifecycleCoordinator = lifecycleCoordinator;
        _runtimeSetupService = runtimeSetupService; _globalRestartService = globalRestartService; _runtimeDetector = runtimeDetector;
        _host = settingsStore.Settings.Host; _port = settingsStore.Settings.Port;
        _startWithApplication = settingsStore.Settings.StartWithApplication; _logProtocol = settingsStore.Settings.LogProtocol;
        _exitTimeoutMs = settingsStore.Settings.ExitTimeoutMs; _enterTimeoutMs = settingsStore.Settings.EnterTimeoutMs;
        _service.StatusChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        _lifecycleCoordinator.StateChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        _runtimeSetupService.StatusChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshRuntimeSetup);
        Refresh();
        _ = DetectRuntimeAsync();
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
    /// <summary>获取生命周期命令是否正在运行。</summary>
    public bool IsLifecycleOperationRunning { get; private set; }
    /// <summary>获取当前生命周期操作文本。</summary>
    public string LifecycleOperationText { get; private set; } = string.Empty;
    /// <summary>获取是否正在等待主程序发布窗口 bootstrap。</summary>
    public bool IsWaitingForWindows => !_service.HasBootstrapSnapshot;

    /// <summary>获取是否检测到 ASP.NET Core Runtime 缺失，决定引导区域是否可见。</summary>
    public bool IsRuntimeMissing { get; private set; }

    /// <summary>获取 runtime 安装引导流程是否忙。</summary>
    public bool IsRuntimeSetupBusy { get; private set; }

    /// <summary>获取 runtime 安装引导流程当前阶段。</summary>
    public WebRendererRuntimeSetupState RuntimeSetupState { get; private set; }

    /// <summary>获取 runtime installer 下载进度（0-100）。</summary>
    public double RuntimeDownloadProgress { get; private set; }

    /// <summary>获取 runtime 安装引导流程的当前状态文案。</summary>
    public string? RuntimeSetupMessage { get; private set; }

    /// <summary>获取是否已安装完成并等待重启。</summary>
    public bool IsRuntimeAwaitingRestart { get; private set; }

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
    [RelayCommand(CanExecute = nameof(CanStartRuntimeSetup))]
    private async Task DownloadAndInstallRuntimeAsync() => await _runtimeSetupService.RunSetupAsync(CancellationToken.None);
    [RelayCommand] private void OpenRuntimeDownloadPage() => Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/10.0") { UseShellExecute = true });
    [RelayCommand(CanExecute = nameof(CanStartRuntimeSetup))]
    private async Task RecheckRuntimeAsync() => await DetectRuntimeAsync();

    private bool CanStartRuntimeSetup() => !IsRuntimeSetupBusy;
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
        IsLifecycleOperationRunning = _lifecycleCoordinator.IsLifecycleOperationRunning;
        LifecycleOperationText = _lifecycleCoordinator.CurrentOperation ?? string.Empty;
        RebuildWindowLinks();
        foreach (var name in new[] { nameof(ServiceState), nameof(LocalUrl), nameof(LanUrl), nameof(ClientCount), nameof(ActivePackageId), nameof(Windows), nameof(LastError), nameof(IsLifecycleOperationRunning), nameof(LifecycleOperationText), nameof(IsWaitingForWindows) }) OnPropertyChanged(name);
    }

    private void RefreshRuntimeSetup()
    {
        var setupStatus = _runtimeSetupService.Status;
        RuntimeSetupState = setupStatus.State;
        RuntimeDownloadProgress = setupStatus.DownloadProgress;
        IsRuntimeSetupBusy = setupStatus.IsBusy;
        IsRuntimeAwaitingRestart = setupStatus.State == WebRendererRuntimeSetupState.AwaitingRestart;
        RuntimeSetupMessage = BuildRuntimeSetupMessage(setupStatus);
        foreach (var name in new[] { nameof(RuntimeSetupState), nameof(RuntimeDownloadProgress), nameof(IsRuntimeSetupBusy), nameof(IsRuntimeAwaitingRestart), nameof(RuntimeSetupMessage) })
            OnPropertyChanged(name);
        DownloadAndInstallRuntimeCommand.NotifyCanExecuteChanged();
        RecheckRuntimeCommand.NotifyCanExecuteChanged();
    }

    private static string? BuildRuntimeSetupMessage(WebRendererRuntimeSetupStatus status) => status.State switch
    {
        WebRendererRuntimeSetupState.Idle => null,
        WebRendererRuntimeSetupState.FetchingRelease => "正在查询最新版本...",
        WebRendererRuntimeSetupState.Downloading => $"正在下载 ASP.NET Core Runtime {status.PendingVersion}... {status.DownloadProgress:0}%",
        WebRendererRuntimeSetupState.Verifying => "正在校验 installer 完整性...",
        WebRendererRuntimeSetupState.Installing => "正在安装（请在 UAC 弹窗中确认）...",
        WebRendererRuntimeSetupState.AwaitingRestart => "安装完成。请点击应用右上角的重启按钮以启动 Web 前台。",
        WebRendererRuntimeSetupState.Failed => status.ErrorMessage,
        _ => null
    };

    private async Task DetectRuntimeAsync()
    {
        try
        {
            var result = await _runtimeDetector.DetectAsync().ConfigureAwait(false);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                // 安装完成等待重启时不覆盖 IsRuntimeMissing，引导区域继续显示"等待重启"文案
                if (RuntimeSetupState == WebRendererRuntimeSetupState.AwaitingRestart)
                    return;
                IsRuntimeMissing = !result.IsAvailable;
                OnPropertyChanged(nameof(IsRuntimeMissing));
            });
        }
        catch (Exception)
        {
            // 检测失败保持当前状态，避免误报
        }
    }

    private void RebuildWindowLinks()
    {
        var hasBootstrap = _service.HasBootstrapSnapshot;
        var published = _service.GetPublishedWindows()
            .ToDictionary(window => window.FullWindowType, StringComparer.OrdinalIgnoreCase);
        var settings = _settingsHostService.Settings;
        var items = _windowRegistry.GetV3LayoutWindows()
            .OrderBy(registration => registration.LocalId, StringComparer.OrdinalIgnoreCase)
            .Where(registration => published.ContainsKey(registration.Id))
            .Select(registration =>
            {
                published.TryGetValue(registration.Id, out var snapshot);
                var name = FrontedWindowDisplayNameResolver.ResolveDisplayName(
                    registration, settings.Language, settings.CultureInfo);
                var url = $"{LocalUrl.TrimEnd('/')}/render/{EncodeWindowType(registration.Id)}";
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
