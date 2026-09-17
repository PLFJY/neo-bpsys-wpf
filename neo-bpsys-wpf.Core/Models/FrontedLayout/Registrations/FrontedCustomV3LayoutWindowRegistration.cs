namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

/// <summary>
/// 当前布局包作用域内的用户自定义 v3 窗口注册。
/// </summary>
public sealed class FrontedCustomV3LayoutWindowRegistration : FrontedV3LayoutWindowRegistration
{
    /// <summary>
    /// 当前自定义窗口所属的布局包 ID。
    /// </summary>
    public required string PackageScopeId { get; init; }

    /// <summary>
    /// 布局 JSON 中声明的多语言显示名称。
    /// </summary>
    public IReadOnlyDictionary<string, string> DisplayNames { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

}
