namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// SmartBP module package manifest.
/// </summary>
public sealed class SmartBpModuleManifest
{
    /// <summary>
    /// Component identifier.
    /// </summary>
    public string ComponentId { get; set; } = SmartBpModuleConstants.ComponentId;

    /// <summary>
    /// Module package version.
    /// </summary>
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Runtime ABI version.
    /// </summary>
    public int RuntimeAbiVersion { get; set; } = SmartBpModuleConstants.RuntimeAbiVersion;

    /// <summary>
    /// Runtime identifier.
    /// </summary>
    public string Rid { get; set; } = SmartBpModuleConstants.Rid;

    /// <summary>
    /// Required app version range.
    /// </summary>
    public string RequiredAppVersion { get; set; } = ">=3.0.0";

    /// <summary>
    /// Package versions included in the module.
    /// </summary>
    public Dictionary<string, string> PackageVersions { get; set; } = [];

    /// <summary>
    /// Packaged asset metadata.
    /// </summary>
    public SmartBpModuleAsset Asset { get; set; } = new();
}

/// <summary>
/// SmartBP module asset metadata.
/// </summary>
public sealed class SmartBpModuleAsset
{
    /// <summary>
    /// Asset file name.
    /// </summary>
    public string Name { get; set; } = "SmartBpModule.7z";

    /// <summary>
    /// Asset download URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Asset size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Asset SHA-256 hash.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;
}
