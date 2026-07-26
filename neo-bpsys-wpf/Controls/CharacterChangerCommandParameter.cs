namespace neo_bpsys_wpf.Controls;

/// <summary>
/// <see cref="CharacterChanger"/> 控件的命令参数，包含目标索引和来源索引。
/// </summary>
/// <param name="index">目标玩家索引。</param>
/// <param name="buttonContent">来源按钮内容（按钮的索引）。</param>
public class CharacterChangerCommandParameter(int index, int buttonContent)
{
    /// <summary>
    /// 获取或设置目标玩家索引。
    /// </summary>
    public int Target { get; set; } = index;

    /// <summary>
    /// 获取或设置来源按钮内容（按钮的索引）。
    /// </summary>
    public int Source { get; set; } = buttonContent;
}