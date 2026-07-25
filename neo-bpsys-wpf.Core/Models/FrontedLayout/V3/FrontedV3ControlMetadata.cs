namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

/// <summary>
/// v3 前台控件在 Designer 与运行时的元数据，由 <see cref="FrontedV3ControlAttribute"/> 推导并
/// 通过 <see cref="FrontedV3ControlRegistration.Metadata"/> 暴露。
/// </summary>
/// <remarks>
/// <para>
/// 该类型只描述控件在 UI 上的"如何展示与创建"，不参与 JSON 序列化，也不决定属性读写位置。
/// 字段全部可选，缺失时由调用方按合理默认值回退（如显示名回退到 <c>LocalControlId</c>）。
/// </para>
/// <para>
/// <see cref="DefaultWidth"/>/<see cref="DefaultHeight"/> 用于新添加控件时的初始根尺寸；
/// 为 <see langword="null"/> 时由 Designer 按最小命中框回退。
/// </para>
/// </remarks>
public sealed class FrontedV3ControlMetadata
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3ControlMetadata"/>。
    /// </summary>
    public FrontedV3ControlMetadata()
    {
    }

    /// <summary>
    /// 控件在 Designer 控件目录中显示的本地化键；为 <see langword="null"/> 时回退到
    /// <see cref="FrontedV3ControlRegistration.LocalControlId"/>。
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 控件描述的本地化键；为 <see langword="null"/> 时 Designer 不显示描述。
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 控件在 Designer 控件目录中显示的图标资源名（WPF-UI Symbol 或资源键）；为 <see langword="null"/> 时显示默认图标。
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 新添加控件时的默认根宽度；为 <see langword="null"/> 时由 Designer 按最小命中框回退。
    /// </summary>
    public double? DefaultWidth { get; init; }

    /// <summary>
    /// 新添加控件时的默认根高度；为 <see langword="null"/> 时由 Designer 按最小命中框回退。
    /// </summary>
    public double? DefaultHeight { get; init; }

    /// <summary>
    /// 控件在 Designer 控件目录中的显示顺序；为 <see langword="null"/> 时按注册顺序追加。
    /// </summary>
    public int? DisplayOrder { get; init; }
}
