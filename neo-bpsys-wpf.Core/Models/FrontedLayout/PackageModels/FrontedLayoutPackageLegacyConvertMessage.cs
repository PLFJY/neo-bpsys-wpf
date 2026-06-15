using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// Structured message emitted while converting a legacy <c>.bpui</c> package.
/// </summary>
public sealed class FrontedLayoutPackageLegacyConvertMessage
{
    /// <summary>
    /// Stable message code used for localization and diagnostics.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Message severity.
    /// </summary>
    public FrontedLayoutPackageLegacyConvertMessageSeverity Severity { get; set; }

    /// <summary>
    /// Template arguments keyed by argument name.
    /// </summary>
    public Dictionary<string, string> Args { get; set; } = [];

    /// <summary>
    /// Localized or fallback message text.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
