namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;

/// <summary>
/// Designer 选中子目标的抽象基类，描述 Part 或 CollectionItem 的稳定身份。
/// </summary>
/// <remarks>
/// <para>
/// Phase 6 Designer 去特化后，子目标身份不再通过控件专用 selection state（如
/// <c>SelectedMapV2InternalStylePart</c>、<c>SelectedGlobalScoreCell</c>）维护，
/// 而是统一由 <see cref="FrontedV3DesignSelection.SubTarget"/> 携带。
/// </para>
/// <para>
/// 实现类型：
/// <list type="bullet">
/// <item><see cref="FrontedV3FixedPartTarget"/>：固定 Part 子目标，携带 Part Id。</item>
/// <item><see cref="FrontedV3CollectionItemTarget"/>：PartCollection 集合项子目标，携带集合 Id 与项 Key。</item>
/// </list>
/// </para>
/// </remarks>
public abstract class FrontedV3DesignSubTarget
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3DesignSubTarget"/>。
    /// </summary>
    /// <param name="kind">子目标类别。</param>
    protected FrontedV3DesignSubTarget(FrontedV3DesignSubTargetKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// 获取子目标类别。
    /// </summary>
    public FrontedV3DesignSubTargetKind Kind { get; }
}
