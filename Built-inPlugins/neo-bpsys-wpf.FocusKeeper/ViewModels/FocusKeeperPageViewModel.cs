using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.FocusKeeper.ViewModels;

/// <summary>
/// 焦点保持后台页面的视图模型。桥接 <see cref="IFocusKeeperService"/> 与 UI。
/// </summary>
public sealed partial class FocusKeeperPageViewModel : ViewModelBase
{
    private readonly IFocusKeeperService _service;

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartAsAdminCommand))]
    private bool _isCurrentProcessElevated;
    [ObservableProperty] private string? _targetDisplay;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private GameWindowInfo? _selectedWindow;

    /// <summary>初始化视图模型（设计时）。</summary>
#pragma warning disable CS8618
    public FocusKeeperPageViewModel() { }
#pragma warning restore CS8618

    /// <summary>初始化视图模型。</summary>
    /// <param name="service">焦点保持服务。</param>
    public FocusKeeperPageViewModel(IFocusKeeperService service)
    {
        _service = service;
        _service.PropertyChanged += OnServicePropertyChanged;
        SyncFromService();
        // 进入页面时自动刷新一次窗口列表，免去用户手动点击
        RefreshList();
    }

    /// <summary>可注入的窗口列表。</summary>
    public ObservableCollection<GameWindowInfo> GameWindows { get; } = new();

    /// <summary>是否有错误信息需要展示。</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>主程序是否未以管理员权限运行（用于显示提权提示）。</summary>
    public bool NeedsElevation => !IsCurrentProcessElevated;

    /// <summary>是否可以注入到选中的窗口。</summary>
    public bool CanAttachSelected => SelectedWindow is not null && !IsInstalled;

    /// <summary>状态显示文本。</summary>
    public string StatusText =>
        IsInstalled
            ? (IsEnabled ? "已注入 · 已启用" : "已注入 · 已禁用")
            : "未注入";

    /// <inheritdoc />
    partial void OnIsEnabledChanged(bool value)
    {
        _service.IsEnabled = value;
        OnPropertyChanged(nameof(StatusText));
    }

    /// <inheritdoc />
    partial void OnSelectedWindowChanged(GameWindowInfo? value)
        => OnPropertyChanged(nameof(CanAttachSelected));

    /// <inheritdoc />
    partial void OnErrorMessageChanged(string? value)
        => OnPropertyChanged(nameof(HasError));

    /// <inheritdoc />
    partial void OnIsCurrentProcessElevatedChanged(bool value)
        => OnPropertyChanged(nameof(NeedsElevation));

    /// <inheritdoc />
    partial void OnIsInstalledChanged(bool value)
        => OnPropertyChanged(nameof(CanAttachSelected));

    private void OnServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SyncFromService(e.PropertyName);
    }

    private void SyncFromService(string? propertyName = null)
    {
        if (propertyName is null || propertyName == nameof(IFocusKeeperService.IsInstalled))
            IsInstalled = _service.IsInstalled;
        if (propertyName is null || propertyName == nameof(IFocusKeeperService.IsCurrentProcessElevated))
            IsCurrentProcessElevated = _service.IsCurrentProcessElevated;
        if (propertyName is null || propertyName == nameof(IFocusKeeperService.IsEnabled))
            IsEnabled = _service.IsEnabled;
        if (propertyName is null || propertyName == nameof(IFocusKeeperService.TargetProcessName))
            TargetDisplay = _service.TargetProcessName is null
                ? null
                : $"{_service.TargetProcessName} (PID {_service.TargetProcessId})";
        if (propertyName is null || propertyName == nameof(IFocusKeeperService.ErrorMessage))
            ErrorMessage = _service.ErrorMessage;
    }

    /// <summary>自动查找第五人格并注入。</summary>
    [RelayCommand]
    private void AutoAttach()
    {
        _service.FindAndInstall();
    }

    /// <summary>
    /// 以管理员权限重启主程序。仅在未提权时可用；
    /// 若用户在 UAC 提示中拒绝，错误信息会同步到 <see cref="ErrorMessage"/>。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestartAsAdmin))]
    private void RestartAsAdmin()
    {
        bool ok = _service.RestartAsAdmin();
        if (!ok)
        {
            // 用本地化资源覆盖 service 层的硬编码消息
            ErrorMessage = Loc("ElevationCancelledMessage");
        }
    }

    private bool CanRestartAsAdmin() => NeedsElevation;

    private static string Loc(string key)
    {
        var culture = LocalizeDictionary.CurrentCulture;
        var value = LocalizeDictionary.Instance.GetLocalizedObject(
            "neo-bpsys-wpf.FocusKeeper",
            "neo_bpsys_wpf.FocusKeeper.Locales.FocusKeeper",
            key,
            culture);
        return value?.ToString() ?? key;
    }

    /// <summary>刷新可选窗口列表。</summary>
    [RelayCommand]
    private void RefreshList()
    {
        GameWindows.Clear();
        foreach (var w in _service.EnumerateGameWindows())
            GameWindows.Add(w);
    }

    /// <summary>卸载当前注入。</summary>
    [RelayCommand]
    private void Detach()
    {
        _service.Uninstall();
    }

    /// <summary>注入到选中的窗口。</summary>
    [RelayCommand]
    private void AttachToSelected()
    {
        if (SelectedWindow is null) return;
        _service.Install(SelectedWindow.Handle);
    }
}
