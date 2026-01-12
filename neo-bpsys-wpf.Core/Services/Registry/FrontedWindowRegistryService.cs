using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Services.Registry;

public static class FrontedWindowRegistryService
{
    internal static List<FrontedWindowInfo> RegisteredWindow { get; } = [];

    internal static List<InjectedControlInfo> InjectedControls { get; set; } = [];
}