#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.Text;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class LegacyConversionMessageFormatter
{
    private const int MaxUserSummaryItems = 3;

    public static bool HasUserFacingWarnings(FrontedLayoutPackageLegacyConvertResult result)
    {
        return BuildUserIssues(result).Count > 0;
    }

    public static string BuildUserSummary(FrontedLayoutPackageLegacyConvertResult result)
    {
        var issues = BuildUserIssues(result);
        if (issues.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("旧布局包已导入，但有 ")
            .Append(issues.Count)
            .Append(" 项内容无法完全转换。建议打开前台编辑器检查布局。");

        foreach (var issue in issues.Take(MaxUserSummaryItems))
        {
            builder.AppendLine();
            builder.Append("- ").Append(issue);
        }

        return builder.ToString();
    }

    public static string BuildTechnicalDetails(FrontedLayoutPackageLegacyConvertResult result)
    {
        var groups = new List<(string Title, IReadOnlyList<string> Items)>
        {
            ("Infos", result.Infos),
            ("Diagnostics", result.Diagnostics),
            ("Warnings", result.Warnings),
            ("UnsupportedProperties", result.UnsupportedProperties),
            ("MissingResources", result.MissingResources)
        };

        var builder = new StringBuilder();
        foreach (var (title, items) in groups.Where(group => group.Items.Count > 0))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(title + ":");
            foreach (var item in items)
            {
                builder.AppendLine("- " + item);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> BuildUserIssues(FrontedLayoutPackageLegacyConvertResult result)
    {
        var issues = new List<string>();

        if (result.MissingResources.Count > 0
            || result.Warnings.Any(IsMissingResourceWarning))
        {
            issues.Add("部分旧版图片资源未找到，已使用默认资源或留空。");
        }

        if (result.UnsupportedProperties.Count > 0)
        {
            issues.Add("部分旧版设置当前没有 Designer v3 对应项。");
        }

        if (result.Warnings.Any(IsUnknownControlWarning))
        {
            issues.Add("部分未知旧版控件未转换。");
        }

        if (result.Warnings.Any(IsUnknownLayoutWarning))
        {
            issues.Add("部分未知旧版布局文件已跳过。");
        }

        if (result.Warnings.Any(IsValidationWarning))
        {
            issues.Add("部分转换后的布局需要手动检查。");
        }

        foreach (var warning in result.Warnings)
        {
            if (IsBenignDiagnostic(warning)
                || IsMissingResourceWarning(warning)
                || IsUnknownControlWarning(warning)
                || IsUnknownLayoutWarning(warning)
                || IsValidationWarning(warning))
            {
                continue;
            }

            var sanitized = SanitizeTechnicalWarning(warning);
            if (!string.IsNullOrWhiteSpace(sanitized)
                && !issues.Contains(sanitized, StringComparer.Ordinal))
            {
                issues.Add(sanitized);
            }
        }

        return issues;
    }

    private static bool IsBenignDiagnostic(string message)
    {
        return ContainsAny(
            message,
            "Legacy resource copied",
            "Legacy global score cells aggregated",
            "Irregular cell spacing was approximated by median gaps",
            "Legacy control geometry fuzzy-matched",
            "Legacy overtime score cells were consumed",
            "Legacy lock overlay geometry consumed",
            "legacy lock overlay merged",
            "known legacy overlay consumed");
    }

    private static bool IsMissingResourceWarning(string message)
    {
        return message.Contains("Legacy resource missing", StringComparison.OrdinalIgnoreCase)
               || message.Contains("resource missing", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not packaged for field", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnknownControlWarning(string message)
    {
        return message.Contains("no v3 control matches", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnknownLayoutWarning(string message)
    {
        return message.Contains("Unknown legacy layout file", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidationWarning(string message)
    {
        return message.Contains("validation errors", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string message, params string[] values)
    {
        return values.Any(value => message.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeTechnicalWarning(string warning)
    {
        var closestIndex = warning.IndexOf("Closest candidates:", StringComparison.OrdinalIgnoreCase);
        if (closestIndex >= 0)
        {
            warning = warning[..closestIndex].TrimEnd(' ', '.', ';', ':');
        }

        return warning.Length <= 120
            ? warning
            : warning[..117] + "...";
    }
}

#pragma warning restore CS1591
