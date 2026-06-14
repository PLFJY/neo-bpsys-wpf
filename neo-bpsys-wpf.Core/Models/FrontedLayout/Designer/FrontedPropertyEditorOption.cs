namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Display option for enum-like Designer v3 property editors.
/// </summary>
public sealed class FrontedPropertyEditorOption
{
    /// <summary>
    /// 选项值。
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 选项显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}
