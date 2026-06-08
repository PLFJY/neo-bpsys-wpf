using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// WidgetsWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("712D2E21-B8DF-4220-8E3D-8AD0003DD079", "WidgetsWindow",
    ["MapBpCanvas", "BpOverViewCanvas", "MapV2Canvas"], true)]
public partial class WidgetsWindow : FrontedWindowBase
{
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ISharedDataService? _sharedDataService;
    private readonly ILogger<WidgetsWindow>? _logger;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private bool _hasRendered;
    private bool _isBoModeSubscribed;

    public WidgetsWindow()
    {
        InitializeComponent();
    }

    public WidgetsWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        ILogger<WidgetsWindow> logger,
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

        var canvases = new (string CanvasName, Canvas Canvas)[]
        {
            ("MapBpCanvas", MapBpCanvas),
            ("BpOverViewCanvas", BpOverViewCanvas),
            ("MapV2Canvas", MapV2Canvas)
        };

        foreach (var (canvasName, canvas) in canvases)
        {
            await RenderCanvasAsync(canvasName, canvas);
        }
    }

    private async Task RenderCanvasAsync(string canvasName, Canvas canvas)
    {
        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.WidgetsWindow);
            var config = await _layoutService!.LoadCanvasConfigAsync(nameof(WidgetsWindow), canvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(WidgetsWindow),
                    canvasName);
                return;
            }

            // Detach existing behavior host before re-rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.DetachAsync(windowId, canvasName);
            }

            _renderer!.RenderToCanvas(canvas, config, new FrontedRenderContext
            {
                WindowId = windowId,
                WindowTypeName = nameof(WidgetsWindow),
                CanvasName = canvasName
            });

            // Attach behavior host after rendering
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
                {
                    WindowId = windowId,
                    WindowType = nameof(WidgetsWindow),
                    CanvasName = canvasName,
                    RootCanvas = canvas,
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
                nameof(WidgetsWindow),
                canvasName);
        }
    }

    private void DetachBehaviorHost()
    {
        try
        {
            var windowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.WidgetsWindow);
            var canvases = new[] { "MapBpCanvas", "BpOverViewCanvas", "MapV2Canvas" };
            foreach (var canvasName in canvases)
            {
                _ = _behaviorRuntime?.DetachAsync(windowId, canvasName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detach behavior host on WidgetsWindow.");
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
