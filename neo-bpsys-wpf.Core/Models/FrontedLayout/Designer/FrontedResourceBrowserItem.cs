using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Resource item displayed by Designer v3 Resource Browser.
/// </summary>
public sealed class FrontedResourceBrowserItem
{
    public string DisplayName { get; init; } = string.Empty;

    public string SelectedPath { get; init; } = string.Empty;

    public string? FilePath { get; init; }

    public string Category { get; init; } = string.Empty;

    public ImageSource? Thumbnail { get; init; }

    public bool IsAbsoluteFile { get; init; }
}
