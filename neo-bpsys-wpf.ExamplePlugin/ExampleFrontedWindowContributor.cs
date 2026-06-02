using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.ExamplePlugin.ViewModels;
using neo_bpsys_wpf.ExamplePlugin.Views;

namespace neo_bpsys_wpf.ExamplePlugin;

public sealed class ExampleFrontedWindowContributor : IFrontedWindowPluginContributor
{
    public IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows()
    {
        yield return new FrontedPluginWindowDescriptor
        {
            PackageId = "plfjy.ExamplePlugin",
            WindowId = "3363BFE1-1393-4765-B926-001B6848FAF7",
            WindowTypeName = "ExampleXamlWindow",
            DisplayName = "Example XAML Window",
            Description = "Example plugin-provided WPF fronted window.",
            Kind = FrontedWindowKind.PluginXaml,
            WindowType = typeof(ExampleXamlWindow),
            ViewModelType = typeof(ExampleXamlWindowViewModel)
        };
    }
}
