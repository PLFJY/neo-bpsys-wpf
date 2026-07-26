namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 旧版布局包转换消息的严重级别。
/// </summary>
public enum FrontedLayoutPackageLegacyConvertMessageSeverity
{
    /// <summary>
    /// 常规信息。
    /// </summary>
    Info,

    /// <summary>
    /// 兼容性说明。不是错误，仅提醒用户旧版布局中某些内容与新版行为的差异。
    /// </summary>
    CompatibilityNote,

    /// <summary>
    /// 警告，某些内容可能转换不完整，但整体转换仍成功。
    /// </summary>
    Warning,

    /// <summary>
    /// 错误，转换失败或某些关键内容无法转换。
    /// </summary>
    Error
}
