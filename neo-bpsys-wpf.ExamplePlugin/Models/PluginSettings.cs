using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.ExamplePlugin.Models;

public partial class PluginSettings : ObservableObjectBase
{
    [ObservableProperty] private string _testSetting = "Hello World";
}