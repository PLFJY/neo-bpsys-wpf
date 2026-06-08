using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreGlobalWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("3A4F66F7-BAC7-47AF-AC45-11657C50F7DD", "ScoreGlobalWindow", true)]
public partial class ScoreGlobalWindow : FrontedWindowBase
{
    private const string BaseCanvasName = "BaseCanvas";
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ISharedDataService? _sharedDataService;
    private readonly ILogger<ScoreGlobalWindow>? _logger;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private bool _hasRendered;
    private bool _isBoModeSubscribed;
    private bool _isReloadingLayout;
    private bool _reloadRequested;

    public ScoreGlobalWindow()
    {
        InitializeComponent();
    }

    public ScoreGlobalWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        ILogger<ScoreGlobalWindow> logger,
        IFrontedBehaviorRuntime? behaviorRuntime = null)
    {
        _layoutService = layoutService;
        _renderer = renderer;
        _sharedDataService = sharedDataService;
        _logger = logger;
        _behaviorRuntime = behaviorRuntime;

        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribeBoModeChanged();

        if (_hasRendered || _layoutService is null || _renderer is null)
        {
            return;
        }

        _hasRendered = true;

        await ReloadFrontedLayoutAsync();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        UnsubscribeBoModeChanged();
        DetachBehaviorHost();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        UnsubscribeBoModeChanged();
        DetachBehaviorHost();
    }

    public async Task ReloadFrontedLayoutAsync()
    {
        if (_isReloadingLayout)
        {
            _reloadRequested = true;
            return;
        }

        _isReloadingLayout = true;
        try
        {
            do
            {
                _reloadRequested = false;
                await ReloadFrontedLayoutCoreAsync();
            }
            while (_reloadRequested);
        }
        finally
        {
            _isReloadingLayout = false;
        }
    }

    private async Task ReloadFrontedLayoutCoreAsync()
    {
        if (_layoutService is null || _renderer is null)
        {
            return;
        }

        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow);
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(ScoreGlobalWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(ScoreGlobalWindow),
                    BaseCanvasName);
                return;
            }

            // Detach existing behavior host before re-rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.DetachAsync(windowId, BaseCanvasName);
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = windowId,
                WindowTypeName = nameof(ScoreGlobalWindow),
                CanvasName = BaseCanvasName
            });

            // Attach behavior host after rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
                {
                    WindowId = windowId,
                    WindowType = nameof(ScoreGlobalWindow),
                    CanvasName = BaseCanvasName,
                    RootCanvas = BaseCanvas,
                    CanvasConfig = config,
                    SharedDataService = _sharedDataService!,
                    IsDesignerPreview = false,
                    Logger = _logger
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to render fronted v3 layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                nameof(ScoreGlobalWindow),
                BaseCanvasName);
        }
    }

    private void DetachBehaviorHost()
    {
        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow);
            _ = _behaviorRuntime?.DetachAsync(windowId, BaseCanvasName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detach behavior host on ScoreGlobalWindow.");
        }
    }

    private void SubscribeBoModeChanged()
    {
        if (_sharedDataService is null || _isBoModeSubscribed)
        {
            return;
        }

        _sharedDataService.IsBo3ModeChanged += OnBoModeChanged;
        _isBoModeSubscribed = true;
    }

    private void UnsubscribeBoModeChanged()
    {
        if (_sharedDataService is null || !_isBoModeSubscribed)
        {
            return;
        }

        _sharedDataService.IsBo3ModeChanged -= OnBoModeChanged;
        _isBoModeSubscribed = false;
    }

    private void OnBoModeChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.CheckAccess())
        {
            _ = ReloadFrontedLayoutAsync();
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() => _ = ReloadFrontedLayoutAsync()));
    }
}
