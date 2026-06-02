using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.ExampleFrontedControls;

public sealed class ExampleLayoutWindowContributor : IFrontedWindowPluginContributor
{
    public const string WindowTypeName = "ExampleLayoutOverlay";

    public IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows()
    {
        yield return new FrontedPluginWindowDescriptor
        {
            PackageId = TeamCardFrontedControlContributor.PackageId,
            WindowId = "B11F63A4-1765-4870-9E36-0AE654026421",
            WindowTypeName = WindowTypeName,
            DisplayName = "Example Layout Overlay",
            Description = "Designer v3 plugin layout window with Text and TeamCard controls.",
            Kind = FrontedWindowKind.PluginLayout,
            Canvases =
            [
                new FrontedCanvasDescriptor
                {
                    CanvasName = "BaseCanvas",
                    DisplayName = "BaseCanvas",
                    Customizable = true,
                    DefaultWidth = 1440D,
                    DefaultHeight = 810D
                }
            ]
        };
    }
}
