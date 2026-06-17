using System.Windows;
using System.IO;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Descriptor for a plugin-provided WPF fronted window.
/// </summary>
/// <remarks>
/// <see cref="FullWindowType"/> is always <c>plugin:{PackageId}/{WindowTypeName}</c>.
/// User layouts for that identity are stored under the safe path
/// <c>FrontedLayouts/plugin/{PackageId}/{WindowTypeName}</c>.
/// <see cref="FrontedWindowKind.PluginXaml"/> windows are normal plugin WPF windows and are not
/// Designer-editable by default. <see cref="FrontedWindowKind.PluginLayout"/> windows use the host
/// layout renderer. Each v3 layout window has exactly one internal BaseCanvas.
/// </remarks>
public sealed class FrontedPluginWindowDescriptor : IFrontedWindowDescriptor
{
    /// <summary>
    /// Plugin package id from the plugin manifest.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Stable runtime window identity. Generate a GUID once and keep it unchanged between plugin releases.
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Plugin-local window type name, used in <see cref="FullWindowType"/> and default layout paths.
    /// </summary>
    public required string WindowTypeName { get; init; }

    /// <summary>
    /// Layout/package identity in the form <c>plugin:{PackageId}/{WindowTypeName}</c>.
    /// </summary>
    public string FullWindowType => $"plugin:{PackageId}/{WindowTypeName}";

    /// <inheritdoc />
    public string DisplayName { get; init; } = string.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames { get; init; }

    /// <inheritdoc />
    public string? DisplayNameKey { get; init; }

    /// <inheritdoc />
    public string? Description { get; init; }

    /// <inheritdoc />
    public string? DescriptionKey { get; init; }

    /// <inheritdoc />
    public string? GroupKey { get; init; }

    /// <inheritdoc />
    public int? DisplayOrder { get; init; }

    /// <inheritdoc />
    public bool IsVisibleInFrontManage { get; init; } = true;

    /// <inheritdoc />
    public bool IsV3LayoutWindow => Kind == FrontedWindowKind.PluginLayout;

    /// <inheritdoc />
    public bool Customizable { get; init; } = true;

    /// <summary>
    /// Selects whether the plugin contributes a raw XAML window or a host-rendered Designer v3 layout window.
    /// </summary>
    public required FrontedWindowKind Kind { get; init; }

    /// <inheritdoc />
    public bool IsPlugin => true;

    /// <summary>
    /// WPF window type required for <see cref="FrontedWindowKind.PluginXaml"/>.
    /// </summary>
    public Type? WindowType { get; init; }

    /// <summary>
    /// Optional ViewModel type used by plugin XAML windows.
    /// </summary>
    public Type? ViewModelType { get; init; }

    /// <summary>
    /// Folder under the plugin directory that contains default Designer v3 layouts.
    /// </summary>
    public string DefaultLayoutRoot { get; init; } = "FrontedLayouts";

    /// <summary>
    /// Resolved plugin installation folder, set by the host before validation and rendering.
    /// </summary>
    public string? PluginFolder { get; set; }

    /// <summary>
    /// Allows a plugin layout window to start without bundled default layout JSON.
    /// </summary>
    public bool AllowBlankDefaultLayout { get; init; }

    /// <summary>
    /// Default WPF window options for plugin layout windows.
    /// </summary>
    public FrontedWindowLayoutOptions DefaultOptions { get; init; } = new();

    /// <summary>
    /// Validates the descriptor before it is accepted by the host registry.
    /// </summary>
    /// <param name="pluginFolder">Optional plugin folder override used for default layout checks.</param>
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

        if (Kind == FrontedWindowKind.PluginLayout && !AllowBlankDefaultLayout)
        {
            var root = pluginFolder ?? PluginFolder;
            if (!string.IsNullOrWhiteSpace(root))
            {
                var defaultPath = Path.Combine(root, DefaultLayoutRoot, $"{WindowTypeName}.json");
                if (!File.Exists(defaultPath))
                {
                    throw new FrontedLayoutConfigException(
                        $"Plugin layout window {FullWindowType} default layout is missing: {defaultPath}");
                }
            }
        }
    }
}
