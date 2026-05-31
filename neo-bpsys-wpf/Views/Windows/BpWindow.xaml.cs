using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

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
    private readonly ILogger<BpWindow>? _logger;
    private bool _hasRendered;

    public BpWindow()
    {
        InitializeComponent();
    }

    public BpWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ILogger<BpWindow> logger)
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
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(BpWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(BpWindow),
                    BaseCanvasName);
                return;
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow),
                CanvasName = BaseCanvasName
            });
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
}
