using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// GameDataWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("25378080-2085-4121-BE9A-94E987455CEC", "GameDataWindow", true)]
public partial class GameDataWindow : FrontedWindowBase
{
    private const string BaseCanvasName = "BaseCanvas";
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ISharedDataService? _sharedDataService;
    private readonly ILogger<GameDataWindow>? _logger;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private bool _hasRendered;
    private bool _isBoModeSubscribed;

    public GameDataWindow()
    {
        InitializeComponent();
    }

    public GameDataWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        ILogger<GameDataWindow> logger,
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
        if (_layoutService is null || _renderer is null)
        {
            return;
        }

        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.GameDataWindow);
            var config = await _layoutService.LoadWindowConfigAsync(nameof(GameDataWindow));
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(GameDataWindow),
                    BaseCanvasName);
                return;
            }

            // Detach existing behavior host before re-rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.DetachAsync(windowId);
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = windowId,
                WindowTypeName = nameof(GameDataWindow),
                CanvasName = BaseCanvasName
            });

            // Attach behavior host after rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
                {
                    WindowId = windowId,
                    WindowType = nameof(GameDataWindow),
                    CanvasName = BaseCanvasName,
                    RootCanvas = BaseCanvas,
                    WindowConfig = config,
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
                nameof(GameDataWindow),
                BaseCanvasName);
        }
    }

    private void DetachBehaviorHost()
    {
        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.GameDataWindow);
            _ = _behaviorRuntime?.DetachAsync(windowId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detach behavior host on GameDataWindow.");
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
