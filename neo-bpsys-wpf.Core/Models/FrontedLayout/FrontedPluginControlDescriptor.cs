using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Runtime and Designer metadata for a plugin fronted control.
/// </summary>
public sealed class FrontedPluginControlDescriptor<TConfig> : IFrontedPluginControlDescriptor
    where TConfig : FrontedControlConfigBase
{
    public required string PackageId { get; init; }

    public required string ControlTypeName { get; init; }

    public string FullControlType => new FrontedPluginControlType(PackageId, ControlTypeName).ToString();

    public required Type ConfigType { get; init; }

    public required Func<string, TConfig, FrontedControlBuildContext, FrameworkElement> CreateControl { get; init; }

    public Func<TConfig>? CreateDefaultConfig { get; init; }

    public IReadOnlyList<FrontedPluginPropertyDescriptor>? Properties { get; init; }

    public Func<TConfig, IEnumerable<FrontedLayoutValidationMessage>>? Validate { get; init; }

    public string? DisplayNameKey { get; init; }

    public string? DescriptionKey { get; init; }

    public string? Icon { get; init; }

    public Version? MinHostVersion { get; init; }

    public int ConfigSchemaVersion { get; init; } = 1;
}
