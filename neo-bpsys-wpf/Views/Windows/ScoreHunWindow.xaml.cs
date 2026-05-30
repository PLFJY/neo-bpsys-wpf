using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreHunWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("EA69B342-DDA6-4394-BDFD-13368D76A6BA", "ScoreHunWindow", true)]
public partial class ScoreHunWindow : FrontedWindowBase
{
    private const string BaseCanvasName = "BaseCanvas";
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ILogger<ScoreHunWindow>? _logger;
    private bool _hasRendered;

    public ScoreHunWindow()
    {
        InitializeComponent();
    }

    public ScoreHunWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ILogger<ScoreHunWindow> logger)
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

        try
        {
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(ScoreHunWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(ScoreHunWindow),
                    BaseCanvasName);
                return;
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreHunWindow),
                CanvasName = BaseCanvasName
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to render fronted v3 layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                nameof(ScoreHunWindow),
                BaseCanvasName);
        }
    }
}
