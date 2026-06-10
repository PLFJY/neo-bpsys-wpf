#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageLegacyConvertRequest
{
    public string LegacyPackagePath { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? MinVersion { get; set; }

    public bool InstallAfterConvert { get; set; }

    public bool ActivateAfterInstall { get; set; }
}

public sealed class FrontedLayoutPackageLegacyConvertResult
{
    public bool Success { get; set; }

    public string? ConvertedPackagePath { get; set; }

    public string? InstalledPackageId { get; set; }

    public int LayoutCount { get; set; }

    public int ResourceCount { get; set; }

    public IReadOnlyList<string> Infos { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public IReadOnlyList<string> Diagnostics { get; set; } = [];

    public IReadOnlyList<string> UnsupportedProperties { get; set; } = [];

    public IReadOnlyList<string> MissingResources { get; set; } = [];

    /// <summary>
    /// 结构化转换消息列表，包含消息代码、严重级别和参数。
    /// UI 应优先使用此属性，<see cref="Infos"/>、<see cref="Warnings"/>、<see cref="Diagnostics"/>
    /// 由此属性派生，仅用于向后兼容。
    /// </summary>
    public IReadOnlyList<FrontedLayoutPackageLegacyConvertMessage> Messages { get; set; } = [];

    public bool HasWarnings =>
        Warnings.Count > 0
        || UnsupportedProperties.Count > 0
        || MissingResources.Count > 0;

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 使用消息列表填充旧的字符串数组属性，并确保消息已本地化。
    /// </summary>
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

#pragma warning restore CS1591
