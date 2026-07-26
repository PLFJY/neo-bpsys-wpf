using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 CutScene 对局进度文本业务控件配置。
/// </summary>
public class GameProgressTextControlConfig : FrontedControlConfigBase, IFrontedTextStyleConfig
{
    /// <summary>
    /// 初始化对局进度文本控件配置。
    /// </summary>
    public GameProgressTextControlConfig()
    {
        ControlType = "GameProgressText";
    }

    /// <summary>
    /// 字体族。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// 字重。
    /// </summary>
    public string? FontWeight { get; set; }

    /// <summary>
    /// 文本颜色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 文本颜色绑定路径；有值时优先使用绑定颜色，静态 <see cref="Color"/> 仅作为回退。
    /// </summary>
    public string? ColorBindingPath { get; set; }

    /// <summary>
    /// 字号。
    /// </summary>
    public double FontSize { get; set; }

    /// <summary>
    /// 文本对齐。
    /// </summary>
    public string? TextAlignment { get; set; }

    /// <summary>
    /// 文本块水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 文本块垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    // ============================================================
    // 显示模式
    // ============================================================

    /// <summary>
    /// 显示模式。
    /// 默认为 <see cref="GameProgressTextDisplayMode.Inline"/>。
    /// </summary>
    public GameProgressTextDisplayMode DisplayMode { get; set; } = GameProgressTextDisplayMode.Inline;

    /// <summary>
    /// 竖排文本的语言模式。
    /// 默认为 <see cref="GameProgressVerticalLanguageMode.Auto"/>。
    /// </summary>
    public GameProgressVerticalLanguageMode VerticalLanguageMode { get; set; } = GameProgressVerticalLanguageMode.Auto;

    /// <summary>
    /// 拉丁文本竖向显示模式。
    /// 默认为 <see cref="GameProgressLatinVerticalMode.RotateBlock"/>。
    /// </summary>
    public GameProgressLatinVerticalMode LatinVerticalMode { get; set; } = GameProgressLatinVerticalMode.RotateBlock;

    /// <summary>
    /// 竖向文本方向。
    /// 默认为 <see cref="GameProgressVerticalDirection.Auto"/>。
    /// </summary>
    public GameProgressVerticalDirection VerticalDirection { get; set; } = GameProgressVerticalDirection.Auto;

    /// <summary>
    /// 文本显示语言。
    /// 默认为 <see cref="LanguageKey.FollowApp"/>。
    /// </summary>
    public LanguageKey DisplayLanguage { get; set; } = LanguageKey.FollowApp;

    /// <summary>
    /// 数字风格。
    /// 默认为 <see cref="GameProgressNumberStyle.Auto"/>。
    /// </summary>
    public GameProgressNumberStyle NumberStyle { get; set; } = GameProgressNumberStyle.Auto;

    // ============================================================
    // 新增：间距
    // ============================================================

    /// <summary>
    /// 竖排模式下字符间距。
    /// </summary>
    public double VerticalTextSpacing { get; set; }

    /// <summary>
    /// 竖排分组模式中 Game 组和 Half 组的间距。
    /// </summary>
    public double GroupSpacing { get; set; } = 8;

    // ============================================================
    // 新增：分隔线
    // ============================================================

    /// <summary>
    /// 是否显示分隔线（仅 <see cref="VerticalSeparatedGameAndHalf"/> 模式）。
    /// </summary>
    public bool ShowSeparator { get; set; }

    /// <summary>
    /// 分隔线粗细。
    /// </summary>
    public double SeparatorThickness { get; set; } = 1;

    /// <summary>
    /// 分隔线颜色。
    /// 格式为 #RRGGBB 或 #AARRGGBB 或 WPF 颜色名称。
    /// </summary>
    public string? SeparatorColor { get; set; }

    // ============================================================
    // 新增：背景
    // ============================================================

    /// <summary>
    /// 背景颜色。
    /// 格式为 #RRGGBB 或 #AARRGGBB 或 WPF 颜色名称。
    /// 为空时不设置背景。
    /// </summary>
    public string? BackgroundColor { get; set; }

    // ============================================================
    // 新增：内边距（拆分为四个方向以兼容 PropertyGrid）
    // ============================================================

    /// <summary>
    /// 左边距。
    /// </summary>
    public double PaddingLeft { get; set; }

    /// <summary>
    /// 上边距。
    /// </summary>
    public double PaddingTop { get; set; }

    /// <summary>
    /// 右边距。
    /// </summary>
    public double PaddingRight { get; set; }

    /// <summary>
    /// 下边距。
    /// </summary>
    public double PaddingBottom { get; set; }

}
