#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutActivePackageState
{
    public string PackageId { get; set; } = "builtin";

    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
}

#pragma warning restore CS1591
