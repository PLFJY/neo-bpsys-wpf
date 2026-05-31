using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
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
    private readonly ILogger<WidgetsWindow>? _logger;
    private bool _hasRendered;

    public WidgetsWindow()
    {
        InitializeComponent();
    }

    public WidgetsWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ILogger<WidgetsWindow> logger)
    {
        _layoutService = layoutService;
        _renderer = renderer;
        _logger = logger;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_hasRendered || _layoutService is null || _renderer is null)
        {
            return;
        }

        _hasRendered = true;

        await ReloadFrontedLayoutAsync();
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
            var config = await _layoutService!.LoadCanvasConfigAsync(nameof(WidgetsWindow), canvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(WidgetsWindow),
                    canvasName);
                return;
            }

            _renderer!.RenderToCanvas(canvas, config, new FrontedRenderContext
            {
                WindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.WidgetsWindow),
                CanvasName = canvasName
            });
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
}
