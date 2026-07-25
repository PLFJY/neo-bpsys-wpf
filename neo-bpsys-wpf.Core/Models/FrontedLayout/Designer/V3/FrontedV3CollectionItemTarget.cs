namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;

/// <summary>
/// PartCollection 集合项选中子目标，携带集合 Id 与项 Key 作为稳定身份。
/// </summary>
/// <remarks>
/// 该类型替代旧 Designer 中控件专用的 CollectionItem selection state（如
/// <c>SelectedGlobalScoreCell</c>、<c>SelectedGlobalScoreCellParentName</c>、
/// <c>SelectedGlobalScoreCellId</c>）。Phase 6 后所有集合项选中统一由
/// <see cref="FrontedV3DesignSelection"/> + <see cref="FrontedV3CollectionItemTarget"/> 表达。
/// </remarks>
public sealed class FrontedV3CollectionItemTarget : FrontedV3DesignSubTarget
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3CollectionItemTarget"/>。
    /// </summary>
    /// <param name="collectionId">集合标识。</param>
    /// <param name="itemKey">集合项唯一键。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="collectionId"/> 或 <paramref name="itemKey"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3CollectionItemTarget(string collectionId, string itemKey)
        : base(FrontedV3DesignSubTargetKind.CollectionItem)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(itemKey);
        CollectionId = collectionId;
        ItemKey = itemKey;
    }

    /// <summary>
    /// 获取集合标识。
    /// </summary>
    public string CollectionId { get; }

    /// <summary>
    /// 获取集合项唯一键。
    /// </summary>
    public string ItemKey { get; }
}
