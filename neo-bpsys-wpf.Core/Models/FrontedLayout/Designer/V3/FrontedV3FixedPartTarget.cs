namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;

/// <summary>
/// 固定 Part 选中子目标，携带 Part Id 作为稳定身份。
/// </summary>
/// <remarks>
/// 该类型替代旧 Designer 中控件专用的 Part selection state（如
/// <c>SelectedMapV2InternalStylePart</c>）。Phase 6 后所有 Part 选中统一由
/// <see cref="FrontedV3DesignSelection"/> + <see cref="FrontedV3FixedPartTarget"/> 表达。
/// </remarks>
public sealed class FrontedV3FixedPartTarget : FrontedV3DesignSubTarget
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3FixedPartTarget"/>。
    /// </summary>
    /// <param name="partId">Part 标识。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="partId"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3FixedPartTarget(string partId)
        : base(FrontedV3DesignSubTargetKind.FixedPart)
    {
        ArgumentNullException.ThrowIfNull(partId);
        PartId = partId;
    }

    /// <summary>
    /// 获取 Part 标识。
    /// </summary>
    public string PartId { get; }
}
