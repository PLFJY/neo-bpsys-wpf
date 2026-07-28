using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using Xunit;

namespace neo_bpsys_wpf.Tests.Helpers;

public class DownloadMirrorPresetsTest
{
    [Fact]
    public void FindLowestLatencyOption_returns_available_option_with_lowest_latency()
    {
        var unavailable = new PluginMarketMirrorOption { Value = "https://unavailable.example/", LatencyMs = -1 };
        var slow = new PluginMarketMirrorOption { Value = "https://slow.example/", LatencyMs = 120 };
        var fastest = new PluginMarketMirrorOption { Value = "https://fast.example/", LatencyMs = 35 };

        var result = DownloadMirrorPresets.FindLowestLatencyOption([unavailable, slow, fastest]);

        Assert.Same(fastest, result);
    }

    [Fact]
    public void FindLowestLatencyOption_returns_null_when_no_option_succeeded()
    {
        var untested = new PluginMarketMirrorOption { Value = "https://untested.example/" };
        var unavailable = new PluginMarketMirrorOption { Value = "https://unavailable.example/", LatencyMs = -1 };

        var result = DownloadMirrorPresets.FindLowestLatencyOption([untested, unavailable]);

        Assert.Null(result);
    }
}
