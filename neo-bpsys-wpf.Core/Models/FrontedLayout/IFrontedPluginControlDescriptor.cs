using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Non-generic metadata view for a Designer v3 plugin fronted control descriptor.
/// </summary>
public interface IFrontedPluginControlDescriptor
{
    string PackageId { get; }

    string ControlTypeName { get; }

    string FullControlType { get; }

    Type ConfigType { get; }

    IReadOnlyList<FrontedPluginPropertyDescriptor>? Properties { get; }

    string? DisplayNameKey { get; }

    string? DescriptionKey { get; }

    string? Icon { get; }

    Version? MinHostVersion { get; }

    int ConfigSchemaVersion { get; }
}
