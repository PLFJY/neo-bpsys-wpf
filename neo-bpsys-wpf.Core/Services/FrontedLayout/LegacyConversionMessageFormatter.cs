#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.Text;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class LegacyConversionMessageFormatter
{
    private const int MaxUserSummaryItems = 3;

    /// <summary>
    /// 报告给普通用户的警告类别数量上限。
    /// </summary>
    private const int MaxUserSummaryCategories = 3;

    // Message codes that are considered benign and should not be shown as user-facing issues.
    private static readonly HashSet<string> BenignMessageCodes = new(StringComparer.Ordinal)
    {
        LegacyConvertMessageHelper.CodeResourceCopied,
        LegacyConvertMessageHelper.CodeGlobalScoreCellsAggregated,
        LegacyConvertMessageHelper.CodeIrregularCellSpacingApproximated,
        LegacyConvertMessageHelper.CodeControlGeometryFuzzyMatched,
        LegacyConvertMessageHelper.CodeOvertimeScoreCellsAggregated,
        LegacyConvertMessageHelper.CodeLockOverlayGeometryConsumed,
        LegacyConvertMessageHelper.CodeLockOverlayFolded,
        LegacyConvertMessageHelper.CodeFoldedControlConsumed,
        LegacyConvertMessageHelper.CodeLockImageMapped,
        LegacyConvertMessageHelper.CodePickingBorderImageMapped,
        LegacyConvertMessageHelper.CodeBo3GlobalScoreBackgroundMapped,
        LegacyConvertMessageHelper.CodeTextSettingsApplied,
        LegacyConvertMessageHelper.CodeMapV2PickingBorderMapped,
        LegacyConvertMessageHelper.CodeFoldedGeometryNotRepresentable,
    };

    /// <summary>
    /// 检查转换结果中是否存在普通用户需要知道的警告。
    /// </summary>
    public static bool HasUserFacingWarnings(FrontedLayoutPackageLegacyConvertResult result)
    {
        return result.Messages.Any(m =>
            m.Severity is FrontedLayoutPackageLegacyConvertMessageSeverity.Warning
                or FrontedLayoutPackageLegacyConvertMessageSeverity.Error
            && !BenignMessageCodes.Contains(m.Code));
    }

    /// <summary>
    /// 构建面向普通用户的摘要文本，按严重级别分组。
    /// </summary>
    public static string BuildUserSummary(FrontedLayoutPackageLegacyConvertResult result)
    {
        var messages = result.Messages;
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var errors = messages
            .Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.Error)
            .ToArray();
        var warnings = messages
            .Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.Warning
                        && !BenignMessageCodes.Contains(m.Code))
            .ToArray();
        var compatNotes = messages
            .Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote)
            .ToArray();

        var builder = new StringBuilder();
        if (errors.Length > 0)
        {
            builder.AppendLine("=== " + BuildTitleForSeverity(FrontedLayoutPackageLegacyConvertMessageSeverity.Error) + " ===");
            foreach (var msg in errors.Take(MaxUserSummaryItems))
            {
                builder.AppendLine("- " + msg.Message);
            }

            if (errors.Length > MaxUserSummaryItems)
            {
                builder.AppendLine($"... (+ {errors.Length - MaxUserSummaryItems} more)");
            }

            builder.AppendLine();
        }

        if (warnings.Length > 0)
        {
            builder.AppendLine("=== " + BuildTitleForSeverity(FrontedLayoutPackageLegacyConvertMessageSeverity.Warning) + " ===");
            foreach (var msg in warnings.Take(MaxUserSummaryItems))
            {
                builder.AppendLine("- " + msg.Message);
            }

            if (warnings.Length > MaxUserSummaryItems)
            {
                builder.AppendLine($"... (+ {warnings.Length - MaxUserSummaryItems} more)");
            }

            builder.AppendLine();
        }

        if (compatNotes.Length > 0)
        {
            builder.AppendLine("=== " + BuildTitleForSeverity(FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote) + " ===");
            foreach (var msg in compatNotes.Take(MaxUserSummaryItems))
            {
                builder.AppendLine("- " + msg.Message);
            }

            if (compatNotes.Length > MaxUserSummaryItems)
            {
                builder.AppendLine($"... (+ {compatNotes.Length - MaxUserSummaryItems} more)");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 构建技术详情文本，按严重级别分组，用于日志输出。
    /// </summary>
    public static string BuildTechnicalDetails(FrontedLayoutPackageLegacyConvertResult result)
    {
        var builder = new StringBuilder();

        AppendGroup(builder, "Errors",
            result.Messages.Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.Error));
        AppendGroup(builder, "Warnings",
            result.Messages.Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.Warning));
        AppendGroup(builder, "CompatibilityNotes",
            result.Messages.Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote));
        AppendGroup(builder, "Info",
            result.Messages.Where(m => m.Severity == FrontedLayoutPackageLegacyConvertMessageSeverity.Info));

        return builder.ToString().TrimEnd();
    }

    private static void AppendGroup(
        StringBuilder builder,
        string title,
        IEnumerable<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var items = messages.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(title + ":");
        foreach (var item in items)
        {
            builder.Append("- [").Append(item.Code).Append("] ");
            builder.AppendLine(item.Message);
        }
    }

    private static string BuildTitleForSeverity(FrontedLayoutPackageLegacyConvertMessageSeverity severity) =>
        severity switch
        {
            FrontedLayoutPackageLegacyConvertMessageSeverity.Error => "Errors / 错误",
            FrontedLayoutPackageLegacyConvertMessageSeverity.Warning => "Warnings / 警告",
            FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote => "Compatibility Notes / 兼容性提示",
            FrontedLayoutPackageLegacyConvertMessageSeverity.Info => "Info / 信息",
            _ => severity.ToString()
        };
}

#pragma warning restore CS1591
