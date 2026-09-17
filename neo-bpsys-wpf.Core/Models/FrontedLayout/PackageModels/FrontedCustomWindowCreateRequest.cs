namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 创建布局包作用域内用户自定义 v3 窗口的请求。
/// </summary>
public sealed class FrontedCustomWindowCreateRequest
{
    /// <summary>
    /// 可选的窗口 ID；为空时由包管理器生成稳定安全的 ID。
    /// </summary>
    public string? WindowId { get; set; }

    /// <summary>
    /// 窗口显示名称字典，键为受支持的语言代码。
    /// </summary>
    public Dictionary<string, string> DisplayNames { get; set; } = new(StringComparer.Ordinal);
}
