using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 转换旧版 <c>.bpui</c> 包时发出的结构化消息。
/// </summary>
public sealed class FrontedLayoutPackageLegacyConvertMessage
{
    /// <summary>
    /// 用于本地化和诊断的稳定消息代码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 消息严重级别。
    /// </summary>
    public FrontedLayoutPackageLegacyConvertMessageSeverity Severity { get; set; }

    /// <summary>
    /// 以参数名为键的模板参数。
    /// </summary>
    public Dictionary<string, string> Args { get; set; } = [];

    /// <summary>
    /// 本地化或回退的消息文本。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
