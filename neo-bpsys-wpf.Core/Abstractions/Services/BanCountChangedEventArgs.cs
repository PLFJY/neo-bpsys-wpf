using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Ban位数量改变事件参数
/// </summary>
/// <param name="banListName"></param>
/// <param name="index"></param>
public class BanCountChangedEventArgs(BanListName banListName, int index) : EventArgs
{
    /// <summary>
    /// Ban位名称
    /// </summary>
    public BanListName BanListName { get; } = banListName;
    /// <summary>
    /// Ban位索引
    /// </summary>
    public int Index { get; } = index;
}