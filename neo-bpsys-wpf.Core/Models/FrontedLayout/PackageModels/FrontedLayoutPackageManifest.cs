#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageManifest
{
    public string Format { get; set; } = "neo-bpsys-bpui";

    public int FormatVersion { get; set; } = 3;

    public string PackageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string MinVersion { get; set; } = string.Empty;

    public int LayoutSchemaVersion { get; set; } = 3;

    public FrontedLayoutPackageManifestContent Content { get; set; } = new();

    public FrontedLayoutPackageImportPolicy ImportPolicy { get; set; } = new();
}

public sealed class FrontedLayoutPackageManifestContent
{
    public List<FrontedLayoutPackageLayoutEntry> Layouts { get; set; } = [];

    public List<FrontedLayoutPackageResourceEntry> Resources { get; set; } = [];

    public FrontedLayoutPackagePreviewEntry? Preview { get; set; }
}

public sealed class FrontedLayoutPackageLayoutEntry
{
    public string Window { get; set; } = string.Empty;

    public string Canvas { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public sealed class FrontedLayoutPackageResourceEntry
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Uri { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class FrontedLayoutPackagePreviewEntry
{
    public string Cover { get; set; } = string.Empty;
}

public sealed class FrontedLayoutPackageImportPolicy
{
    public string OverwriteExistingUserLayouts { get; set; } = "Ask";

    public bool RequireRestart { get; set; }
}

#pragma warning restore CS1591
