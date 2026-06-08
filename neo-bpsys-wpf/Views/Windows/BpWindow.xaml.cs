using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// BpWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("ACFC0F23-83F4-4607-B473-24D7DB292D23", "BpWindow", true)]
public partial class BpWindow : FrontedWindowBase
{
    private const string BaseCanvasName = "BaseCanvas";
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ISharedDataService? _sharedDataService;
    private readonly ILogger<BpWindow>? _logger;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private bool _hasRendered;
    private bool _isBoModeSubscribed;

    public BpWindow()
    {
        InitializeComponent();
    }

    public BpWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        ILogger<BpWindow> logger,
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
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow);
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(BpWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(BpWindow),
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
                WindowTypeName = nameof(BpWindow),
                CanvasName = BaseCanvasName
            });

            // Attach behavior host after rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
                {
                    WindowId = windowId,
                    WindowType = nameof(BpWindow),
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
                nameof(BpWindow),
                BaseCanvasName);
        }
    }

    private void DetachBehaviorHost()
    {
        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow);
            _ = _behaviorRuntime?.DetachAsync(windowId, BaseCanvasName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detach behavior host on BpWindow.");
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
