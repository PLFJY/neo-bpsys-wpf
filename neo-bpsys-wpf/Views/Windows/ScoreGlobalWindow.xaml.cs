using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

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
    private readonly ILogger<ScoreGlobalWindow>? _logger;
    private bool _hasRendered;

    public ScoreGlobalWindow()
    {
        InitializeComponent();
    }

    public ScoreGlobalWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ILogger<ScoreGlobalWindow> logger)
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

        try
        {
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(ScoreGlobalWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(ScoreGlobalWindow),
                    BaseCanvasName);
                return;
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow),
                CanvasName = BaseCanvasName
            });
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
}
