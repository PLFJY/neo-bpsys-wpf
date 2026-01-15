using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Core.Services.Registry;

public static class BackendPagesRegistryService
{
    internal static List<BackendPageInfo> Registered { get; } = [];
}