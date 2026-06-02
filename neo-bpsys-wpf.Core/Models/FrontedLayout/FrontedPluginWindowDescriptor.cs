using System.Windows;
using System.IO;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Descriptor for a plugin-provided fronted window.
/// </summary>
public sealed class FrontedPluginWindowDescriptor : IFrontedWindowDescriptor
{
    public required string PackageId { get; init; }

    public required string WindowId { get; init; }

    public required string WindowTypeName { get; init; }

    public string FullWindowType => $"plugin:{PackageId}/{WindowTypeName}";

    public string DisplayName { get; init; } = string.Empty;

    public string? DisplayNameKey { get; init; }

    public string? Description { get; init; }

    public string? DescriptionKey { get; init; }

    public required FrontedWindowKind Kind { get; init; }

    public bool IsPlugin => true;

    public Type? WindowType { get; init; }

    public Type? ViewModelType { get; init; }

    public string DefaultLayoutRoot { get; init; } = "FrontedLayouts";

    public string? PluginFolder { get; set; }

    public bool AllowBlankDefaultLayout { get; init; }

    public FrontedWindowLayoutOptions DefaultOptions { get; init; } = new();

    public IReadOnlyList<FrontedCanvasDescriptor> Canvases { get; init; } = [];

    public void Validate(string? pluginFolder = null)
    {
        if (string.IsNullOrWhiteSpace(PackageId))
        {
            throw new FrontedLayoutConfigException("Plugin fronted window PackageId is required.");
        }

        if (string.IsNullOrWhiteSpace(WindowId) || !Guid.TryParse(WindowId, out _))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin fronted window {PackageId}/{WindowTypeName} requires a stable GUID WindowId.");
        }

        if (string.IsNullOrWhiteSpace(WindowTypeName))
        {
            throw new FrontedLayoutConfigException($"Plugin fronted window {PackageId} requires WindowTypeName.");
        }

        if (Kind == FrontedWindowKind.PluginXaml
            && (WindowType is null || !typeof(Window).IsAssignableFrom(WindowType)))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin XAML window {FullWindowType} requires WindowType assignable to Window.");
        }

        if (Kind == FrontedWindowKind.PluginLayout && Canvases.Count == 0)
        {
            throw new FrontedLayoutConfigException($"Plugin layout window {FullWindowType} requires at least one canvas.");
        }

        if (Kind == FrontedWindowKind.PluginLayout && !AllowBlankDefaultLayout)
        {
            var root = pluginFolder ?? PluginFolder;
            if (!string.IsNullOrWhiteSpace(root))
            {
                foreach (var canvas in Canvases.Where(canvas => canvas.Customizable))
                {
                    var defaultPath = Path.Combine(
                        root,
                        DefaultLayoutRoot,
                        WindowTypeName,
                        $"{canvas.CanvasName}.json");
                    if (!File.Exists(defaultPath))
                    {
                        throw new FrontedLayoutConfigException(
                            $"Plugin layout window {FullWindowType}/{canvas.CanvasName} default layout is missing: {defaultPath}");
                    }
                }
            }
        }
    }
}
