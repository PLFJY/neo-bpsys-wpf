using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Core.Services.Registry;

public static class FrontedWindowRegistryService
{
    public static List<FrontedWindowInfo> Registered { get; } = [];
}