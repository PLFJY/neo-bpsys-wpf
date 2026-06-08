namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 对局进度文本显示模式。
/// </summary>
public enum GameProgressTextDisplayMode
{
    /// <summary>
    /// 单行显示，例如 "GAME 1 FIRST HALF"。
    /// </summary>
    Inline,

    /// <summary>
    /// 双行显示：GAME 1 和 FIRST HALF 分两行。
    /// </summary>
    TwoLine,

    /// <summary>
    /// 只显示半场信息的竖向模式。
    /// </summary>
    VerticalHalfOnly,

    /// <summary>
    /// 只显示局数的竖向模式。
    /// </summary>
    VerticalGameOnly,

    /// <summary>
    /// 局数和半场同时竖向显示，两组文本上下排列。
    /// </summary>
    VerticalGameAndHalf,

    /// <summary>
    /// 局数和半场竖向分离显示，中间可选分隔线。
    /// </summary>
    VerticalSeparatedGameAndHalf,

    /// <summary>
    /// 类似黑色 ribbon 竖条，只显示 Game 信息。
    /// </summary>
    RibbonGameOnly
}

/// <summary>
/// 竖向文本的语言模式。
/// </summary>
public enum GameProgressVerticalLanguageMode
{
    /// <summary>
    /// 根据当前 UI 文化自动选择最优策略。
    /// CJK 使用 Upright，非 CJK 使用 RotateBlock。
    /// </summary>
    Auto,

    /// <summary>
    /// 字符 upright 逐字纵向排列，适合 CJK。
    /// </summary>
    Upright,

    /// <summary>
    /// 整段文本旋转 90°，适合英文。
    /// </summary>
    RotateBlock,

    /// <summary>
    /// 逐字符纵向堆叠，适合英文。
    /// </summary>
    StackCharacters
}

/// <summary>
/// 拉丁文本竖向显示模式。
/// </summary>
public enum GameProgressLatinVerticalMode
{
    /// <summary>
    /// 整段文本旋转 90° 显示。
    /// </summary>
    RotateBlock,

    /// <summary>
    /// 逐字符纵向堆叠显示。
    /// </summary>
    StackCharacters
}

/// <summary>
/// 数字风格。
/// </summary>
public enum GameProgressNumberStyle
{
    /// <summary>
    /// 根据当前 UI 文化自动选择。
    /// </summary>
    Auto,

    /// <summary>
    /// 阿拉伯数字（1, 2, 3...）。
    /// </summary>
    Arabic,

    /// <summary>
    /// CJK 数字（一, 二, 三...）。
    /// </summary>
    CjkNumeral
}

/// <summary>
/// 竖向文本方向。
/// </summary>
public enum GameProgressVerticalDirection
{
    /// <summary>
    /// 自动选择。CJK → <see cref="FacingLeft"/>，非 CJK → <see cref="FacingLeft"/>。
    /// </summary>
    Auto,

    /// <summary>
    /// 朝左：RotateBlock 逆时针旋转 90°（文本向上阅读），竖排文字水平靠右对齐。
    /// </summary>
    FacingLeft,

    /// <summary>
    /// 朝右：RotateBlock 顺时针旋转 90°（文本向下阅读），竖排文字水平靠左对齐。
    /// </summary>
    FacingRight
}

/// <summary>
/// 对局半场标识。
/// </summary>
public enum GameProgressHalf
{
    /// <summary>
    /// 上半场。
    /// </summary>
    First,

    /// <summary>
    /// 下半场。
    /// </summary>
    Second
}
