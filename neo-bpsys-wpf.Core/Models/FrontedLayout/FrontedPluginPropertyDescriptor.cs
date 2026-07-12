using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 插件前台控件配置的声明式设计器属性元数据。
/// </summary>
public sealed class FrontedPluginPropertyDescriptor
{
    public required string PropertyName { get; init; }

    public string? DisplayNameKey { get; init; }

    public string? DescriptionKey { get; init; }

    public string GroupName { get; init; } = "Plugin";

    public FrontedPropertyEditorKind? EditorKind { get; init; }

    public IReadOnlyList<FrontedPropertyEditorOption>? Options { get; init; }

    public FrontedBindingTargetKind BindingTargetKind { get; init; } = FrontedBindingTargetKind.Any;

    public bool IsVisible { get; init; } = true;

    public bool IsReadOnly { get; init; }
}
