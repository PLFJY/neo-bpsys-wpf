using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.TeamJsonMaker;

/// <summary>队伍 JSON 导出中的选手信息。</summary>
public class Member
{
    /// <summary>初始化选手。</summary>
    /// <param name="camp">选手所属阵营。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="camp"/> 不是有效枚举值。</exception>
    public Member(Camp camp)
    {
        if (!Enum.IsDefined(camp))
        {
            throw new ArgumentOutOfRangeException(nameof(camp), camp, "Unknown camp value.");
        }

        Camp = camp;
    }

    /// <summary>
    /// 选手名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 选手所属阵营
    /// </summary>
    public Camp Camp { get; set; }

    /// <summary>
    /// 选手定妆照的图片 Uri
    /// </summary>
    public string? ImageUri { get; set; }
}