using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Host WPF output window for plugin-declared Designer v3 layouts.
/// </summary>
/// <remarks>
/// This window renders <see cref="FrontedWindowKind.PluginLayout"/> descriptors with the host v3 renderer.
/// It loads user layouts by <see cref="FrontedPluginWindowDescriptor.FullWindowType"/> first, then falls back
/// to JSON files under the plugin folder. Plugin XAML windows use their own WPF window type and do not use
/// this host.
/// </remarks>
public sealed class FrontedPluginLayoutWindow : FrontedWindowBase
{
    private readonly FrontedPluginWindowDescriptor _descriptor;
    private readonly IFrontedLayoutService _layoutService;
    private readonly IFrontedRenderer _renderer;
    private readonly ILogger<FrontedPluginLayoutWindow> _logger;
    private readonly Dictionary<string, Canvas> _canvases = new(StringComparer.Ordinal);
    private bool _hasRendered;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// Creates a host window for one plugin layout descriptor.
    /// </summary>
    public FrontedPluginLayoutWindow(
        FrontedPluginWindowDescriptor descriptor,
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ILogger<FrontedPluginLayoutWindow> logger)
    {
        _descriptor = descriptor;
        _layoutService = layoutService;
        _renderer = renderer;
        _logger = logger;

        Title = string.IsNullOrWhiteSpace(descriptor.DisplayName)
            ? descriptor.FullWindowType
            : descriptor.DisplayName;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        AllowsTransparency = descriptor.DefaultOptions.AllowTransparency;

        var root = new Grid();
        foreach (var canvasDescriptor in descriptor.Canvases)
        {
            var canvas = new Canvas
            {
                Name = canvasDescriptor.CanvasName,
                Width = canvasDescriptor.DefaultWidth,
                Height = canvasDescriptor.DefaultHeight,
                Background = Brushes.Transparent
            };
            root.Children.Add(canvas);
            _canvases[canvasDescriptor.CanvasName] = canvas;
        }

        var primaryCanvas = descriptor.Canvases.FirstOrDefault();
        Width = primaryCanvas?.DefaultWidth > 0 ? primaryCanvas.DefaultWidth : 1920D;
        Height = primaryCanvas?.DefaultHeight > 0 ? primaryCanvas.DefaultHeight : 1080D;
        Content = root;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasRendered)
        {
            return;
        }

        _hasRendered = true;
        await ReloadFrontedLayoutAsync();
    }

    /// <summary>
    /// Reloads and renders all canvases declared by the plugin layout descriptor.
    /// </summary>
    public async Task ReloadFrontedLayoutAsync()
    {
        foreach (var canvasDescriptor in _descriptor.Canvases)
        {
            if (!_canvases.TryGetValue(canvasDescriptor.CanvasName, out var canvas))
            {
                continue;
            }

            await RenderCanvasAsync(canvasDescriptor, canvas);
        }
    }

    private async Task RenderCanvasAsync(FrontedCanvasDescriptor canvasDescriptor, Canvas canvas)
    {
        try
        {
            var result = await _layoutService.LoadCanvasConfigWithMetadataAsync(
                _descriptor.FullWindowType,
                canvasDescriptor.CanvasName);
            var config = result.Config ?? await LoadPluginDefaultLayoutAsync(canvasDescriptor);
            if (config is null)
            {
                _logger.LogWarning(
                    "Plugin fronted layout config not found. Window: {FullWindowType}, Canvas: {CanvasName}",
                    _descriptor.FullWindowType,
                    canvasDescriptor.CanvasName);
                return;
            }

            _renderer.RenderToCanvas(canvas, config, new FrontedRenderContext
            {
                WindowId = _descriptor.WindowId,
                CanvasName = canvasDescriptor.CanvasName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to render plugin fronted v3 layout. Window: {FullWindowType}, Canvas: {CanvasName}",
                _descriptor.FullWindowType,
                canvasDescriptor.CanvasName);
        }
    }

    private async Task<FrontedCanvasConfig?> LoadPluginDefaultLayoutAsync(FrontedCanvasDescriptor canvasDescriptor)
    {
        if (string.IsNullOrWhiteSpace(_descriptor.PluginFolder))
        {
            return null;
        }

        var path = Path.Combine(
            _descriptor.PluginFolder,
            _descriptor.DefaultLayoutRoot,
            _descriptor.WindowTypeName,
            $"{canvasDescriptor.CanvasName}.json");
        // Plugin defaults live beside the plugin manifest/DLL so packaged plugin windows do not depend on app Resources.
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }
}
