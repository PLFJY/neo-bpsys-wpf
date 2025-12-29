namespace neo_bpsys_wpf.Core.Plugins.Abstractions;

/// <summary>
/// 插件元数据默认实现
/// </summary>
public class PluginMetadata : IPluginMetadata
{
    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public required Version Version { get; init; }

    /// <inheritdoc/>
    public required string Author { get; init; }

    /// <inheritdoc/>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <inheritdoc/>
    public Version? MinimumHostVersion { get; init; }

    /// <inheritdoc/>
    public string? IconPath { get; init; }

    /// <inheritdoc/>
    public string? HomepageUrl { get; init; }
}
