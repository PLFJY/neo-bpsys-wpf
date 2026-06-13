using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

internal static class FrontedWindowConfigTestExtensions
{
    internal static FrontedCanvasConfig ToCanvasConfig(this FrontedWindowConfig config) =>
        FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config);
}
