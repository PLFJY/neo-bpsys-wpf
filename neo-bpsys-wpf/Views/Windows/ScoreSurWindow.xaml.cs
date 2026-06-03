using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreSurWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("4ED64F79-E47C-490D-B86A-AE396F279889", "ScoreSurWindow", true)]
public partial class ScoreSurWindow : FrontedWindowBase
{
    private const string BaseCanvasName = "BaseCanvas";
    private readonly IFrontedLayoutService? _layoutService;
    private readonly IFrontedRenderer? _renderer;
    private readonly ISharedDataService? _sharedDataService;
    private readonly ILogger<ScoreSurWindow>? _logger;
    private bool _hasRendered;
    private bool _isBoModeSubscribed;

    public ScoreSurWindow()
    {
        InitializeComponent();
    }

    public ScoreSurWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        ILogger<ScoreSurWindow> logger)
    {
        _layoutService = layoutService;
        _renderer = renderer;
        _sharedDataService = sharedDataService;
        _logger = logger;

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
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        UnsubscribeBoModeChanged();
    }

    public async Task ReloadFrontedLayoutAsync()
    {
        if (_layoutService is null || _renderer is null)
        {
            return;
        }

        try
        {
            var config = await _layoutService.LoadCanvasConfigAsync(nameof(ScoreSurWindow), BaseCanvasName);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 layout config not found. Window: {WindowTypeName}, Canvas: {CanvasName}",
                    nameof(ScoreSurWindow),
                    BaseCanvasName);
                return;
            }

            _renderer.RenderToCanvas(BaseCanvas, config, new FrontedRenderContext
            {
                WindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreSurWindow),
                WindowTypeName = nameof(ScoreSurWindow),
                CanvasName = BaseCanvasName
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to render fronted v3 layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                nameof(ScoreSurWindow),
                BaseCanvasName);
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
