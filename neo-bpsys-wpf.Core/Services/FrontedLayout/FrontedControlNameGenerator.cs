using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 生成在画布内唯一的 v3 设计器控件名称。
/// </summary>
public class FrontedControlNameGenerator
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 使用 ControlType 作为前缀生成第一个可用名称。
    /// </summary>
    public string Generate(string controlType, FrontedCanvasDesignDocument document)
    {
        var prefix = Regex.Replace(controlType, "[^A-Za-z0-9_]", string.Empty);
        if (string.IsNullOrWhiteSpace(prefix) || char.IsDigit(prefix[0]))
        {
            prefix = $"Control{prefix}";
        }

        var usedNames = document.Controls
            .Select(control => control.Name)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"{prefix}{index}";
            if (ValidControlNameRegex.IsMatch(candidate) && !usedNames.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique name for '{controlType}'.");
    }
}
