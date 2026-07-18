namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 属性网格编辑器类型。
/// </summary>
public enum FrontedPropertyEditorKind
{
    /// <summary>
    /// 纯文本编辑器。
    /// </summary>
    Text,

    /// <summary>
    /// 数值文本编辑器。
    /// </summary>
    Number,

    /// <summary>
    /// 布尔编辑器。
    /// </summary>
    Boolean,

    /// <summary>
    /// 开关式布尔编辑器。
    /// </summary>
    ToggleSwitch,

    /// <summary>
    /// 枚举选项编辑器。
    /// </summary>
    Enum,

    /// <summary>
    /// 颜色字符串编辑器。
    /// </summary>
    Color,

    /// <summary>
    /// 字体族下拉框编辑器。
    /// </summary>
    FontFamily,

    /// <summary>
    /// 文本绑定表达式的模态编辑器。
    /// </summary>
    TextBinding,

    /// <summary>
    /// 只读显示行。
    /// </summary>
    ReadOnly
}
