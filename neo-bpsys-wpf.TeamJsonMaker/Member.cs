using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.TeamJsonMaker;

public class Member(Camp camp)
{
    /// <summary>
    /// 选手名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 选手所属阵营
    /// </summary>
    public Camp Camp { get; set; } = camp;

    /// <summary>
    /// 选手定妆照的图片 Uri
    /// </summary>
    public string? ImageUri { get; set; }
}