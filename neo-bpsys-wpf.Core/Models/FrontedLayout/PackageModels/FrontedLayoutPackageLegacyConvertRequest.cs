using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 旧版前台布局包转换为新格式的请求。
/// </summary>
public sealed class FrontedLayoutPackageLegacyConvertRequest
{
    /// <summary>
    /// 旧版包文件路径。
    /// </summary>
    public string LegacyPackagePath { get; set; } = string.Empty;

    /// <summary>
    /// 包标识符。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 包名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 可选的包描述。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 可选的作者。
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 可选的最低支持版本。
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// 转换后是否安装。
    /// </summary>
    public bool InstallAfterConvert { get; set; }

    /// <summary>
    /// 安装后是否激活。
    /// </summary>
    public bool ActivateAfterInstall { get; set; }
}

/// <summary>
/// 旧版前台布局包转换结果。
/// </summary>
public sealed class FrontedLayoutPackageLegacyConvertResult
{
    /// <summary>
    /// 是否转换成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 转换后的包文件路径。
    /// </summary>
    public string? ConvertedPackagePath { get; set; }

    /// <summary>
    /// 安装后的包标识符。
    /// </summary>
    public string? InstalledPackageId { get; set; }

    /// <summary>
    /// 布局数量。
    /// </summary>
    public int LayoutCount { get; set; }

    /// <summary>
    /// 资源数量。
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// 信息消息列表。
    /// </summary>
    public IReadOnlyList<string> Infos { get; set; } = [];

    /// <summary>
    /// 警告消息列表。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];

    /// <summary>
    /// 诊断消息列表。
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; set; } = [];

    /// <summary>
    /// 不支持的属性列表。
    /// </summary>
    public IReadOnlyList<string> UnsupportedProperties { get; set; } = [];

    /// <summary>
    /// 缺失的资源列表。
    /// </summary>
    public IReadOnlyList<string> MissingResources { get; set; } = [];

    /// <summary>
    /// 结构化转换消息列表，包含消息代码、严重级别和参数。
    /// UI 应优先使用此属性，<see cref="Infos"/>、<see cref="Warnings"/>、<see cref="Diagnostics"/>
    /// 由此属性派生，仅用于向后兼容。
    /// </summary>
    public IReadOnlyList<FrontedLayoutPackageLegacyConvertMessage> Messages { get; set; } = [];

    /// <summary>
    /// 是否存在警告。
    /// </summary>
    public bool HasWarnings =>
        Warnings.Count > 0
        || UnsupportedProperties.Count > 0
        || MissingResources.Count > 0;

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 使用消息列表填充旧的字符串数组属性，并确保消息已本地化。
    /// </summary>
    /// <param name="result">要填充的结果对象。</param>
    /// <param name="messages">结构化消息列表。</param>
    public static void PopulateFromMessages(
        FrontedLayoutPackageLegacyConvertResult result,
        List<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        LegacyConvertMessageHelper.LocalizeAll(messages);
        result.Messages = messages.ToArray();
        result.Infos = messages
            .Where(m => m.Severity is FrontedLayoutPackageLegacyConvertMessageSeverity.Info
                or FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote)
            .Select(m => m.Message)
            .ToArray();
        result.Diagnostics = messages
            .Where(m => m.Severity is FrontedLayoutPackageLegacyConvertMessageSeverity.Info
                or FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote)
            .Select(m => m.Message)
            .ToArray();
        result.Warnings = messages
            .Where(m => m.Severity is FrontedLayoutPackageLegacyConvertMessageSeverity.Warning
                or FrontedLayoutPackageLegacyConvertMessageSeverity.Error)
            .Select(m => m.Message)
            .ToArray();
    }
}
