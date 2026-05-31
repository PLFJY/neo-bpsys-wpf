using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for FrontedDesignerWindow.xaml.
/// </summary>
public partial class FrontedDesignerWindow : FluentWindow
{
    private readonly IFrontedRenderer? _renderer;
    private readonly ILogger<FrontedDesignerWindow>? _logger;
    private bool _isLoaded;
    private FrontedDesignerWindowViewModel? _viewModel;

    public FrontedDesignerWindow()
    {
        InitializeComponent();
    }

    public FrontedDesignerWindow(
        FrontedDesignerWindowViewModel viewModel,
        IFrontedRenderer renderer,
        ILogger<FrontedDesignerWindow> logger)
    {
        _renderer = renderer;
        _logger = logger;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        AttachViewModel();
        _viewModel?.ReloadLayoutCommand.Execute(null);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PreviewRenderRequested -= OnPreviewRenderRequested;
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _viewModel?.ReloadLayoutCommand.Execute(null);
    }

    private void AttachViewModel()
    {
        if (_viewModel is not null)
        {
            return;
        }

        if (DataContext is not FrontedDesignerWindowViewModel viewModel)
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.PreviewRenderRequested += OnPreviewRenderRequested;
    }

    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        if (_renderer is null || e.Config is null || e.Context is null)
        {
            ClearPreviewCanvas();
            return;
        }

        try
        {
            PreviewCanvas.Width = e.Config.CanvasWidth;
            PreviewCanvas.Height = e.Config.CanvasHeight;
            _renderer.RenderToCanvas(PreviewCanvas, e.Config, e.Context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to render fronted designer preview.");
            ClearPreviewCanvas();
            _viewModel?.ReportRenderFailure(ex);
        }
    }

    private void ClearPreviewCanvas()
    {
        PreviewCanvas.Children.Clear();
        PreviewCanvas.Background = null;
        PreviewCanvas.Width = 640;
        PreviewCanvas.Height = 360;
    }
}
