using System.Windows;
using System.IO;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 插件提供的 WPF 前台窗口的描述符。
/// </summary>
/// <remarks>
/// <see cref="FullWindowType"/> 始终为 <c>plugin:{PackageId}/{WindowTypeName}</c>。
/// 该标识对应的用户布局存储在安全路径
/// <c>FrontedLayouts/plugin/{PackageId}/{WindowTypeName}</c> 下。
/// <see cref="FrontedWindowKind.PluginXaml"/> 窗口是普通插件 WPF 窗口，默认不可在设计器中编辑。
/// <see cref="FrontedWindowKind.PluginLayout"/> 窗口使用宿主布局渲染器。
/// 每个 v3 布局窗口内部固定一个 BaseCanvas。
/// </remarks>
public sealed class FrontedPluginWindowDescriptor : IFrontedWindowDescriptor
{
    /// <summary>
    /// 来自插件清单的插件包 ID。
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// 稳定的运行时窗口标识。生成一次 GUID 后在插件版本间保持不变。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 插件内部的窗口类型名，用于 <see cref="FullWindowType"/> 和默认布局路径。
    /// </summary>
    public required string WindowTypeName { get; init; }

    /// <summary>
    /// 布局/包标识，格式为 <c>plugin:{PackageId}/{WindowTypeName}</c>。
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
    /// 选择插件提供的是原始 XAML 窗口还是宿主渲染的设计器 v3 布局窗口。
    /// </summary>
    public required FrontedWindowKind Kind { get; init; }

    /// <inheritdoc />
    public bool IsPlugin => true;

    /// <summary>
    /// <see cref="FrontedWindowKind.PluginXaml"/> 所要求的 WPF 窗口类型。
    /// </summary>
    public Type? WindowType { get; init; }

    /// <summary>
    /// 插件 XAML 窗口使用的可选 ViewModel 类型。
    /// </summary>
    public Type? ViewModelType { get; init; }

    /// <summary>
    /// 插件目录下包含默认设计器 v3 布局的文件夹。
    /// </summary>
    public string DefaultLayoutRoot { get; init; } = "FrontedLayouts";

    /// <summary>
    /// 已解析的插件安装目录，由宿主在验证和渲染前设置。
    /// </summary>
    public string? PluginFolder { get; set; }

    /// <summary>
    /// 允许插件布局窗口在没有内置默认布局 JSON 的情况下启动。
    /// </summary>
    public bool AllowBlankDefaultLayout { get; init; }

    /// <summary>
    /// 插件布局窗口的默认 WPF 窗口选项。
    /// </summary>
    public FrontedWindowLayoutOptions DefaultOptions { get; init; } = new();

    /// <summary>
    /// 在描述符被宿主注册表接受前进行验证。
    /// </summary>
    /// <param name="pluginFolder">用于默认布局检查的可选插件目录覆盖值。</param>
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
