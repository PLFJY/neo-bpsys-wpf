using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Generates v3 designer control names that are unique within a Canvas.
/// </summary>
public class FrontedControlNameGenerator
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Generates the first available name using the ControlType as prefix.
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
